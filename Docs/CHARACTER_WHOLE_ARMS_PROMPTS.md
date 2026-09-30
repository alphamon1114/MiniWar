# 양팔 통짜 원화

최종 보관 출력: `Assets/Resources/Online/GunnerMaleArmsV1.png`, `GunnerFemaleArmsV1.png`, `GunnerArmsDownV1.png`. 여성 원본 `exec-233775ab-04c7-4b15-9fbd-48405ee3d03b.png`, 아래 자세 원본 `exec-14321694-e731-4a6c-b9bf-9e81d00269ab.png`.

V2 correction 시도 `exec-0d0464da-af45-4a4a-8e4f-2ead367c7964.png`는 대기 자세의 한 팔이 사라져 채택하지 않았다. 아래 자세는 별도 시트로 교체했다. V1의 부드러운 화풍은 사용자가 선호하지 않았으며, 후속 도트 스타일 시험은 `CHARACTER_PIXEL_STYLE_PROMPT.md`에 기록한다.

## Down pose override

```text
Use case: stylized-concept
Create a small transparent sprite atlas with exactly TWO COLUMNS and TWO ROWS, four poses. Reference 1 shows male navy/gold sleeves with black gloves; reference 2 female black/gold/red sleeves with white gloves. Match these costumes, pixel shading, and slim adult arm anatomy.
All FOUR poses aim an invisible gun STRAIGHT DOWN, toward the bottom of the canvas. Top left male long-gun hold, top right male pistol hold. Bottom left female long-gun hold, bottom right female pistol hold.
Each pose is ONE whole paired-arm sprite: a continuous visible near arm from rounded shoulder cap, down through its natural elbow, to a glove. The far supporting hand is drawn touching or immediately behind the near hand, with just its short cuff visible. The far upper arm is COMPLETELY HIDDEN by the imagined torso and near arm: DO NOT DRAW a second shoulder cap, a second detached sleeve stump, or an exposed far upper arm. This is a side-view game overlay, not an anatomical diagram. Exactly ONE visible shoulder cap in each of the four cells. Hands angle down naturally, not a pointing finger gesture. Near upper arm and forearm have natural comparable lengths. No stumpy arms.
No guns, torso, neck, head, legs or background. Same scale, margins, and near shoulder height in all four cells; at least 50 pixels of padding. True transparent alpha. No labels or grid. Arms point down in all four cells, while the character is viewed from the same right-facing 3/4 side view as the references.
```

## V2 correction (남녀 동일 지시)

```text
Use case: precise-object-edit
Edit only the supplied transparent 3-column by 4-row paired-arm sprite atlas. Preserve exact image dimensions, pose order, character scale, near shoulder pivot in every cell, colors, lighting, anatomy, and all unaffected cells.
Make TWO narrowly targeted corrections:
1. In row 2 column 2 (long-gun straight DOWN) and row 4 column 2 (pistol straight DOWN), the FAR upper arm currently starts too far to the RIGHT of the near shoulder. Move/repaint the FAR upper arm's rounded shoulder cap so it starts only about 55 pixels to the right of the near shoulder (not about 110 pixels), at approximately the SAME HEIGHT as the near shoulder. Draw that far arm naturally down to the existing supporting glove. Keep the near arm and both glove positions fixed. This far shoulder must fit on a human chest rather than floating outside it.
2. Row 2 column 3, relaxed rifle carry: raise the FAR supporting glove and its forearm by about 35 pixels, keeping the near shoulder and near trigger-hand position fixed. The supporting glove should sit above the trigger glove so it supports the underside of a rifle barrel. Repaint that forearm naturally into the existing pose; no disconnected pieces.
No weapons, no body/torso/head, no background, no text or guides. True transparent alpha. Do not shift or redraw the other ten poses. Do not resize or rearrange cells. Both arms are a single drawn pose with continuous sleeves and natural elbows.
```

남성 출력: `Assets/Resources/Online/GunnerMaleArmsV1.png` (원본 `exec-2246602b-7a4e-43ac-867d-81f3db8eca07.png`).

## Female V1

