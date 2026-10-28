package main

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"testing"
)

func newTestServer(t *testing.T) (*Store, http.Handler) {
	t.Helper()
	store, err := NewStore(filepath.Join(t.TempDir(), "issues.json"))
	if err != nil {
		t.Fatalf("NewStore: %v", err)
	}
	return store, newRouter(store, "")
}

func doJSON(t *testing.T, h http.Handler, method, path, body string) *httptest.ResponseRecorder {
	t.Helper()
	req := httptest.NewRequest(method, path, strings.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	rec := httptest.NewRecorder()
	h.ServeHTTP(rec, req)
	return rec
}

func TestHealth(t *testing.T) {
	_, h := newTestServer(t)
	rec := doJSON(t, h, http.MethodGet, "/api/health", "")
	if rec.Code != http.StatusOK {
		t.Fatalf("status = %d, want 200", rec.Code)
	}
	var body map[string]any
	if err := json.Unmarshal(rec.Body.Bytes(), &body); err != nil {
		t.Fatalf("decode: %v", err)
	}
	if body["status"] != "ok" {
		t.Fatalf("status field = %v, want ok", body["status"])
	}
}

func TestCreateIssue(t *testing.T) {
	_, h := newTestServer(t)
	payload := `{"title":"入口墙面破损","description":"左侧墙体存在裂缝","priority":"high","position":{"x":0.42,"y":0.03,"z":1.26}}`
	rec := doJSON(t, h, http.MethodPost, "/api/issues", payload)
	if rec.Code != http.StatusCreated {
		t.Fatalf("status = %d, want 201, body=%s", rec.Code, rec.Body.String())
	}

	var issue Issue
	if err := json.Unmarshal(rec.Body.Bytes(), &issue); err != nil {
		t.Fatalf("decode: %v", err)
	}
	if issue.ID == "" {
		t.Fatal("id should be generated")
	}
	if issue.Status != StatusOpen {
		t.Fatalf("status = %q, want open", issue.Status)
	}
	if issue.Position.Z != 1.26 {
		t.Fatalf("position.z = %v, want 1.26", issue.Position.Z)
	}
	if issue.CreatedAt.IsZero() || issue.UpdatedAt.IsZero() {
		t.Fatal("timestamps should be generated")
	}

	list := doJSON(t, h, http.MethodGet, "/api/issues", "")
	if list.Code != http.StatusOK {
		t.Fatalf("list status = %d, want 200", list.Code)
	}
	var listBody struct {
		Count int     `json:"count"`
		Items []Issue `json:"items"`
	}
	if err := json.Unmarshal(list.Body.Bytes(), &listBody); err != nil {
		t.Fatalf("decode list: %v", err)
	}
	if listBody.Count != 1 || len(listBody.Items) != 1 {
		t.Fatalf("count = %d, items = %d, want 1", listBody.Count, len(listBody.Items))
	}
}

func TestCreateIssueValidation(t *testing.T) {
	cases := []struct {
		name     string
		payload  string
		wantCode string
	}{
		{"empty title", `{"title":"   ","priority":"low"}`, "invalid_title"},
		{"missing title", `{"priority":"low"}`, "invalid_title"},
		{"bad priority", `{"title":"裂缝","priority":"urgent"}`, "invalid_priority"},
		{"missing priority", `{"title":"裂缝"}`, "invalid_priority"},
		{"bad json", `{"title":`, "invalid_json"},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			_, h := newTestServer(t)
			rec := doJSON(t, h, http.MethodPost, "/api/issues", tc.payload)
			if rec.Code != http.StatusBadRequest {
				t.Fatalf("status = %d, want 400, body=%s", rec.Code, rec.Body.String())
			}
			var body errorBody
			if err := json.Unmarshal(rec.Body.Bytes(), &body); err != nil {
				t.Fatalf("decode: %v", err)
			}
			if body.Error.Code != tc.wantCode {
				t.Fatalf("error code = %q, want %q", body.Error.Code, tc.wantCode)
			}
			if body.Error.Message == "" {
				t.Fatal("error message should not be empty")
			}
		})
	}
}

func TestUpdateStatus(t *testing.T) {
	_, h := newTestServer(t)
	rec := doJSON(t, h, http.MethodPost, "/api/issues", `{"title":"设备异常","priority":"medium"}`)
	var created Issue
	_ = json.Unmarshal(rec.Body.Bytes(), &created)

	for _, status := range []string{StatusInProgress, StatusResolved, StatusOpen} {
		path := "/api/issues/" + created.ID + "/status"
		res := doJSON(t, h, http.MethodPatch, path, `{"status":"`+status+`"}`)
		if res.Code != http.StatusOK {
			t.Fatalf("update to %s: status = %d, body=%s", status, res.Code, res.Body.String())
		}
		var updated Issue
		_ = json.Unmarshal(res.Body.Bytes(), &updated)
		if updated.Status != status {
			t.Fatalf("status = %q, want %q", updated.Status, status)
		}
		if updated.UpdatedAt.Before(created.UpdatedAt) {
			t.Fatal("updatedAt should not go backwards")
		}
	}

	bad := doJSON(t, h, http.MethodPatch, "/api/issues/"+created.ID+"/status", `{"status":"done"}`)
	if bad.Code != http.StatusBadRequest {
		t.Fatalf("invalid status = %d, want 400", bad.Code)
	}
	missing := doJSON(t, h, http.MethodPatch, "/api/issues/iss_not_exist/status", `{"status":"open"}`)
	if missing.Code != http.StatusNotFound {
		t.Fatalf("missing issue = %d, want 404", missing.Code)
	}
}

// 验证"Go 服务重启后数据仍然存在"：换一个 Store 实例重新读取同一文件。
func TestPersistenceAcrossRestart(t *testing.T) {
	path := filepath.Join(t.TempDir(), "issues.json")
	store, err := NewStore(path)
	if err != nil {
		t.Fatalf("NewStore: %v", err)
	}
	issue, err := store.Create(createIssueRequest{Title: "墙面裂缝", Priority: PriorityHigh})
	if err != nil {
		t.Fatalf("Create: %v", err)
	}
	if _, err := store.UpdateStatus(issue.ID, StatusResolved); err != nil {
		t.Fatalf("UpdateStatus: %v", err)
	}

	reloaded, err := NewStore(path)
	if err != nil {
		t.Fatalf("reload: %v", err)
	}
	got, ok := reloaded.Get(issue.ID)
	if !ok {
		t.Fatal("issue should survive restart")
	}
	if got.Status != StatusResolved || got.Title != "墙面裂缝" {
		t.Fatalf("reloaded issue = %+v", got)
	}
}

func TestCORSAndUnknownRoute(t *testing.T) {
	_, h := newTestServer(t)

	req := httptest.NewRequest(http.MethodOptions, "/api/issues", nil)
	rec := httptest.NewRecorder()
	h.ServeHTTP(rec, req)
	if rec.Code != http.StatusNoContent {
		t.Fatalf("preflight = %d, want 204", rec.Code)
	}
	if rec.Header().Get("Access-Control-Allow-Origin") != "*" {
		t.Fatal("missing CORS header")
	}

	missing := doJSON(t, h, http.MethodGet, "/api/unknown", "")
	if missing.Code != http.StatusNotFound {
		t.Fatalf("unknown api = %d, want 404", missing.Code)
	}
	var body errorBody
	if err := json.Unmarshal(bytes.TrimSpace(missing.Body.Bytes()), &body); err != nil {
		t.Fatalf("decode: %v", err)
	}
	if body.Error.Code != "not_found" {
		t.Fatalf("code = %q, want not_found", body.Error.Code)
	}
}
