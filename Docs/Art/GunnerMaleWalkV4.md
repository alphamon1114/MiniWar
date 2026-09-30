# 남캐 걷기 V4 — 다리 겹침 순서 수정

**채택 결정 (2026-09-30):** 사용자가 "좀 애매하긴한데 이대로가자"라고 확인했다. V4를 현 단계 남캐 모션으로 확정하고 추가 미세 조정은 후순위로 둔다.

사용자가 V3 교차 동작에서 한쪽 다리가 갑자기 반대쪽 다리 앞으로 튀어나온다고 지적했다. 발이 이동하는 앞뒤 위치만 맞췄고, 카메라에 가까운 다리와 먼 다리의 가려짐 순서를 포즈 사이에서 유지하지 못한 것이 문제였다.

## 수정 기준

- 은색 허벅지 띠가 없는 다리는 카메라에 가까운 다리로 고정한다.
- 은색 띠가 있는 다리는 카메라에서 먼 다리로 고정한다. 교차할 때 가까운 다리의 뒤로 가려져 지나간다.
- 두 교차 포즈의 과한 무릎 들기를 낮췄다. 특히 먼 다리가 지나가는 포즈에서는 가까운 지지 다리의 허벅지, 정강이, 부츠 윤곽이 연속해서 보이도록 했다.
- 몸체 한 장과 기존 양팔 한 장의 구조를 유지한다. 런타임에서 다리 조각의 정렬 순서를 바꾸는 방식은 사용하지 않는다.

## 저장한 원화

내장 **image_gen** 편집 모드를 사용했다. PNG는 생성 원본을 그대로 복사했고, Unity에서 사각형과 기준점을 등록했다.

|경로|내용|생성 원본|
|---|---|---|
|`Assets/Art/Studies/GunnerMaleUpscaleV2/MaleWalkV4Keys.png`|두 착지와 두 교차 포즈, 2171×724|`exec-531ce713-2e7f-4d87-b2ae-4986c3966e42.png`|
|`Assets/Art/Studies/GunnerMaleUpscaleV2/MaleWalkV4Between.png`|지지/전진 중간 포즈, 2170×725|`exec-b870b743-4eaa-4b6b-895d-91eb16c35516.png`|

프롬프트: [핵심 교차 포즈 수정](GunnerMaleWalkV4-keys-prompt.txt), [중간 포즈 수정](GunnerMaleWalkV4-inbetweens-prompt.txt).

## 연결과 확인

- `MaleWalkV4Body.asset`은 대기 4장, 걷기 8장, 기존 공중/착지 4장을 담는다.
- 걷기 순서는 Keys0 → Between0 → Keys1 → Between1 → Keys2 → Between2 → Keys3 → Between3이다.
- `Tools/McpSetup/build-male-walk-v4.cs.txt`에 어깨 좌표, 발 기준점, 등록 순서를 저장했다. 저장 후 스프라이트와 텍스처 참조 16개를 다시 읽어 검사한다.
- `Assets/Resources/Online/Rigs/GunnerMale.prefab`의 몸체를 V4로 연결했다. `PixelCharacterTools.BuildBody(0)`도 확정 에셋을 사용하므로 캐릭터 재생성 시 이전 원화로 돌아가지 않는다.
- 걷기 속도는 몸체 에셋에 저장하며 V4는 1초에 한 주기다. 온라인 캐릭터와 Unity 미리보기가 같은 설정을 사용한다. 여캐는 기존 속도를 유지한다.
- `Logs/QA/MaleWalkV4.gif`: 1000×640, 20fps, 4초 반복. 왼쪽 보통 속도, 오른쪽 절반 속도.
- 실제 CharacterRig.Pose로 걷기 8포즈를 렌더링하고 어깨 위치를 검사했다. 문제로 지적된 먼 다리 교차 포즈와 그 앞뒤 포즈를 눈으로 확인했다. 채택 후 실제 Resources 남캐 프리팹의 V4 참조와 1초 주기, 남녀 장비·조준·방향 2,100개 조합을 검증했다.
- GIF 인코딩 후 80개 렌더 프레임과 디코딩한 재생 순서/시간이 일치하는지 확인한다. 이 검사는 파일과 재생 순서에 대한 검사이며 그림의 자연스러움을 자동 보장하지 않는다.
- 채택 반영 Windows 빌드: 2026-09-30 09:55, `Succeeded errors=0`. `Builds/OnlineClient/MiniWar.exe`를 갱신했다. 기록: `Logs/OnlineBuildResult.txt`.

이전 V3는 비교용으로 보존했다.
