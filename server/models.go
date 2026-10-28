package main

import "time"

// 问题优先级 / 状态枚举，Unity 与 React 端保持同一套取值。
const (
	PriorityLow    = "low"
	PriorityMedium = "medium"
	PriorityHigh   = "high"

	StatusOpen       = "open"
	StatusInProgress = "in_progress"
	StatusResolved   = "resolved"
)

var validPriorities = map[string]bool{
	PriorityLow:    true,
	PriorityMedium: true,
	PriorityHigh:   true,
}

var validStatuses = map[string]bool{
	StatusOpen:       true,
	StatusInProgress: true,
	StatusResolved:   true,
}

// Position 是 AR 标记在放置时的世界坐标（米）。
type Position struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
	Z float64 `json:"z"`
}

// Issue 是三端共用的数据结构。
type Issue struct {
	ID          string    `json:"id"`
	Title       string    `json:"title"`
	Description string    `json:"description"`
	Priority    string    `json:"priority"`
	Status      string    `json:"status"`
	Position    Position  `json:"position"`
	CreatedAt   time.Time `json:"createdAt"`
	UpdatedAt   time.Time `json:"updatedAt"`
}

// createIssueRequest 是 Unity 上报问题时提交的字段。
type createIssueRequest struct {
	Title       string    `json:"title"`
	Description string    `json:"description"`
	Priority    string    `json:"priority"`
	Position    *Position `json:"position"`
}

// updateStatusRequest 是 React 修改处理状态时提交的字段。
type updateStatusRequest struct {
	Status string `json:"status"`
}
