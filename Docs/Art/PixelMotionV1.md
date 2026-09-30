# PixelMotionV1 원화 기록 — 2026-09-28

남캐 원화 해상도/세부 묘사를 높인 후속 작업은 [GunnerMaleUpscaleV2](GunnerMaleUpscaleV2.md)에 있다. 각 2172×724 PNG 4장, 총 16개 몸체 포즈이며 머리 볼륨 수정 피드백을 반영했다. 아래 표는 현재 게임 PixelV1의 바인딩 기록이다.

## 이후 사용자 선택 — 여캐 외형 기준

사용자가 `exec-6a936d44-b4c4-4638-99e3-d891dd4a10ed.png`를 가장 마음에 드는 여캐로 선택했다. 원본을 수정 없이 [GunnerFemale-preferred-design.png](../References/GunnerFemale-preferred-design.png)에 보관했다. 이후 여캐 작업은 이 그림의 눈매·턱선·금발 머리 실루엣·베레모·허리와 긴 코트 비율을 기준으로 삼는다. 이전의 **도트 느낌 유지** 요구도 유효하다. 도트화하면서 얼굴이나 체형을 새 디자인으로 바꾸지 않는다.

이 선택은 디자인 기준 갱신이다. 아래 표는 이미 게임에 연결한 PixelV1 파일 기록이며, 새로 선택한 4×4 원본을 게임 시트에 그대로 덮어쓴 것은 아니다. 현재 런타임은 Actions 3×2 + Walk 4×2 + 별도 양팔 3×4를 사용하므로 후속 적용 시 프레임과 어깨 연결도 함께 맞춰야 한다.

## 기존 PixelV1 제작 기록

내장 **image_gen**으로 제작했다. 외부 CLI나 API 키는 사용하지 않았다. PNG 원본을 프로젝트에 복사하고, Unity에서 스프라이트 영역·Point 필터·피벗·어깨/손잡이 좌표를 등록한다.

기준: 남캐 `Assets/Art/Studies/PixelGunner/GunnerMalePixelV1.png`, 여캐 사용자 제공 `Docs/References/GunnerFemale-design-reference.png`. 여캐 초기 도트의 얼굴/체형과 매끈한 중간 시안에 대한 피드백을 반영했다. 최종 걷기는 사용자가 지정한 `exec-66de843e-d499-4ae5-a62e-5aff6fab6275.png`를 단독 참조하여 디자인/배치를 유지하고 도트 표현으로 변환한 버전이다.

## 최종 사용 파일

경로 접두사는 `Assets/Resources/Online/PixelV1/`이다. 원본 식별자는 내장 도구의 `generated_images/01a0cd05-2b12-7731-9a52-2f4f468fe937/` 아래 파일명이다. 이 외부 폴더가 없어도 게임은 프로젝트의 복사본을 사용한다.

|파일|PNG 크기|생성 원본|최종 프롬프트|
|---|---|---|---|
|MaleWalk.png|1254×1254|`exec-e90b6785-9370-4d3e-80bc-d3e02f6f892d.png`|[maleWalk 항목](PixelMotionV1-prompts.json)|
|MaleActions.png|1254×1254|`exec-7b4b4e2d-e64c-4f2b-935e-6fe6b94507aa.png`|[대기/점프](PixelMotionV1-actions-prompt.txt)|
|MaleArms.png|1086×1448|`exec-155d76b1-ec86-4f6e-9108-7732f1cf626e.png`|[양팔](PixelMotionV1-arms-prompt.txt)|
|FemaleActions.png|1536×1024|`exec-2941ae1d-f163-43bb-8868-4784590f6ff8.png`|[얼굴 유지·도트 재작화](PixelMotionV1-female-final-actions-prompt.txt)|
|FemaleWalk.png|1254×1254|`exec-b09eca29-5a61-4da6-9474-2f8c0afc0e5b.png`|[지정 이미지 도트 변환](PixelMotionV1-female-exact-walk-prompt.txt)|
|FemaleArms.png|1086×1448|`exec-b24a2869-8afd-4d21-b11a-d1514eb056c4.png`|[양팔 최종](PixelMotionV1-female-final-arms-prompt.txt)|
|Weapons.png|1774×887|`exec-8957e0a0-b8ab-4e7d-8e43-45f36722b734.png`|[장비](PixelMotionV1-weapons-prompt.txt)|
|Handguns.png|1024×1536|`exec-52b35141-f635-445a-9e82-e714ada8878e.png`|[피스톨/리볼버](PixelMotionV1-handguns-prompt.txt)|

도구에 요청한 논리 픽셀 크기·색상 제한은 스타일 지시이며 정확한 격자/팔레트 보장 수치가 아니다. 최종 파일은 투명 알파를 포함하며 Unity에서 Point 필터로 표시한다.

## 시트 배치

- Actions: 3열×2행. 대기 A/B/착지, 상승/정점/하강.
- Walk: 4열×2행. 걷기 8장.
- Arms: 3열×4행. 긴 총 위/윗사선/앞/아랫사선/아래/대기, 권총도 같은 순서.
- Weapons: 6열×3등급. 첫 열 권총은 현재 미사용, 나머지 SMG/샷건/소총/저격총/근접.
- Handguns: 2열×3등급. 피스톨/리볼버.
- `PixelCharacterTools.cs`에 원본 해상도에 맞춘 어깨·손잡이 좌표, 배율과 발 기준점이 있다.

## 교체/미채택 시안

아래 원본은 게임에서 참조하지 않는다. 프롬프트는 후속 수정 참고로 보관한다.

- 여캐 초기 도트: actions `exec-341fabaa-581a-411f-a3e7-4d42acdce742.png`, walk `exec-c4f3c56a-cbde-4e1d-acbd-fbe810561cdc.png`, arms `exec-d5b615d2-77bb-4085-89a2-d81b223e9e58.png`. 프롬프트: female-actions, female-walk-final, female-arms.
- 여캐 매끈한 수정본: actions `exec-bca496a2-a8f2-4472-82c8-7a7d2648c2b9.png`, walk `exec-66de843e-d499-4ae5-a62e-5aff6fab6275.png`, arms `exec-c6dc7b43-1dee-464f-a1f0-ad1ebdbcf03f.png`. 프롬프트: female-v2-actions/walk/arms. 최종 도트 재작화의 디자인 참고로 사용.
- 여캐 이전 도트 걷기 `exec-e041da90-a227-4883-be57-846b792b909d.png`는 사용자 지정 원본 단독 변환으로 교체. 프롬프트: female-final-walk.
- 여캐 최초 회화풍 걷기 `exec-4369712f-2e36-434c-a1ca-0fbc893e8894.png`는 미채택. 프로젝트의 임시 FemaleWalkDraft 파일은 제거했다.
- 남캐 발 교대 보정 `exec-60d82c95-3d45-49bb-8810-40c854cfd46e.png`, 4키 포즈 `exec-c1fc2463-3dde-4c4b-a573-eb157d4737df.png`는 미채택. male-walk-correction / male-walk-keys 프롬프트를 보관한다. 현재 8장 사이클의 발 구분은 추가 다듬기 대상이다.

최종 조립·검증 및 남은 범위: [캐릭터 애니메이션](../CHARACTER_ANIMATION.md).
