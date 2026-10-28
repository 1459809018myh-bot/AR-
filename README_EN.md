# AR Inspection System

> A full-stack AR-powered inspection issue reporting system with Unity mobile client, Go backend, and React dashboard.

## 🎯 Overview

This project demonstrates a complete end-to-end workflow for facility inspection teams to report issues using AR markers in real environments, which managers can then review and process through a web dashboard.

**Three-tier architecture:**

```
Unity AR (Android/ARCore)      →    Go Backend (:8080)    →    React Dashboard (:5173)
Detect planes & place markers   →   REST API + JSON store  →   View & update status
```

## ✨ Features

### 📱 Unity AR Mobile Client
- Real-time horizontal plane detection using AR Foundation
- Tap-to-place AR markers on detected surfaces
- In-app form to submit issues with:
  - Title (required)
  - Description (optional)
  - Priority (low/medium/high)
  - 3D position (auto-captured)
- Color-coded markers by priority (red/orange/blue)
- Input validation and user feedback

### 🖥️ React Management Dashboard
- Issue list with filtering by status and priority
- Status update workflow (open → in_progress → resolved)
- Statistics cards showing issue counts
- Error handling with graceful degradation
- Auto-refresh capability

### ⚙️ Go Backend
- RESTful API with health check endpoint
- JSON file persistence with atomic writes
- CORS-enabled for browser access
- Input validation with clear error messages
- Unique ID generation for issues
- Zero external dependencies (standard library only)

## 🛠️ Tech Stack

| Component | Technologies |
|-----------|-------------|
| **Mobile** | Unity 2022.3 LTS, AR Foundation 5.1.1, ARCore XR Plugin 5.1.1 |
| **Backend** | Go 1.22+ (standard library only) |
| **Frontend** | React 19, TypeScript 5.9, Vite 7.3 |
| **Platform** | Android (ARCore-compatible devices) |

## 🚀 Quick Start

### Prerequisites

- **Go 1.22+** installed
- **Node.js 18+** and npm
- **Unity Hub** with Unity 2022.3 LTS
- Android device with ARCore support

### 1. Start the Backend

```bash
cd server
go run .
```

The server will start on `http://localhost:8080` and create a `data/issues.json` file for persistence.

**Environment Variables:**
- `PORT` - Server port (default: 8080)
- `DATA_FILE` - Path to JSON data file (default: data/issues.json)
- `STATIC_DIR` - Optional path to serve frontend build

### 2. Start the React Dashboard

```bash
cd web
npm install
npm run dev
```

Open http://localhost:5173 in your browser.

**Production Build:**
```bash
cd web
npm run build
cd ../server
STATIC_DIR=../web/dist go run .
```

Now both backend and frontend are served from port 8080.

### 3. Configure Unity for Mobile

#### Open the Project
1. Install Unity Hub and Unity 2022.3 LTS with **Android Build Support**
2. Add the `unity/` directory as a project in Unity Hub
3. Unity will automatically import AR Foundation and ARCore packages

#### Configure Backend URL
1. In Unity Hierarchy, select the `App` GameObject
2. In the Inspector, find `ARInspectionBootstrap` component
3. Set **Backend Base URL** to your computer's local IP:
   ```
   http://192.168.1.20:8080
   ```
   ⚠️ Replace `192.168.1.20` with your actual local network IP (find it using `ipconfig` on Windows or `ifconfig` on Mac/Linux)

#### Android Build Settings
Open **File > Build Settings > Player Settings**:

| Setting | Value |
|---------|-------|
| Platform | Android |
| Minimum API Level | 24 or higher |
| Scripting Backend | IL2CPP |
| Target Architectures | ARM64 |
| Active Input Handling | Both |
| Allow downloads over HTTP | **Always allowed** ⚠️ |
| Internet Access | Require |

#### Network Configuration
1. Ensure your phone and computer are on the same Wi-Fi network
2. Configure Windows Firewall (run as Administrator):
   ```powershell
   netsh advfirewall firewall add rule name="AR Inspection 8080" dir=in action=allow protocol=TCP localport=8080
   ```
3. Test connectivity by visiting `http://YOUR_IP:8080/api/health` in your phone's browser

#### Build and Deploy
1. Connect your Android device via USB (with USB debugging enabled)
2. In Unity: **File > Build Settings > Build And Run**

## 📖 Usage

