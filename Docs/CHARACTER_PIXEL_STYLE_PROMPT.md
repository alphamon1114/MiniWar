# 도트 화풍 시험 V1

2026-09-28: 이 샘플을 기준으로 남녀 대기·걷기·점프·5방향 팔과 장비를 게임에 연결했다. 현재 원화 및 최종 프롬프트는 [PixelMotionV1 기록](Art/PixelMotionV1.md)을 따른다. 아래는 최초 남캐 대기 샘플의 제작 기록이다.

보관 출력: `Assets/Art/Studies/PixelGunner/GunnerMalePixelV1.png` (1254×1254 RGBA). 최종 생성 원본 `exec-9aed956a-c54f-43c3-be20-f7f8bdef2a1e.png`. 이전 스타일 초안은 `exec-39076d8f-d101-4228-8d3b-aa7cb11532fe.png`.

`PixelGunnerStudy.cs`가 몸체/양팔/소총 3개의 스프라이트로 가져와 편집 가능한 `GunnerMalePixelStudy.prefab`을 조립한다. 결과 `Logs/QA/GunnerPixelStudy.png`는 Unity 렌더링이다. 생성기에 요구한 정확한 픽셀 격자/색상 수는 보장된 측정치가 아니다.

## 팔 중복 제거 수정

```text
Use case: precise-object-edit
Edit this pixel-art sprite construction sheet with ONE correction only: remove the extra attached navy UPPER-ARM/SLEEVE STUB on the LEFT side of the full-body character (the sleeve with the gold shoulder emblem, hanging from shoulder to waist). The full-body asset must be genuinely ARMLESS, with a clean narrow coat/torso silhouette from shoulder to waist, because the separate arm overlay will supply that entire arm. Keep the torso shoulder attachment area filled with navy coat fabric; no hole.
Keep EVERYTHING else unchanged: exact character face, adult proportions, long slim legs, coat, boots, scarf, colors, hard pixel edges, the separate whole arm-pair overlay at upper right, the separate rifle at lower right, their positions and scales, transparent alpha background and square canvas. Do not add detail, antialiasing, soft shading, labels, shadows, new poses or new parts. Preserve the crisp blocky pixel-art rendering.
```

내장 image_gen 도구로 제작. 참조: `Docs/References/GunnerMale-proportions-reference.png`.

몸체 한 장 + 양팔 한 장 + 별도 무기의 구성을 유지하는 남성 소총 대기 스타일 시험. 전체 동작 시트 전환 완료를 뜻하지 않는다.

```text
Use case: style-transfer
Asset type: genuine low-resolution pixel-art game sprite parts, not a painted illustration.
Input reference: use the brown-haired adult male gunner's identity, slim adult proportions, navy long coat, blue scarf, gold trim, dark trousers and brown boots. CHANGE THE RENDERING STYLE COMPLETELY to crisp hand-placed pixel art.
Output one SQUARE transparent sprite construction sheet with THREE separated assets, all at the SAME pixel scale:
LEFT HALF: one full-height armless male body, head/neck/torso/coat/legs/boots all connected in one sprite. Relaxed standing, facing right in 3/4 side view. NO arms or gun on this body. Natural adult proportions approximately 7 heads tall, small head, long slim legs, modest boots.
UPPER RIGHT: one whole visible arm-pair sprite for relaxed horizontal rifle carry. Continuous navy shoulder, upper arm, bent elbow, forearm, black gloved trigger hand; farther supporting forearm and hand drawn together in this same overlay. Foreground upper arm hangs naturally down, foreground forearm forward at waist height. Supporting glove is 4 logical pixels higher than trigger glove. Only one prominent shoulder cap. NO torso or head or gun in the arm overlay.
LOWER RIGHT: one separate matching pixel-art rifle pointing right, dark metal and brown wood, simple gold accents, transparent surrounding area.
CRITICAL PIXEL RULES: design on a logical 128x128 pixel canvas and show it at exactly 8x nearest-neighbor enlargement (1024x1024 output). Every visible edge, highlight, eye and garment detail must be made of crisp SQUARE 8x8-pixel blocks on that same grid. The body is approximately 108 logical pixels tall. Use a restrained palette of at most 28 colors total, at most 3 shades per material, a dark 1-logical-pixel outline, deliberate angular clusters, strong readable silhouette. Flat solid-color pixels only. No antialiasing, gradients, airbrush, soft shadows, fine embroidery, texture noise, smooth curves or photographic detail. Face is a small cluster of pixels, NOT a detailed painted face. No subpixel marks anywhere. Small character from a classic fantasy side-scrolling arcade action game, not chibi. Keep all parts inside the canvas with generous transparent gutters. True alpha transparency, no checkerboard pixels, no text, no labels, no grid lines, no background.
```
