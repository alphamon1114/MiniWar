# MiniWar 집에서 이어하기 — 2026-09-23

## 현재 상태

Windows 온라인 개발 빌드의 계정 등록/로그인, 남녀 캐릭터 선택, 공용 마을, 채팅, 장비 저장 및 외형 동기화까지 구현했다. 파티, 온라인 던전 전투, 부활/관전, 카드 보상, 성장, 거래, 랭킹은 후속 작업이다.

학원 PC의 게임 미리보기 서버는 종료했다. 실행 파일과 저장 데이터는 학원 PC에 남아 있다. 집에서는 새 로컬 서버를 실행한다. Unity MCP는 게임 서버와 다른 개발 도구이다.

이번 작업은 아직 커밋/푸시하지 않았다. 로컬 기준 커밋은 `cae2c72bb0fc0d2e63a12f6fdfeb006ec09a8525` (`UIUpdate`)이다. GitHub만 내려받으면 이번 변경은 들어 있지 않으므로 Notion의 소스 백업 ZIP을 사용한다.

## 소스 복원

1. Notion의 같은 날짜 소스 백업 ZIP을 모두 내려받는다. 각 ZIP은 독립적인 압축 파일이며 모두 필요하다.
2. 작업 중인 파일이 없는 새 폴더(예: `C:/unity/HomeRestore`)에 모든 ZIP을 풀고 같은 `MiniWar` 폴더로 합친다. 최종적으로 `MiniWar/Assets`, `MiniWar/Packages`, `MiniWar/ProjectSettings`, `MiniWar/Server`, `MiniWar/Tools`, `MiniWar/Docs`가 한곳에 있어야 한다.
3. Git 이력을 유지하려면 먼저 저장소를 별도 새 폴더에 clone하고, 필요시 위 기준 커밋에서 새 작업 브랜치를 만든 다음 백업의 `MiniWar` 폴더 내용물을 저장소 루트에 덮어쓴다. 기존 작업 폴더에 바로 덮어쓰지 않는다.
4. 백업은 Git 추적 파일 및 무시되지 않은 새 소스를 담는다. `.git`, `Library`, `Builds`, `Logs`, Unity MCP 가상환경, 테스트/실사용 계정 데이터, 서버 인증서/개인 키는 포함하지 않는다. 실행 파일은 집에서 다시 빌드한다.

저장소: https://github.com/alphamon1114/MiniWar

## 집 PC 최초 실행

필요 도구: Unity Hub와 **Unity 6000.6.0f1**, Windows 빌드 지원, Git, **.NET 10 SDK**. 학원 PC의 서버 빌드 SDK는 10.0.401이었다.

