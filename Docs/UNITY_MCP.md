# Unity MCP — MiniWar

MCP for Unity v10.0.0 (MIT), Unity 6000.6.0f1. 에디터 개발 도구이며 게임의 LAN 멀티 서버와는 별개다.

## 연결
1. Python 3.10+와 pip가 설치된 PC에서 `Tools/Install-UnityMcp.ps1` 실행. 전용 가상환경은 `Tools/McpSetup/venv`에 생성된다.
2. 이후 서버 시작은 `Tools/Start-UnityMcp.ps1`. 백그라운드에서 `127.0.0.1:8765`만 사용한다.
3. Unity에서 MiniWar를 열고 패키지 컴파일을 기다린다.
4. `MiniWar > Unity MCP > Connect local server`를 선택한다.
5. Codex에 `codex mcp add unityMCP --url http://127.0.0.1:8765/mcp`로 등록한다.
6. 현재 Codex 작업의 도구 목록에 나타나지 않으면 Codex를 재시작해 설정을 다시 읽는다.

검증: `Tools/McpSetup/venv/Scripts/python.exe Tools/Probe-UnityMcp.py`.
로그: `Logs/unity-mcp.stdout.log`, `Logs/unity-mcp.stderr.log`, `Library/MiniWarMcpStatus.txt`.

현재 PC에는 Codex 번들 Python을 사용한 전용 가상환경이 설치되어 있다. 다른 PC로 이 폴더를 복사하지 말고 위 설치 스크립트를 다시 실행한다.

`Library/MiniWarMcpAutoConnect` 파일이 있는 로컬 프로젝트는 에디터 로드 시 연결한다. 삭제하면 자동 연결을 끄며 메뉴에서 수동 연결할 수 있다. 이 파일은 Git에 포함되지 않는다.

## 범위
씬/오브젝트/컴포넌트·에셋·콘솔·테스트·빌드 도구를 제공한다. 여러 Unity 프로젝트가 열리면 `MiniWar` 인스턴스를 명시한다. 게임 서버 외부 공개나 학원 방화벽 개방은 이 MCP 설치에 필요하지 않다.

## 출처
- https://github.com/CoplayDev/unity-mcp/tree/v10.0.0
- https://learn.chatgpt.com/docs/extend/mcp?surface=cli
