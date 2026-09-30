# 캐릭터 새 도트 기준안 V1 — 2026-09-30

사용자가 이전 가로 압축 비율이 너무 말랐다고 지적하여, 남성 맥(최강의군단/나이트워커), 여성 엘리어스(빅샷) 참고를 받아 새로 생성한 기본 전신 도트 기준안이다. 새 디자인은 남성의 비니·은발·갈색 재킷과 여성의 금발·베레모·검정/금색 군복을 따른다. 자연스러운 체형과 팔 길이, 몸에 붙은 팔을 우선한다.

결과: [MacElias-Concept.png](MacElias-Concept.png). 제공된 참고는 `Reference-Mac.webp`, `Reference-Elias.webp`에 보존했다. 내장 image_gen으로 생성했다. 사용자가 이 기준안을 승인한 뒤 대기·걷기·점프·5방향 조준 모션과 게임 프리팹, 캐릭터 선택 이미지를 새로 연결했다. [모션 구현 기록](Motions.md)과 [최종 프롬프트](FinalMotionPrompts.json)를 따른다. 코드로 몸을 압축한 이전 실험은 해제했다.

## 최종 생성 프롬프트

```text
Use case: stylized-concept / pixel-art style transfer.
Create ONE cohesive two-character full-body pixel-art design sheet for a Korean side-scrolling action RPG, freshly redrawn from the two supplied costume references. These are reference images, NOT old sprites to stretch or squeeze.
Image 1 reference: male Mac from HeroWarz/Night Walker. Image 2 reference: female Elias from Big Shot.
Canvas composition: two separate full-body sprites side by side, MALE LEFT, FEMALE RIGHT, equal visual scale, aligned boot soles. Plenty of space between them. Entire hats and boots visible. Plain solid very dark blue-gray background (#18202c), no scenery, no text, no logos, no UI, no shadow platform.
Each character faces right in a 3/4 side-view suitable for a side-scrolling game, face visible, natural relaxed combat-ready standing pose, both arms and hands fully attached and anatomically coherent. Arms hang naturally beside/just ahead of the torso. No gun covering the face. No weapons for this body-proportion study.
MALE: preserve the first reference's dark knit beanie with understated small ochre marks, silver-gray bangs, handsome calm young-adult face, brown leather field/bomber jacket over white shirt, dark shoulder straps and a few small functional pouches, dark charcoal fitted trousers with very restrained distressed patches, black lace-up combat boots, dark fingerless gloves. No blue fantasy coat. Fit athletic body, natural shoulder breadth, chest volume, visible elbow and forearm length. Medium-build, never skinny.
FEMALE: preserve the second reference's appealing blonde bob with a small side braid, teal eyes, black beret with a gold band, black/dark charcoal military coat with scarlet high collar and neat gold trim, warm brown utility belts, distinctive split long skirt/coat tails with white inner panels, white gloves and black boots. Keep her facial appeal, clean jawline and readable expression. Youthful adult, healthy moderate shoulder and thigh volume, graceful but not pencil-thin, no exaggerated bust.
PROPORTIONS: long-legged, elegant action-RPG silhouettes around 6.5 to 7 heads high, male only a little taller. Legs and arms drawn at their intended proportions from scratch. NORMAL HUMAN WIDTH and substantial upper arms beneath clothing; forearms gently taper to wrists and are slimmer than the upper arms. No tiny T-rex arms, no enormous sleeves, no skinny compressed torsos, no giant heads, no chibi proportions, no bodybuilder muscles, no stretched vertical anatomy.
PIXEL MEDIUM is essential: genuinely hand-clustered pixel art, visible coherent square pixels at an apparent native sprite height around 220 pixels, displayed at a clean integer enlargement. Strong 1-2 logical-pixel dark outlines, deliberate stair-step diagonals, restrained 4-5 tone ramps per material, sharp highlights, clean grouped shadows. Attractive detailed facial pixels while staying sprite-like. Comparable craft to classic Dungeon Fighter Online character sprites. NOT smooth painting with a pixel filter, NOT vector art, NOT 3D, no airbrush, no anti-aliased blur. Both characters MUST have the same pixel scale and shading technique.
```
