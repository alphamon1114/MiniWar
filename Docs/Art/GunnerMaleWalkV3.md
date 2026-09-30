# 남캐 걷기 교차 수정 — 2026-09-30

이 버전은 사용자가 교차 시 다리의 가려짐 순서가 뒤집히는 문제를 지적했다. 현재 미리보기는 이를 수정한 [Walk V4](GunnerMaleWalkV4.md)를 사용한다. V3 원화와 GIF는 비교용으로 남겼다.

이전 확대 원화의 걷기는 같은 다리가 계속 앞서서 발을 끄는 것처럼 보였다. 사용자 요청에 따라 양발이 번갈아 앞서는 착지와 서로 지나치는 교차 자세를 새로 그려 8포즈 반복으로 연결했다.

## 원화와 재생 순서

`Assets/Art/Studies/GunnerMaleUpscaleV2/`에 저장했다. 모두 내장 **image_gen** 편집 모드로 만든 투명 PNG이며 별도 래스터 편집 스크립트는 사용하지 않았다.

|원화|내용|최종 생성 원본|
|---|---|---|
|`MaleWalkV3Keys.png`|왼발 착지, 오른발 교차, 오른발 착지, 왼발 교차|`exec-cd9299aa-7bdf-4803-a68c-488a5df51c87.png`|
|`MaleWalkV3Between.png`|각 핵심 포즈 사이의 체중 이동/내딛기|`exec-5d6d65b5-0ce5-4648-b07f-2f7309a3189b.png`|

은색 허벅지 띠가 있는 다리를 미리보기에서 왼발로 구분한다. 이 띠가 앞쪽 다리에 고정되어 있지 않고, 반대발 착지 때 뒤로 뻗은 다리에 남아 있도록 그렸다. 부츠 윗단은 양쪽 모두 금색이다.

1. Keys 0 — 왼발 앞으로
2. Between 0 — 왼발에 체중 이동
3. Keys 1 — 교차, 오른발 전진
4. Between 1 — 오른발 내딛기
5. Keys 2 — 오른발 앞으로
6. Between 2 — 오른발에 체중 이동
7. Keys 3 — 교차, 왼발 전진
8. Between 3 — 왼발 내딛기 → 1로 반복

## Unity 연결

- `MaleWalkV3Body.asset`에 대기 4장, 새 걷기 8장, 기존 점프/착지 4장을 등록했다. Sprite 서브에셋과 텍스처 참조를 저장 후 다시 읽어 검증한다.
- 양팔은 기존 PixelV1 양팔 한 장을 그대로 쓴다. 몸체/다리의 래스터 포즈만 교체하고 프레임별 어깨 위치를 맞춘다.
- `MiniWar > Characters > Motion preview`에서 남캐의 **New male walk study**를 켜면 새 원화가 재생된다. 걷기 반복은 1초이며 일시정지 후 Pose phase로 포즈를 확인할 수 있다.
- `Tools/McpSetup/build-male-walk-v3.cs.txt`에 스프라이트 사각형 추출, 어깨 위치, 기준점과 재생 순서를 저장했다.
- 이전 `capture-male-upscale-v2`도 새 Body.asset이 있으면 이를 사용한다. 온라인 게임 프리팹의 PixelV1 연결은 이번 미리보기 수정에서 변경하지 않았다.

## 결과 확인

- `Logs/QA/MaleWalkV3.gif`: 1000×640, 20fps, 4초 반복. 왼쪽 1초 주기, 오른쪽 2초 주기.
- Unity에서 실제 CharacterRig.Pose로 8개의 걷기 포즈가 모두 선택되는지, 팔이 각 어깨 위치를 따르는지 확인한다.
- 렌더 프레임을 GIF로 묶은 뒤 디코딩해 80프레임의 시간 순서와 4초 길이가 일치하는지 검증한다.
- 교차/착지 대표 프레임을 눈으로 점검했다. 상체와 팔의 세부 도트 밀도를 맞추는 작업은 별도다.

## 프롬프트

- [핵심 4포즈](GunnerMaleWalkV3-keyframes-prompt.txt)
- [교차 다리와 부츠 수정](GunnerMaleWalkV3-keyframes-correction-prompt.txt)
- [중간 4포즈](GunnerMaleWalkV3-inbetweens-prompt.txt)
- [부츠 색상 통일](GunnerMaleWalkV3-cuff-correction-prompt.txt)

최초 8칸 통합 시안은 반대발 착지가 제대로 표현되지 않아 채택하지 않았다. 위 4포즈 단위 원화가 최종이다.