### Mobile App Workflow

1. **Detect Planes** - Slowly move your device to scan the floor until blue plane meshes appear
2. **Place Marker** - Tap on a detected plane to place a yellow sphere marker
3. **Fill Form** - Enter title (required), description (optional), and select priority
4. **Submit** - Tap "Submit Issue" button
   - Success: Marker changes color based on priority, displays issue ID
   - Failure: Shows error message from backend
5. **Continue** - Tap "Close" to remove marker and place another

### Dashboard Operations

- View all submitted issues in a table
- Filter by status (open/in progress/resolved) or priority
- Click status dropdown to update issue workflow
- View summary statistics at the top
- Click refresh button to reload data

## 🔌 API Reference

Base URL: `http://localhost:8080`

All endpoints use JSON for request/response bodies.

### Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/health` | Service health check |
| GET | `/api/issues` | List all issues (sorted by creation time, descending) |
| POST | `/api/issues` | Create a new issue |
| PATCH | `/api/issues/{id}/status` | Update issue status |

### POST /api/issues

**Request:**
```json
{
  "title": "Wall damage at entrance",
  "description": "Crack on left wall",
  "priority": "high",
  "position": { "x": 0.42, "y": 0.03, "z": 1.26 }
}
```

**Response (201):**
```json
{
  "id": "iss_9442254dc9",
  "title": "Wall damage at entrance",
  "description": "Crack on left wall",
  "priority": "high",
  "status": "open",
  "position": { "x": 0.42, "y": 0.03, "z": 1.26 },
  "createdAt": "2026-10-27T03:47:59.037Z",
  "updatedAt": "2026-10-27T03:47:59.037Z"
}
```

### PATCH /api/issues/{id}/status

**Request:**
```json
{
  "status": "in_progress"
}
```

**Response (200):**
```json
{
  "id": "iss_9442254dc9",
  "status": "in_progress",
  "updatedAt": "2026-10-27T03:50:12.456Z",
  ...
}
```

### Error Format

All errors return appropriate HTTP status codes with:
```json
{
  "error": {
    "code": "invalid_title",
    "message": "Title cannot be empty"
  }
}
```

## 📁 Project Structure

```
ar-inspection/
├── server/              # Go backend
│   ├── main.go          # Server entry point
│   ├── handlers.go      # HTTP handlers and routing
│   ├── store.go         # JSON persistence layer
│   ├── models.go        # Data models
│   └── handlers_test.go # API tests
├── web/                 # React frontend
│   └── src/
│       ├── App.tsx      # Main component
│       ├── api.ts       # Backend API client
│       └── types.ts     # TypeScript definitions
└── unity/               # Unity AR client
    ├── Assets/ARInspection/Scripts/
    │   ├── ARInspectionBootstrap.cs  # App initialization
    │   ├── ARPlacementController.cs  # AR plane interaction
    │   ├── InspectionFormUI.cs       # Runtime UI generation
    │   ├── IssueApiClient.cs         # HTTP client
    │   ├── TapInput.cs               # Touch/mouse input handling
    │   └── Models.cs                 # Data transfer objects
    └── Packages/manifest.json
```

## 🧪 Testing

**Backend Tests:**
```bash
cd server
go test -v ./...
```

Tests cover:
- API endpoint validation
- Input parameter validation
- Persistence across server restarts
- Error handling

## 🐛 Troubleshooting

### Mobile Can't Connect to Backend

**Check:**
- Phone and computer on same Wi-Fi network
- Backend URL in Unity uses computer's IP (not `localhost`)
- Firewall allows port 8080
- HTTP downloads allowed in Unity Player Settings

**Test:** Visit `http://YOUR_IP:8080/api/health` in phone browser

### Plane Detection Not Working

**Solutions:**
- Ensure good lighting (not too bright or dark)
- Look for textured surfaces (avoid solid colors)
- Move device slowly and steadily
- Check device supports ARCore (install "Google Play Services for AR" from Play Store)

### Unity Build Fails

**Verify:**
- Android Build Support installed in Unity Hub
- Scripting Backend set to IL2CPP
- ARM64 architecture selected
- "Allow downloads over HTTP" set to "Always allowed"

## 📝 License

This project is open source and available for educational purposes.

## 🙏 Acknowledgments

Built with Unity AR Foundation, Go, and React as a demonstration of full-stack AR development workflow.
