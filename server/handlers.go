package main

import (
	"encoding/json"
	"errors"
	"log"
	"mime"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"time"
)

// apiError 统一错误体：{"error":{"code":"...","message":"..."}}
type apiError struct {
	Code    string `json:"code"`
	Message string `json:"message"`
}

type errorBody struct {
	Error apiError `json:"error"`
}

func writeJSON(w http.ResponseWriter, status int, payload any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(payload)
}

func writeError(w http.ResponseWriter, status int, code, message string) {
	writeJSON(w, status, errorBody{Error: apiError{Code: code, Message: message}})
}

// newRouter 组装全部路由，方便测试直接注入临时 Store。
func newRouter(store *Store, staticDir string) http.Handler {
	mux := http.NewServeMux()

	mux.HandleFunc("GET /api/health", func(w http.ResponseWriter, r *http.Request) {
		writeJSON(w, http.StatusOK, map[string]any{
			"status":  "ok",
			"service": "ar-inspection-server",
			"time":    time.Now().UTC(),
		})
	})

	mux.HandleFunc("GET /api/issues", func(w http.ResponseWriter, r *http.Request) {
		items := store.List()
		writeJSON(w, http.StatusOK, map[string]any{
			"items": items,
			"count": len(items),
		})
	})

	mux.HandleFunc("POST /api/issues", func(w http.ResponseWriter, r *http.Request) {
		r.Body = http.MaxBytesReader(w, r.Body, 1<<20)
		var req createIssueRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			writeError(w, http.StatusBadRequest, "invalid_json", "请求体不是合法 JSON："+err.Error())
			return
		}

		req.Title = strings.TrimSpace(req.Title)
		req.Description = strings.TrimSpace(req.Description)
		if req.Title == "" {
			writeError(w, http.StatusBadRequest, "invalid_title", "标题不能为空")
			return
		}
		if !validPriorities[req.Priority] {
			writeError(w, http.StatusBadRequest, "invalid_priority", "优先级必须是 low、medium 或 high")
			return
		}

		issue, err := store.Create(req)
		if err != nil {
			log.Printf("create issue failed: %v", err)
			writeError(w, http.StatusInternalServerError, "persist_failed", "数据保存失败，请稍后重试")
			return
		}
		log.Printf("issue created: id=%s priority=%s title=%q", issue.ID, issue.Priority, issue.Title)
		writeJSON(w, http.StatusCreated, issue)
	})

	mux.HandleFunc("PATCH /api/issues/{id}/status", func(w http.ResponseWriter, r *http.Request) {
		id := r.PathValue("id")
		r.Body = http.MaxBytesReader(w, r.Body, 1<<20)
		var req updateStatusRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			writeError(w, http.StatusBadRequest, "invalid_json", "请求体不是合法 JSON："+err.Error())
			return
		}
		if !validStatuses[req.Status] {
			writeError(w, http.StatusBadRequest, "invalid_status", "状态必须是 open、in_progress 或 resolved")
			return
		}

		issue, err := store.UpdateStatus(id, req.Status)
		if errors.Is(err, errIssueNotFound) {
			writeError(w, http.StatusNotFound, "issue_not_found", "问题不存在："+id)
			return
		}
		if err != nil {
			log.Printf("update status failed: %v", err)
			writeError(w, http.StatusInternalServerError, "persist_failed", "数据保存失败，请稍后重试")
			return
		}
		writeJSON(w, http.StatusOK, issue)
	})

	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		if strings.HasPrefix(r.URL.Path, "/api/") {
			writeError(w, http.StatusNotFound, "not_found", "接口不存在："+r.Method+" "+r.URL.Path)
			return
		}
		if staticDir == "" {
			writeError(w, http.StatusNotFound, "not_found", "未开启静态资源目录（STATIC_DIR）")
			return
		}
		serveStatic(w, r, staticDir)
	})

	return withCORS(mux)
}

// withCORS 允许浏览器（Vite 开发服务器 / 静态页面）跨源调用。
func withCORS(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		h := w.Header()
		h.Set("Access-Control-Allow-Origin", "*")
		h.Set("Access-Control-Allow-Methods", "GET, POST, PATCH, OPTIONS")
		h.Set("Access-Control-Allow-Headers", "Content-Type")
		h.Set("Access-Control-Max-Age", "600")
		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusNoContent)
			return
		}
		next.ServeHTTP(w, r)
	})
}

// serveStatic 提供 web/dist 构建产物，并为前端路由回退到 index.html。
func serveStatic(w http.ResponseWriter, r *http.Request, dir string) {
	clean := filepath.Clean("/" + r.URL.Path)
	full := filepath.Join(dir, clean)
	if info, err := os.Stat(full); err == nil && !info.IsDir() {
		if ct := mime.TypeByExtension(filepath.Ext(full)); ct != "" {
			w.Header().Set("Content-Type", ct)
		}
		http.ServeFile(w, r, full)
		return
	}
	index := filepath.Join(dir, "index.html")
	if _, err := os.Stat(index); err != nil {
		writeError(w, http.StatusNotFound, "not_found", "静态资源不存在")
		return
	}
	http.ServeFile(w, r, index)
}