입력: 남성 통짜 팔 V1 (편집 대상), `Assets/Resources/Online/GunnerFemaleBodyV2.png` (의상 기준).

```text
Use case: precise-object-edit
Edit target: Image 1, the twelve-pose arm-pair atlas. Reference image 2 is the female body and costume to match.
Change ONLY the male navy sleeves/blue cuffs/black gloves into the female character's fitted black sleeves, restrained gold piping, black cuffs with gold edges and small red inserts, and white gloves. Keep the exact twelve poses, layout, shoulder attachment locations, comparable natural upper-arm and forearm lengths, and near/far arm relationships. Use slightly slimmer sleeves appropriate for the female body but no short arms. Match the painterly pixel-art shading and palette of reference 2.
Preserve the 3 columns x 4 rows on transparent background. Each cell is one complete arm-pair pose, no segmentation at elbow or wrist. Rows 1-2 hold imaginary long guns (up, up-right, right / down-right, down, waist-level right). Rows 3-4 hold imaginary pistols with both white gloves touching (up, up-right, right / down-right, down, ready by cheek pointing up).
No body, head, neck, legs, actual gun or weapon, labels, grid, background, detached floating gloves, extra poses or accessories. TRUE transparent alpha. Preserve the exact atlas layout and margins.
```

2026-09-28. 내장 image_gen 도구 사용. 몸체 한 장 + 양팔 한 장 + 교체형 무기 구조.

## Male V1

입력: `Assets/Resources/Online/GunnerMaleBodyV3.png`, `Docs/References/GunnerMale-proportions-reference.png`.

```text
Use case: stylized-concept
Asset type: transparent sprite atlas of WHOLE ARM PAIRS for an existing 2D side-view fantasy gunner.
Input image 1 is the existing armless male body sprite sheet: match its navy cloth, subdued gold trim, painterly pixel shading and adult proportions. Input image 2 is a style/anatomy reference of the same man holding a rifle: match the natural length and slenderness of his arms and black gloves.
Create ONLY detachable visible ARM PAIRS, no person or torso. Each cell contains one fully drawn pose with the near arm continuous from rounded shoulder cap through elbow and wrist into glove. The far forearm and supporting glove are part of the SAME pose image; the far upper arm is mostly occluded by the imagined torso. NO separated upper-arm/forearm pieces, NO joint circles. Upper arm and forearm have comparable adult lengths. Slim fitted sleeves; tasteful small gold piping and navy cuffs. View is a consistent right-facing 3/4 side view matching reference.
Layout: exactly 3 columns by 4 rows, twelve equal square cells, total canvas 1536x2048. Each complete pair fits inside its cell with at least 40 pixels of transparent padding on all sides. Same character scale throughout. No text, labels, guide lines, grid, drop shadow, or background. True transparent alpha.
Row 1, left to right: two hands spaced for a long gun aimed straight UP (+90), diagonally UP-RIGHT (+45), straight RIGHT (0).
Row 2: same long-gun hold aimed DOWN-RIGHT (-45), straight DOWN (-90), relaxed rifle carry at WAIST height pointing RIGHT (0), elbows hanging naturally.
Row 3: compact TWO-HANDED PISTOL grip, both gloves touching each other, aimed UP (+90), UP-RIGHT (+45), RIGHT (0).
Row 4: two-handed pistol grip aimed DOWN-RIGHT (-45), DOWN (-90), pistol ready pose with elbows bent and gloved hands raised beside the imagined cheek, gun would point UP.
Crucial: do NOT draw any guns, weapons, weapon silhouettes, rods, head, neck, chest, abdomen, legs, scarf, or coat tails. Hands curl around an invisible gun. In long-gun poses the trigger hand and supporting palm are separated along the invisible barrel axis, with room for an interchangeable gun sprite behind the gloves. In pistol poses both hands clasp the SAME invisible grip. The shoulder cut edges are round, solid navy, and designed to overlap the existing torso seamlessly. Both forearms are connected naturally to upper arms, no floating hands. Crisp pixel-art edges and controlled limited shading, no glossy 3D look.
```
