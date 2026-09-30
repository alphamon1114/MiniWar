# 부위별 캐릭터 이미지 제작 기록

2026-09-28. 내장 `image_gen` 사용. 기존 `GunnerMale.png`, `GunnerFemale.png`는 정체성/의상 참고 이미지이며 보존했다. 출력은 `Assets/Resources/Online/GunnerParts.png`에 복사했다. 원본 1536×1024 RGBA, 배경 샘플 알파 0 확인. Unity가 원본 그림을 스프라이트 24개로 분리한다.

## 최종 보정 프롬프트

Edit target: attached modular character parts atlas. Keep EXACT same 1536x1024 canvas, 6 columns by 4 rows, EXACTLY the same part in every existing cell, same adult male/female identity, same pixel art style, same clothes and colors. TRUE TRANSPARENT alpha background: keep all gutters fully alpha 0. This is an animation rig atlas; make these targeted production corrections:
1) ROW 1 COLUMN 2 (male torso) and ROW 3 COLUMN 2 (female torso): REMOVE both attached shoulder pads/sleeves/arms completely. Retain only sleeveless torso vest, collar, chest, waist. Side shoulder sockets are clean rounded edges. ALL arm and shoulder-pad artwork belongs exclusively to arm cells. The separate upper-arm sprites already contain shoulders and must not be drawn twice.
2) ROW 1 COLUMN 1 male head: keep head, hair and a small neck ONLY. Remove chest, shoulders, large jacket collar/tie from this head sprite. Female head similarly head/hair/beret and small neck only.
3) ROW 1 COLUMN 3 and ROW 3 COLUMN 3 (pelvis): draw only a short belt-and-trouser hip piece. Remove hanging coat panels and long fabric from pelvis. 
4) FEMALE ROW 4 COLUMN 2 and COLUMN 4: draw separate plain GREY TROUSER THIGHS from hip to knee, no coat/skirt fabric. ROW 4 COLUMN 3 and COLUMN 5: grey trouser SHINS from knee to ankle plus black boot facing right, NO long coat fabric. All female coat fabric must occur ONLY in ROW 4 COLUMN 6 coat-tails sprite. Male thighs and lower-leg boots remain same.
5) Make each upper-arm piece truly shoulder-to-elbow and each forearm piece elbow-to-empty-gloved-hand, straight down, separate. Male black gloves, female white gloves.
6) Clean isolated alpha specks and stray pixels. Parts must have FULLY CLEAR TRANSPARENT margins at least 12 pixels from every cell boundary and not overlap neighboring cells.
All 24 cells remain occupied, one clothing body-part per cell. All joint ends are covered cloth with natural round overlap, not gore. No weapons, no labels, no checkerboard/background. Do not redraw assembled characters. Do not change layout or part order.

## 초기 생성 프롬프트

