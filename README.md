# AR 巡检问题标注系统

> 一个完整的 AR 巡检问题上报系统：Unity 移动端 + Go 后端 + React 管理端

[English Documentation](README_EN.md) | [手机连接指南](手机连接指南.md)

## 📸 预览

*Unity AR 移动端（平面检测 → 点击放置标记 → 填写表单 → 提交问题）*

*React 管理端（查看问题列表 → 修改状态 → 筛选统计）*

## ✨ 功能特性

### 📱 Unity AR 移动端
- ✅ 实时平面检测（AR Foundation + ARCore）
- ✅ 点击平面放置 AR 标记
- ✅ 现场填写问题信息（标题/描述/优先级）
- ✅ 自动记录 3D 坐标
- ✅ 优先级配色（高=红色，中=橙色，低=蓝色）
- ✅ 输入校验和成功/失败提示

### 🖥️ React 管理端
- ✅ 问题列表展示
- ✅ 状态修改（待处理 → 处理中 → 已解决）
- ✅ 按状态和优先级筛选
- ✅ 统计卡片
- ✅ 错误处理和加载状态

### ⚙️ Go 后端
- ✅ RESTful API（健康检查、列表、创建、更新）
- ✅ JSON 文件持久化（原子写入）
- ✅ CORS 支持
- ✅ 参数校验
- ✅ 唯一 ID 生成
- ✅ 零第三方依赖（仅标准库）

## 🛠️ 技术栈

| 组件 | 技术 |
|------|------|
| **移动端** | Unity 2022.3 LTS, AR Foundation 5.1.1, ARCore |
| **后端** | Go 1.22+ |
| **前端** | React 19, TypeScript 5.9, Vite 7.3 |
| **平台** | Android (支持 ARCore 的设备) |

## 🚀 快速开始

### 前置要求

- Go 1.22+
- Node.js 18+ 和 npm
- Unity Hub + Unity 2022.3 LTS（含 Android Build Support）
- 支持 ARCore 的 Android 设备

### 1. 启动后端

```bash
cd server
go run .
```

服务器将在 `http://localhost:8080` 启动，数据保存在 `data/issues.json`

### 2. 启动前端（可选）

```bash
cd web
npm install
npm run dev
```

打开 http://localhost:5173

**生产环境（后端托管前端）：**
```bash
cd web && npm run build
cd ../server && STATIC_DIR=../web/dist go run .
```

### 3. 配置 Unity 移动端

#### 打开项目
1. 用 Unity Hub 打开 `unity/` 目录
2. 自动导入 AR Foundation 和 ARCore 包

#### 配置后端地址 ⚠️
1. 在 Hierarchy 选中 `App` 物体
2. 在 Inspector 的 `ARInspectionBootstrap` 组件中
3. 设置 **Backend Base URL** 为你的电脑局域网 IP：
   ```
   http://192.168.1.20:8080
   ```
   
**获取 IP 地址：**
```bash
# Windows
ipconfig

# Mac/Linux
ifconfig
```

#### Android 构建设置
**File > Build Settings > Player Settings**：

| 设置项 | 值 |
|--------|---|
| Platform | Android |
| Minimum API Level | 24+ |
| Scripting Backend | IL2CPP |
| Target Architectures | ARM64 |
| Active Input Handling | Both |
| **Allow downloads over HTTP** | **Always allowed** ⚠️ |
| Internet Access | Require |

#### 配置防火墙（Windows）
```powershell
# 管理员身份运行 PowerShell
netsh advfirewall firewall add rule name="AR Inspection 8080" dir=in action=allow protocol=TCP localport=8080
```

#### 构建并部署
1. 手机开启 USB 调试，连接电脑
2. Unity 中：**File > Build Settings > Build And Run**

### 4. 测试网络连通性

在手机浏览器访问：
```
http://你的电脑IP:8080/api/health
```

看到以下响应说明连接成功：
```json
{"status":"ok","service":"AR Inspection API"}
```

## 📖 使用方法

### 移动端操作

