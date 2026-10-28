package main

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"sort"
	"sync"
	"time"
)

var (
	errIssueNotFound = errors.New("issue not found")
	errPersist       = errors.New("failed to persist data")
)

// Store 用一份 JSON 文件做持久化：进程启动时整体载入，每次写入后整体落盘。
// 原型阶段数据量很小，这种做法足够，并且重启后数据不丢失。
type Store struct {
	mu     sync.RWMutex
	path   string
	issues []Issue
}

func NewStore(path string) (*Store, error) {
	s := &Store{path: path, issues: []Issue{}}
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return nil, err
	}
	raw, err := os.ReadFile(path)
	if err != nil {
		if errors.Is(err, os.ErrNotExist) {
			return s, nil // 首次运行，文件尚未创建
		}
		return nil, err
	}
	if len(raw) == 0 {
		return s, nil
	}
	if err := json.Unmarshal(raw, &s.issues); err != nil {
		return nil, err
	}
	if s.issues == nil {
		s.issues = []Issue{}
	}
	return s, nil
}

// List 返回按创建时间倒序排列的问题副本。
func (s *Store) List() []Issue {
	s.mu.RLock()
	defer s.mu.RUnlock()
	out := make([]Issue, len(s.issues))
	copy(out, s.issues)
	sort.SliceStable(out, func(i, j int) bool {
		return out[i].CreatedAt.After(out[j].CreatedAt)
	})
	return out
}

func (s *Store) Get(id string) (Issue, bool) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	for _, item := range s.issues {
		if item.ID == id {
			return item, true
		}
	}
	return Issue{}, false
}

// Create 生成唯一 ID、默认状态 open，并立即落盘。
func (s *Store) Create(req createIssueRequest) (Issue, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	now := time.Now().UTC().Truncate(time.Millisecond)
	issue := Issue{
		ID:          s.newIDLocked(),
		Title:       req.Title,
		Description: req.Description,
		Priority:    req.Priority,
		Status:      StatusOpen,
		CreatedAt:   now,
		UpdatedAt:   now,
	}
	if req.Position != nil {
		issue.Position = *req.Position
	}

	prev := s.issues
	s.issues = append(append([]Issue{}, prev...), issue)
	if err := s.persistLocked(); err != nil {
		s.issues = prev // 落盘失败则回滚内存，避免"接口成功但数据丢失"
		return Issue{}, err
	}
	return issue, nil
}

func (s *Store) UpdateStatus(id, status string) (Issue, error) {
	s.mu.Lock()
	defer s.mu.Unlock()

	prev := s.issues
	next := make([]Issue, len(prev))
	copy(next, prev)

	for i := range next {
		if next[i].ID != id {
			continue
		}
		updated := next[i]
		updated.Status = status
		updated.UpdatedAt = time.Now().UTC().Truncate(time.Millisecond)
		next[i] = updated

		s.issues = next
		if err := s.persistLocked(); err != nil {
			s.issues = prev
			return Issue{}, err
		}
		return updated, nil
	}
	return Issue{}, errIssueNotFound
}

func (s *Store) newIDLocked() string {
	for {
		buf := make([]byte, 5)
		if _, err := rand.Read(buf); err != nil {
			// crypto/rand 极难失败，兜底用时间戳保证仍能生成 ID
			return "iss_" + time.Now().UTC().Format("20060102150405.000000")
		}
		id := "iss_" + hex.EncodeToString(buf)
		exists := false
		for _, item := range s.issues {
			if item.ID == id {
				exists = true
				break
			}
		}
		if !exists {
			return id
		}
	}
}

// persistLocked 先写临时文件再原子改名，避免进程中断写出半个 JSON。
func (s *Store) persistLocked() error {
	raw, err := json.MarshalIndent(s.issues, "", "  ")
	if err != nil {
		return errPersist
	}
	tmp := s.path + ".tmp"
	if err := os.WriteFile(tmp, raw, 0o644); err != nil {
		return errPersist
	}
	if err := os.Rename(tmp, s.path); err != nil {
		return errPersist
	}
	return nil
}
