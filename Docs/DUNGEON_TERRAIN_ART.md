# 던전 바닥 아트 · 2026-10-01

- 생성 방식: 내장 image_gen 도구.
- 최종 파일: `Assets/Resources/Online/Terrain/KingdomStoneAtlas.png`.
- 윗면은 포장석 길, 아래는 정면 벽돌 단면. Unity에서 영역을 분리해 반복 렌더링한다.
- 색감은 저채도 회갈색 석재와 약한 이끼. Point 필터, 밉맵 없음.
- 커스텀 스프라이트를 지정한 바닥은 기존 그림을 유지한다. 기본 바닥에만 공통 재질을 적용한다.
- 충돌 영역은 원래 바닥 사각형을 그대로 사용한다.

## 최종 생성 프롬프트

Use case: stylized-concept
Asset type: production 2D side-scrolling fantasy pixel-art terrain ATLAS, exactly two equal horizontal bands stacked edge-to-edge, square canvas, no margins, no text.
Primary request: a stone-paved castle-town walkway TOP surface in the UPPER HALF, and its stacked stone-brick retaining wall SIDE surface in the LOWER HALF. Used as two separate horizontally tiled sprites. Inspired by older Korean side-scrolling fantasy action RPG town platforms. Restrained charming illustrated pixel art, clearly readable chunky pixel clusters, not photoreal, not shiny 3D, not overly cute. Medieval kingdom stone masonry. Muted warm limestone grey and dusty beige, dark desaturated moss green, sparse weathering. Not blue blocks, not bright yellow.
UPPER HALF spans exactly x 0-100%, y 0-50%: shallow oblique TOP view of a walkable stone-paved road strip. Flat slab pavers staggered in 3-4 perspective rows across this band, stone paving covers every pixel, the bottom edge is a narrow continuous bevel/rim to meet the retaining wall. Top band is a rectangle all the way to edges, with clean straight upper and lower boundaries. Subtle moss in cracks only, no grass tufts protruding, no props. Reads as horizontal floor top when vertically compressed into a narrow road strip. About 8 slabs across width.
LOWER HALF spans exactly x 0-100%, y 50-100%: perfectly frontal orthographic SIDE view of a substantial stone-brick wall, about 5 irregular horizontal courses of rectangular large dressed stone blocks. Dark narrow mortar joints, staggered brick courses, approximately 5 bricks across each row. Soft upper edge highlights and darker lower edges on blocks. All rectangular, slight wear, occasional sparse muted moss. Covers every pixel in this half, no gaps or background. No arch, no window, no columns.
CRITICAL technical composition: hard straight divide at EXACT middle y=50%, completely fill the canvas, upper half is top-view road material and lower half is frontal wall material. Both halves must tile seamlessly horizontally independently; left/right ends continue the same pattern. Lower wall should also repeat vertically reasonably. Same material palette for both. Opaque texture atlas, no frames, no shadows outside the canvas, no checkerboard, no labels, no objects, no buildings, no background scene. Crisp old fantasy RPG pixel-art clusters, medium detail for small game-scale tiles.