1. **扫描平面** - 缓慢移动手机对准地面，直到出现蓝色平面
2. **放置标记** - 点击平面上任意位置，生成黄色球体
3. **填写表单** - 输入标题（必填）、描述（可选），选择优先级
4. **提交** - 点击「提交问题」
   - 成功：标记变色，显示问题 ID
   - 失败：显示错误信息
5. **继续巡检** - 点击「关闭」继续下一个

### 管理端操作

- 查看所有已提交问题
- 通过下拉菜单修改状态
- 使用筛选器过滤问题
- 查看顶部统计数据
- 点击刷新按钮重新加载

## 🔌 API 接口

基础 URL: `http://localhost:8080`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/health` | 健康检查 |
| GET | `/api/issues` | 获取问题列表 |
| POST | `/api/issues` | 创建新问题 |
| PATCH | `/api/issues/{id}/status` | 更新问题状态 |

### 创建问题示例

**请求：**
```json
{
  "title": "入口墙面破损",
  "description": "左侧墙体存在裂缝",
  "priority": "high",
  "position": { "x": 0.42, "y": 0.03, "z": 1.26 }
}
```

**响应（201）：**
```json
{
  "id": "iss_9442254dc9",
  "title": "入口墙面破损",
  "description": "左侧墙体存在裂缝",
  "priority": "high",
  "status": "open",
  "position": { "x": 0.42, "y": 0.03, "z": 1.26 },
  "createdAt": "2026-10-27T03:47:59.037Z",
  "updatedAt": "2026-10-27T03:47:59.037Z"
}
```

详细 API 文档见 [README_EN.md](README_EN.md#-api-reference)

## 📁 项目结构

```
ar-inspection/
├── server/              # Go 后端
│   ├── main.go          # 启动入口
│   ├── handlers.go      # 路由和处理器
│   ├── store.go         # JSON 持久化
│   ├── models.go        # 数据模型
│   └── handlers_test.go # API 测试
├── web/                 # React 前端
│   └── src/
│       ├── App.tsx      # 主组件
│       ├── api.ts       # 后端客户端
│       └── types.ts     # 类型定义
└── unity/               # Unity AR 客户端
    └── Assets/ARInspection/Scripts/
        ├── ARInspectionBootstrap.cs   # 应用初始化
        ├── ARPlacementController.cs   # AR 交互
        ├── InspectionFormUI.cs        # 运行时 UI
        ├── IssueApiClient.cs          # HTTP 客户端
        ├── TapInput.cs                # 输入处理
        └── Models.cs                  # 数据传输对象
```

## 🧪 测试

**后端测试：**
```bash
cd server
go test -v ./...
```

覆盖场景：
- API 端点验证
- 参数校验
- 持久化测试
- 错误处理

## 🐛 常见问题

### 手机无法连接后端

**检查项：**
- ✅ 手机和电脑在同一 Wi-Fi 网络
- ✅ Unity 中配置的是电脑 IP（不是 `localhost`）
- ✅ 防火墙允许 8080 端口
- ✅ Player Settings 允许 HTTP 下载

**测试：** 手机浏览器访问 `http://电脑IP:8080/api/health`

### 平面检测不工作

**解决方案：**
- 确保光线充足（不要太亮或太暗）
- 寻找有纹理的表面（避免纯色）
- 缓慢移动设备
- 安装 Google Play Services for AR

### Unity 构建失败

**验证：**
- ✅ Android Build Support 已安装
- ✅ Scripting Backend 设为 IL2CPP
- ✅ Target Architectures 勾选 ARM64
- ✅ Allow downloads over HTTP 设为 Always allowed

更多问题见 [手机连接指南.md](手机连接指南.md#常见问题排查)

## 📝 开发记录

本项目使用 AI 辅助开发，完整的开发过程和决策记录见 [AI_LOG.md](AI_LOG.md)

## 📄 许可

本项目开源，仅供学习和教育目的使用。

## 🙏 致谢

使用 Unity AR Foundation、Go 和 React 构建，展示完整的全栈 AR 开发工作流。
