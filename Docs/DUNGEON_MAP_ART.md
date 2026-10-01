# 던전 선택 지도

- 제작: 내장 image_gen, 최종 편집 모드. 생성된 지도를 참조해 색과 형태를 다시 그렸다.
- 최종 에셋: `Assets/Resources/Online/DungeonCampaignCasual.png`
- 원본 생성 결과: `exec-525cf62b-1a95-483a-adfa-279a9dd76598.png`
- 구도: 사용자가 그린 성문 → 중앙 시장 → 우측 상단 왕궁 경로, 양쪽 거주지.
- 톤: 단순한 지도 구도를 유지하되 회청색 성벽, 탁한 벽돌색 지붕, 짙은 녹색 식생과 점령 흔적.
- 던전 이름·선택·준비 중 표시는 그림이 아니라 게임 UI로 표시한다.

## 최종 프롬프트

```text
Use case: style-transfer
Asset type: game dungeon selection map background.
Edit target: the provided bright cartoon kingdom map. Preserve the EXACT city composition, landmark positions, road shapes, gate at lower left, market center, castle upper right, crystal temple middle right, portal lower right, residential clusters, camera framing and 3:2 aspect ratio. No text or UI.
User correction: this is TOO CASUAL and MUCH TOO BRIGHT. Redraw the whole art direction to a middle ground: restrained illustrated old-school 2D Korean fantasy RPG city map. Keep large readable shapes and modest detail, but use more angular mature architecture and less toy-like silhouettes, hand-painted slightly rough outlines and subtle brush shading. Not a photoreal or highly detailed cinematic fantasy painting; not a mobile city builder cartoon.
Overcast late afternoon in an occupied war-damaged kingdom. Reduce the palette saturation substantially. Replace neon lime lawns with muted moss and dusty olive. Walls become cool slate-grey weathered stone, not cream yellow. Roofs become muted dark rusty red, castle roofs weathered dark blue. Roads become desaturated grey-brown worn cobblestone. Water becomes muted dark teal. Forest clusters darker blue-green. Lighting is soft and even, slightly somber. Keep all landmarks clearly visible at game UI size; do not crush shadows or make a night map.
Add modest visible damage to the gate, a handful of broken market awnings and subtle smoky haze near buildings, torn blue flags. Buildings proportions a little less squat/cute. Trees a little less rounded bubble-like and more irregular. Violet crystal and portal have a modest small glow, not neon dominating the canvas. This is a ruined-but-recognizable kingdom to retake, not a cheerful holiday town.
CRITICAL: retain simple map readability and current geographic arrangement. No extra landmarks, no landscape expansion, no mountains, no intricate microdetail, no labels or wording, no excessive texture noise.
```