1. Unity Hub에서 복원한 프로젝트 루트를 열고 패키지 복원과 스크립트 컴파일을 기다린다. 패키지 복원에는 인터넷이 필요하다.
2. 프로젝트 루트에서 PowerShell을 열어 서버를 빌드한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Build-Server.ps1
New-Item -ItemType Directory -Force .\Logs | Out-Null
```

3. Unity 메뉴 **MiniWar > Online > Build Windows client**를 실행한다. 서버는 `Builds/Server`, 게임은 `Builds/OnlineClient`에 생성된다.
4. 루트의 **PlayMiniWar.cmd**를 더블클릭한다. `127.0.0.1:17778`의 로컬 서버를 실행하고 임시 캐릭터로 마을에 자동 접속한다. 처음에는 서버/클라이언트 빌드가 모두 필요하다.
5. 게임 상단 **여성으로 보기 / 남성으로 보기**로 외형을 확인한다. A/D 이동, W/Space 점프, 우클릭 조준, I 장비, 1/2 무기 교체.
6. 게임 창을 닫은 후 **StopMiniWarServer.cmd**로 미리보기 서버를 종료한다. 게임 창만 닫으면 서버는 계속 실행된다.

미리보기 계정은 임시 계정이며 다음 실행에서 같은 캐릭터를 이어 쓰지 않는다. 일반 계정 로그인을 확인하려면 `Docs/ONLINE_QUICKSTART.md`의 LAN 실행 절차를 따른다. 집에서 혼자 확인할 때 외부 포트포워딩이나 학원 서버 접속은 필요 없다.

## Codex와 Unity MCP

게임 실행에는 MCP가 필요 없다. 집 Codex로 에디터를 조작하려면 Python 3.10+와 pip를 준비한 뒤 `Docs/UNITY_MCP.md`를 따른다. `Tools/Install-UnityMcp.ps1`로 새 가상환경을 설치하고 Unity 메뉴 **MiniWar > Unity MCP > Connect local server**로 연결한다. 학원 PC의 가상환경이나 전역 Codex 설정은 백업에 없다.

```powershell
codex mcp add unityMCP --url http://127.0.0.1:8765/mcp
```

## 최신 결정과 코드 위치

- 기획 기준: `Docs/ONLINE_DESIGN.md`. 초기 Notion의 WebGL/싱글플레이/4종 총기/파산 즉시 종료 설계보다 최신 결정을 우선한다.
- 총기는 피스톨, 리볼버, 기관단총, 샷건, 소총, 저격총 6종. 근접은 별도. 미사일 제외. 피스톨과 리볼버는 각각 3등급이며 등급 상승으로 서로 바뀌지 않는다.
- 통신 버전 2: 피스톨 0 / 기관단총 1 / 샷건 2 / 소총 3 / 저격총 4 / 근접 5 / 리볼버 6. 기존 저장 ID를 바꾸지 않는다.
- 서버: `Server/`. 공용 통신: `Assets/Scripts/Online/Shared/`. 클라이언트: `Assets/Scripts/Online/Client/`.
- 새 씬: `Assets/Scenes/OnlineLobby.unity`. 기존 Town/Dungeon 씬은 보존되어 있으나 온라인 흐름과 아직 연결하지 않았다.
- 몸체와 총기는 별도 그림: `Assets/Resources/Online/`. `Handguns.png`의 왼쪽 열은 피스톨, 오른쪽 열은 리볼버. 기존 `Weapons.png`의 첫 번째 권총 열은 사용하지 않는다.
- 성장 초안: `Claude outputs/`. 최신 무기 구분을 유지하며 서버 판정으로 이식한다.
- 아트 제작 기록: `Docs/ART_PROMPTS.md`.

## 검증 및 남은 작업

2026-09-23 자동 검증 52개 통과. Windows 개발 빌드 성공(errors=0). 실제 TLS 클라이언트 2개와 Windows 실행 파일 2개로 계정, 남녀/장비 외형, 이동/조준, 채팅 동기화를 확인했다. 60명 월드 슬롯 검증은 모형 검증이며 실제 PC 30대 부하 검증은 남아 있다.

```powershell
dotnet run --project .\Tests\Online\MiniWar.Online.Tests.csproj
```

권장 다음 작업 순서:

1. 복원 후 서버/클라이언트를 빌드하고 로컬 마을 접속을 확인한다.
2. 파티 생성/초대/탈퇴/준비 및 던전 인스턴스 입장을 구현한다.
3. 첫 던전 성문 외곽과 고정 대포 보스로 전투 → 부활/관전 → 카드 보상 → 마을 복귀 한 바퀴를 완성한다.
4. 서버가 재화/장비/보상을 확정하도록 성장·거래를 연결한다. 거래 확정과 보상 지급의 중복/동시 요청을 검증한다.
5. 나머지 던전 3개와 4~6인 마족 레이드, 클리어 시간 랭킹을 추가한다.
6. 보행/점프/발사/재장전/피격 애니메이션과 손목 조준 연동, 실제 LAN 다중 PC 테스트를 진행한다.

아직 미정인 수치: 피스톨/리볼버별 공격력·연사·장탄수, 최종 마족 이름과 구체 패턴. 입장 시 공격력 보너스 고정, 부활 시 체력/무적 시간, 카드 선택 시간 초과 처리는 제안 기본안과 사용자 확정을 구분해 적용한다.

## 집 Codex에 전달할 문장

> MiniWar를 이어서 작업해줘. 먼저 Docs/HOME_HANDOFF.md, ONLINE_DESIGN.md, ONLINE_QUICKSTART.md, UNITY_MCP.md를 읽고 복원된 소스와 실행 환경을 확인해줘. 현재는 온라인 마을 기반까지 구현되어 있고 파티/던전 전투는 미완성이야. 피스톨과 리볼버는 독립 총기이며 통신 버전 2와 기존 저장 ID를 유지해줘. 우선 로컬 실행을 확인한 다음 파티와 첫 던전 성문 외곽의 전체 플레이 흐름을 구현해줘.
