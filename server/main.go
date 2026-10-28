package main

import (
	"context"
	"errors"
	"log"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"syscall"
	"time"
)

func main() {
	port := envOrDefault("PORT", "8080")
	dataFile := envOrDefault("DATA_FILE", filepath.Join("data", "issues.json"))
	staticDir := os.Getenv("STATIC_DIR")

	store, err := NewStore(dataFile)
	if err != nil {
		log.Fatalf("载入数据文件失败（%s）：%v", dataFile, err)
	}
	log.Printf("数据文件：%s（已载入 %d 条历史问题）", dataFile, len(store.List()))

	srv := &http.Server{
		Addr:              ":" + port,
		Handler:           newRouter(store, staticDir),
		ReadHeaderTimeout: 10 * time.Second,
	}

	go func() {
		log.Printf("AR 巡检服务已启动：http://localhost:%s （健康检查 /api/health）", port)
		if staticDir != "" {
			log.Printf("同时托管静态页面：%s", staticDir)
		}
		if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			log.Fatalf("服务启动失败：%v", err)
		}
	}()

	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt, syscall.SIGTERM)
	<-stop

	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	if err := srv.Shutdown(ctx); err != nil {
		log.Printf("关闭服务时出错：%v", err)
	}
	log.Println("服务已退出")
}

func envOrDefault(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}
