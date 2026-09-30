# 맥 / 엘리어스 기본 모션 V1 — 2026-09-30

사용자가 승인한 `MacElias-Concept.png`를 기준으로 남녀 기본 모션을 새로 생성하고 실제 게임 프리팹에 연결했다. 내장 **image_gen**을 사용했으며 전체 생성·수정 프롬프트와 원본 출력 식별자는 [FinalMotionPrompts.json](FinalMotionPrompts.json)에 보관한다. 참고 이미지는 같은 폴더에 보존했다.

## 현재 사용 에셋

프로젝트 원화는 `Assets/Resources/Online/RedrawV1/`에 있다. 원본 픽셀을 코드로 늘이거나 다시 그리지 않고, Unity 스프라이트의 알파 경계·피벗·PPU를 등록한다.

- `MaleActions.png`, `FemaleActions.png`: 대기 2장, 착지 웅크림, 점프 상승·정점·하강. 1536×1024.
- `MaleWalk.png`, `FemaleWalk.png`: 걷기 시트. 1536×1024.
- `MaleWalkKeys.png`, `FemaleWalkKeys.png`: 반대쪽 발 접지와 앞뒤 교차를 보강한 4개 자세. 2172×724.
- `MaleArms.png`: 긴 총/권총의 5방향 조준과 대기, 12장. 1097×1434.
- `FemaleArms.png`: 같은 12개 자세. 1024×1536.
- `MaleBody.asset`, `FemaleBody.asset`: 대기 4칸(2장 반복) + 걷기 8칸 + 착지/공중 4칸.
- `MaleArms.asset`, `FemaleArms.asset`: 양팔 그림 12장과 실제 장비 손잡이 위치.
- `MalePortrait.png`, `FemalePortrait.png`: 완성된 Unity 리그를 투명 배경에 렌더링한 캐릭터 선택 이미지, 256×384.

걷기의 8개 타이밍은 `Walk0 → Walk1 → Key3 → Key0 → Key0 → Key1 → Key2 → Walk7`이다. 서로 다른 그림은 7장이고 반대쪽 접지 자세에 한 박자를 더 준다. 앞다리와 뒷다리의 교차가 읽히도록 추가 키를 선택했다. 걷기 주기는 남녀 모두 1초이며, 뒤로 이동할 때 역순으로 재생한다. 원화 프레임별 코트와 얼굴의 세부 연속성은 추후 다듬을 수 있다.

몸체(머리·목·몸통·다리 포함) 한 장 + 양팔 한 장 + 장비를 표시한다. 팔꿈치/손목 IK는 사용하지 않는다. 이전 가로 0.76·세로 0.94·팔 두께 0.80 보정은 해제했고, 현재 세 값은 모두 **1**이다. 서 있는 몸체 높이는 NPC 기준과 같은 2.6월드 단위다. 공중 자세는 어깨 기준으로 등록해 다리를 접을 때 몸이 늘어나지 않는다. 무기 외형과 발사 순간의 에임 보정 방식은 유지한다.

## 재생성과 미리보기

- Unity 메뉴 `MiniWar > Characters > Build part sprites and rigs`: 원화 등록과 남녀 게임 프리팹 재생성. 현재 등록 코드는 `RedrawCharacterTools.cs`다.
- `Render character selection portraits`: 현재 리그로 선택 화면 이미지 갱신.
- `Motion preview`: 서버 없이 성별·동작·무기·좌우·5방향 조준·반동을 확인한다. 이전 남캐 V4 덮어쓰기 옵션은 제거했다.
- 실제 게임 프리팹은 기존 경로 `Assets/Resources/Online/Rigs/GunnerMale.prefab`, `GunnerFemale.prefab`을 유지한다.

## 검증

- 2,100개 리그 조합에서 새 원화 참조, 원래 비율, Point 필터, 표시 레이어 수, 어깨·손잡이 연결, 좌우 총구 방향 통과. 이는 미적 자연스러움의 자동 판정은 아니다.
- `Logs/QA/RedrawMotionV1.gif`: 실제 Unity 렌더링 대기·걷기·점프·걷기 중 5방향 반동. 1200×900, 20fps 샘플 80개, 4초. GIF 중복 병합 후 66프레임이며 디코딩한 순서와 전체 시간을 원본과 대조했다.
- `Logs/QA/RedrawWalkKeys.png`: 남녀 8개 걷기 타이밍 비교.
- `Logs/QA/RedrawFiveWayAim.png`: 남녀 5방향 권총 자세.

공유·인계를 위해 [움직임 GIF](MotionPreview.gif), [걷기 프레임](WalkFrames.png), [5방향 조준](FiveWayAim.png)도 이 문서 폴더에 복사했다.

Windows 모션 빌드(2026-09-30 12:28)는 `Succeeded errors=0`. 이 빌드의 실제 Windows 클라이언트 두 개로 남녀 로그인, 장비 교체, 한글 상대 채팅, 두 레이어 모션, 본인/상대 사격 동기화를 통과했다. 시험 서버와 클라이언트는 종료했다. 결과는 `Logs/QA/RigClients/`에 있다. 이후 선택 화면 초상화의 RGBA 투명도와 sRGB 저장을 수정하고 알파 검증을 거쳐 **12:35 최종 빌드도 `Succeeded errors=0`**을 확인했다. 이 마지막 변경은 초상화 렌더링만 포함한다.

기본 이동·조준용 1차 모션이다. 재장전·피격·사망·근접 공격 애니메이션은 아직 별도로 만들지 않았다.
