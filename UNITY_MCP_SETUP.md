# Unity MCP (CoplayDev) 설정 가이드

이 프로젝트는 [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) (패키지명
`com.coplaydev.unity-mcp`)를 사용해 AI 어시스턴트(Claude Code / Claude Desktop / Cursor 등)와
Unity 에디터를 연결합니다.

## 이미 완료된 것 (저장소에 반영됨)

`Packages/manifest.json` 에 다음 의존성이 추가되어 있습니다.

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main",
"com.unity.nuget.newtonsoft-json": "3.0.2",
```

→ **이 프로젝트를 Unity 에디터로 열면 Unity MCP Bridge 패키지가 자동으로 설치**됩니다.
(Unity 2021.3 이상 필요, 본 프로젝트는 Unity 6이라 충족)

## 로컬에서 마무리해야 하는 단계

실제 "연결"은 각자 개발 PC에서 아래 단계를 거쳐야 완성됩니다. (Unity 에디터가 로컬에서
실행 중이어야 하고, PC/OS마다 경로가 달라 저장소에 커밋할 수 없는 부분입니다.)

### 1. Python + uv 설치

MCP 서버는 Python 3.10+ 와 [`uv`](https://docs.astral.sh/uv/) 로 실행됩니다.

```bash
# macOS / Linux
curl -LsSf https://astral.sh/uv/install.sh | sh

# Windows (PowerShell)
powershell -ExecutionPolicy Bypass -c "irm https://astral.sh/uv/install.ps1 | iex"
```

### 2. Unity 에디터에서 프로젝트 열기

- Package Manager가 `com.coplaydev.unity-mcp` 를 자동으로 가져옵니다.
- 필요 시 수동 추가: `Window → Package Manager → + → Add package from git URL...` 에
  `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main` 입력.

### 3. MCP 클라이언트 자동 설정

Unity 에디터 메뉴:

```
Window → MCP for Unity → Configure All Detected Clients
```

- 감지된 MCP 클라이언트(Claude Code/Desktop, Cursor 등)의 설정 파일에
  Unity MCP 서버 항목이 자동으로 등록됩니다.
- 이 과정에서 Python MCP 서버도 함께 설치/구성됩니다.

### 4. 연결 확인

- Unity MCP 창에서 상태가 **Connected (초록색)** 인지 확인합니다.
  (Unity Bridge는 기본적으로 로컬 TCP 포트 `6400` 을 사용)
- Claude Code 사용 시: `claude mcp list` 로 `unityMCP`(또는 `UnityMCP`) 서버가
  등록·연결되었는지 확인합니다.

## 참고

- 이 저장소가 실행 중인 원격/헤드리스 환경에는 Unity 에디터가 없으므로, **실시간 연결은
  로컬 개발 PC에서만** 이루어집니다. 저장소에는 "설치를 트리거하는 매니페스트 의존성"까지만
  포함됩니다.
- 공식 문서: https://coplaydev.github.io/unity-mcp/
- 저장소: https://github.com/CoplayDev/unity-mcp
