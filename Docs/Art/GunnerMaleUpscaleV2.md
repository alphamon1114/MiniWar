# 남캐 확대 원화 V2 — 2026-09-28

사용자는 남캐도 선택한 여캐와 비슷한 수준으로 **원화 해상도와 세부 묘사**를 높이길 요청했다. 게임 화면에서 캐릭터를 크게 만드는 요청은 아니다. 작업 중 머리 볼륨이 과하다는 피드백을 반영해 정수리 높이와 뒤쪽 머리 실루엣을 낮춘 수정본을 기준으로 사용했다.

내장 **image_gen**을 사용한 확대/세부 재작화다. 갈색 머리, 남색·금색 코트, 청색 스카프, 흰 크라바트, 가느다란 어두운 바지와 갈색 부츠를 유지하고 얼굴·머리카락·장식·의복 주름을 더 세밀하게 그렸다. 래스터 픽셀은 별도 스크립트로 리사이즈하거나 합성하지 않았다.

## 최종 파일

모두 `Assets/Art/Studies/GunnerMaleUpscaleV2/`에 저장했다. **각 2172×724 RGBA PNG**, 가로 4포즈씩 총 16포즈다.

|파일|내용|생성 원본 식별자|프롬프트|
|---|---|---|---|
|MaleIdle.png|대기 4장, 머리 볼륨 수정|`exec-7cdbc051-ba51-42b2-b589-70fed2639b01.png`|[대기 확대](GunnerMaleUpscaleV2-idle-prompt.txt), [최종 머리 수정](GunnerMaleUpscaleV2-hair-final-prompt.txt)|
|MaleWalkA.png|걷기 앞부분 4장|`exec-921573c9-9b44-4fba-a2ab-198688fa3bc4.png`|[걷기 A](GunnerMaleUpscaleV2-walk-a-prompt.txt)|
|MaleWalkB.png|걷기 뒷부분 4장|`exec-6be5a8a8-0e2d-456b-be88-517df64bbc8b.png`|[걷기 B](GunnerMaleUpscaleV2-walk-b-prompt.txt)|
|MaleAir.png|웅크림·상승·정점·하강 4장|`exec-40bd5b26-5bd8-4bdc-ad5a-78b79271e40f.png`|[공중/착지](GunnerMaleUpscaleV2-air-prompt.txt)|

생성 원본은 내장 도구의 `generated_images/01a0cd05-2b12-7731-9a52-2f4f468fe937/`에 남겨뒀다. 프로젝트에서는 위 복사본을 사용한다.

## 해상도와 Unity 등록

- 대기 몸체의 불투명 영역 높이는 약 **661~662px**다. 현재 게임 PixelV1의 첫 대기 원화(약 549px)보다 한 캐릭터를 더 많은 픽셀로 담았다.
- 투명 알파와 16개 프레임의 존재, PNG 바깥 경계에서 머리/발끝이 잘리지 않는 것을 확인했다. 기록: `Logs/QA/MaleUpscaleV2.json`.
- Unity에서도 2172×724를 유지한다. Point 필터, 압축 없음, 밉맵 없음, 최대 4096, PPU 240.
- 연결된 알파 영역으로 4개 스프라이트씩 등록했다. 일부 그림이 수학적 4등분 경계를 조금 넘으므로 단순 고정 격자로 자르지 않는다.
- 이 파일들은 새 **몸체 원화**다. 새 양팔 시트나 장비 그림을 만든 작업이 아니며, 현재 온라인 프리팹의 PixelV1 바인딩은 그대로다. 기존 3×2 Actions + 4×2 Walk와 배치가 달라 게임 연결 시 별도 프레임/어깨 등록이 필요하다.
- 원화 확대는 걷기 발 순서나 전체 애니메이션 일관성 검증을 대체하지 않는다. 이후 모션 조립 때 프레임 간 얼굴·비율·발 기준을 맞춰 확인한다.

## 동작 미리보기

2026-09-30: 양발 교차와 다리의 가려짐 순서를 수정한 [Walk V4](GunnerMaleWalkV4.md)가 추가됐다. 아래 최초 GIF 이후의 최신 걷기 결과는 `Logs/QA/MaleWalkV4.gif`다.

- `Logs/QA/MaleUpscaleV2Motion.gif`: 최신 몸체 16포즈에 기존 PixelV1 양팔·총기를 붙인 대기/걷기/점프 비교. 1200×720, 20fps, 4초 반복.
- `Tools/McpSetup/capture-male-upscale-v2.cs.txt`에 원본 캔버스 기준 어깨 좌표와 캡처 코드를 기록했다. Unity의 임시 PreviewRenderUtility에서 렌더링하며, 온라인 프리팹이나 원본 스프라이트 등록은 수정하지 않는다.
- 지상 프레임은 발바닥, 공중 프레임은 어깨 기준으로 루트 위치를 맞췄다. 점프 궤적은 현재 게임의 JumpSpeed/Gravity를 사용한다.
- 렌더링에서 16포즈 사용을 확인하고, GIF 디코딩 결과가 원본 80프레임의 시간 순서와 같은지 검증했다.
- 기존 팔은 새 몸체보다 픽셀 표현이 굵다. 걷기 원화의 발 순서/보폭도 추가 조정 대상이며, 이 파일은 완성된 게임 애니메이션이 아닌 모션 확인용이다.

## 참조 및 중간 결과

- 남캐 디자인/동작: `Assets/Resources/Online/PixelV1/MaleActions.png`, `MaleWalk.png`.
- 여캐의 세부 묘사 기준: `Docs/References/GunnerFemale-preferred-design.png`. 여캐의 복장/얼굴을 남캐에 복사하지 않는다.
- 이전 남캐 머리/비율 참고: `Docs/References/GunnerMale-proportions-reference.png`.
- 16칸 통합 초안 `exec-8ba21694-bf30-4e10-9fdb-58136de99c9f.png`는 1086×1448로 생성되어 개별 프레임 해상도가 충분히 늘지 않았고 하단 여백도 부족했다. 최종으로 채택하지 않고, `Docs/References/GunnerMale-upscale-layout-reference.png`에 동작 배치 참고용으로 보관했다. [초기 프롬프트](GunnerMaleUpscaleV1-prompt.txt).
- 최초 확대 대기 `exec-7ad7dbca-025f-48e6-9127-4f7944f20241.png`와 1차 머리 수정 `exec-c8992abc-5b88-4fd4-94e8-b168008c4137.png`는 머리 볼륨 추가 수정으로 교체했다. [1차 머리 수정 프롬프트](GunnerMaleUpscaleV2-hair-prompt.txt).