Use case: identity-preserve. Production Unity 2D cutout character rig sprite atlas, NEW sibling asset based on supplied reference sheets. Image 1 is male identity/costume reference. Image 2 is female identity/costume reference. Preserve their adult faces, detailed fantasy pixel-art rendering and original costumes exactly: male brown hair, navy/teal royal gunner coat with gold detailing, brown trousers/boots, black gloves; female blonde bob and braid, black beret with gold trim, black/gold military tunic and red details, gray trousers, black boots, white gloves. This is NOT a full-body pose sheet. Draw SEPARATE DISCONNECTED COSTUME BODY PARTS for skeletal puppet animation, with hidden joint areas filled in naturally, rounded covered joint overlaps. No gore, these are game costume cutouts.
ONE atlas with TRUE TRANSPARENT ALPHA background. Landscape 3072x2048 if possible, EXACTLY 6 equal-width COLUMNS and 4 equal-height ROWS, 24 cells, exactly one isolated part per cell, generous clear transparent gutters, NO grid lines, text or labels. Every part must fit completely inside its cell. Consistent right-facing 3/4 side view for all parts. Arms and legs are drawn straight hanging vertically DOWN from their top joint, never pre-bent at elbows or knees. Empty gripping hands, NO weapon. Each piece uses the same detailed pixel-art style. Keep proportions anatomically compatible, no dramatic perspective/foreshortening.
Male parts occupy TOP TWO rows. Female same parts occupy BOTTOM TWO rows. Layout is strictly this:
ROW 1 male, left to right: 1 HEAD incl hair, neck and no shoulders; 2 TORSO from shoulders/collar to waist, armless/headless/legless, no coat tails; 3 PELVIS with belt and upper trouser hip section only; 4 FRONT UPPER ARM shoulder-to-elbow straight down; 5 FRONT FOREARM elbow-to-wrist INCLUDING closed gloved empty hand, straight down; 6 REAR UPPER ARM shoulder-to-elbow straight down.
ROW 2 male, left to right: 1 REAR FOREARM INCLUDING empty gloved hand, straight down; 2 FRONT THIGH hip-to-knee straight down; 3 FRONT LOWER LEG knee-to-foot INCLUDING boot with toe pointing right; 4 REAR THIGH hip-to-knee straight down; 5 REAR LOWER LEG INCLUDING boot toe right; 6 COAT TAILS lower hanging fabric only, from belt down, no legs/pelvis/torso.
ROW 3 female uses EXACT same categories and column order as ROW 1.
ROW 4 female uses EXACT same categories and column order as ROW 2.
Important TORSO has no arm stubs beyond covered shoulder sockets and no hands. Do not repeat whole characters. No assembled figure, no duplicate head, no weapons or ground shadows, no backdrop, no checkerboard. All 24 costume pieces isolated with alpha 0 around them. Must be usable as a modular 2D animation atlas. Preserve original character identities and clothes, not the simplified colored stick-figure style.
# 2026-09-28 접합부 수정

내장 image_gen 편집으로 생성. 원본 분리 시트는 보존하고 실제 리그는 `Assets/Resources/Online/GunnerPartsV2.png`를 사용한다.

생성 원본: `C:/Users/mbc/.codex/generated_images/01a0cd05-2b12-7731-9a52-2f4f468fe937/exec-23923334-8186-4fbd-af30-6a93e8b4717a.png`

```text
Use case: precise-object-edit.
Edit target: this existing 1536x1024 transparent 6-column by 4-row cutout character sprite atlas for Unity.
Make only the following production rigging corrections, preserving both character identities, all 24 parts, placement and size, original detail, costume design, colors and crisp edges:
1. Male torso in row 1 column 2 and female torso in row 3 column 2: remove the empty black oval shoulder socket and its outline/rim completely. Cover it with continuous opaque navy fabric for the man, charcoal fabric for the woman, seamlessly following the torso shading. The torso should be a CLOSED SOLID armless trunk, with no hole, no oval badge, no ring, no visible arm opening. The separate upper-arm sprite will overlap this area.
2. Both male and female separate upper-arm pieces (columns 4 and 6 of rows 1 and 3): make each a SINGLE upper arm with rounded closed shoulder cap and a simple plain fabric lower end for overlap at the elbow. Remove hollow tube ends and the elaborate metal/embroidered wrist cuff at the elbow. Preserve shoulder insignia. Keep upper-arm shape and length. The forearm pieces retain their existing decorated cuffs and gloves.
3. Female front-thigh piece (row 4 column 2) should contain ONE single thigh only, matching the single rear-thigh piece in row 4 column 4, not two joined legs.
Keep heads, forearms with gloves, boots, pelvis pieces and coat tails unchanged. All arms and legs still point down, toes point right. Keep 24 isolated alpha islands in the same 6x4 grid with transparent gutters, no neighboring parts touching. TRUE transparent background alpha=0, no background gradient, no checkerboard drawn into pixels, no text or labels. Do not assemble the figure.
```
