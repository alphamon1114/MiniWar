# Connected body animation atlases

Generated with the built-in image generation tool on 2026-09-28. These are AI-generated frame sprites, with procedural arm overlays in Unity.

Male source: `Assets/Resources/Online/GunnerMale.png`
Female source: `Assets/Resources/Online/GunnerFemale.png`
Drafts: `Assets/Resources/Online/GunnerMaleBodyV1.png`, `GunnerFemaleBodyV1.png`.
Final: `Assets/Resources/Online/GunnerMaleBodyV3.png`, `GunnerFemaleBodyV2.png`.
Male V3 source output: `exec-675c448a-95dc-44e1-b24b-2ff7d81ca64d.png`. The user subsequently supplied the slimmer reference now preserved at `Docs/References/GunnerMale-proportions-reference.png`. Arm length/width and low rifle ready pose were adjusted against that reference in Unity.

## Male slimmer legs correction

Use case: precise-object-edit.
Edit this MALE 4x4 connected-body animation sprite atlas. The user says his legs became too thick and bulky. Correct the male anatomy in all sixteen frames into a SLENDER adult fantasy gunner with long elegant legs.
PRIMARY CHANGE: reduce the WIDTH of each thigh, knee, calf and boot shaft by approximately 28-30 percent. Slim fitted dark trousers, narrower knees, slim tall brown boots, clearly tapered ankles. Boots must not look chunky or oversized; shorten the foot silhouette about 15 percent while retaining the brown leather and gold fittings.
PROPORTIONS: lengthen the legs relative to the torso slightly, about 8 percent, by moving the waist/belt upward a little while keeping overall standing height and the already SMALL HEAD the same. Aim for an adult seven-head figure. No dwarf, stocky body, bodybuilder thighs, giant shoes, wide calves or balloon trousers.
PRESERVE: the same male face and small head, brown hair, navy/gold military coat, blue scarf, teal lining, costume details, all 16 existing idle/walk/jump poses and their sequence, pose directions, right-facing side/three-quarter view, ARMLESS continuous cloth-covered torso, no separate head/waist/leg gaps. Do not add arms, hands or guns. No exposed shoulder skin.
TECHNICAL: true transparent RGBA background, clean crisp pixel-style contours. EXACT 4 columns x 4 rows. Every full figure completely inside its own equal cell with transparent gutter, especially the last column's boots must be fully visible. Same scale in every frame; standing feet consistent baseline. Keep proportions consistent throughout the walking and jumping frames. No text, grid lines, shadow, or additional objects. Return only the corrected atlas.
Selected built-in outputs: male `exec-ec5999b6-f92c-446f-9edc-87e3cc2322da.png`, female `exec-1831c9a0-dace-4c79-96aa-3d9be75433fc.png`, copied into the project. The male closed-shoulder correction used intermediate `exec-89512f22-1f6d-46a7-8a10-b098a0fc8d27.png`. These outputs originated in `C:/Users/mbc/.codex/generated_images/01a0cd05-2b12-7731-9a52-2f4f468fe937/`.

## Male proportion/sleeve correction

Use case: precise-object-edit. Edit this male 4x4 sprite atlas for a Unity hybrid rig. Preserve exactly the transparent background, 4x4 layout, each body/leg walking/jump pose, pixel style, costume colors, belts, scarf, coat tails, feet positions and size of all torsos/legs.
TWO SURGICAL CORRECTIONS in EVERY ONE of the 16 cells:
1. REMOVE the baked upper-arm/sleeve: the large navy shoulder with golden circular sun emblem on the LEFT side of each torso and the bulging sleeve going down toward waist MUST GO. Both arms will be separate procedural overlays. Paint ONLY a slim continuous sleeveless torso there: plain navy fitted waistcoat from collar to side seam. No sleeve caps, upper arms, arm holes, gold shoulder insignia, hands, or weapons. The torso has its own continuous left silhouette. This must visibly remove the large round sleeve mass, not just erase its emblem.
2. Make the brown-haired head (entire hair/face/neck unit) 35 percent SMALLER while its neck remains seamlessly joined to the collar. Keep body and legs unchanged. This fixes a too-large head: adult seven-head heroic proportions, small adult face, NOT child/chibi. Keep face identity and hairstyle. Do not stretch limbs.
No text, guide lines, borders or shadows. True alpha transparency. Output the corrected 4x4 sheet only.

## Female proportion/sleeve correction

Use case: precise-object-edit. Edit this female 4x4 sprite atlas for a Unity hybrid rig. Preserve exactly the transparent background, 4x4 layout, each body/leg walking/jump pose, pixel style, costume colors, belts, red collar, coat tails, feet positions and size of all torsos/legs.
TWO SURGICAL CORRECTIONS in EVERY ONE of the 16 cells:
1. REMOVE the baked upper-arm/sleeve: the large black-and-gold short sleeve on the LEFT side of each torso and the bulging sleeve going down toward waist MUST GO. Both arms will be separate procedural overlays. Paint ONLY a slim continuous fully CLOTH-COVERED torso (NO BARE SKIN, no sleeveless cutouts, no exposed shoulders: fill the former arm attachment area with dark fabric) there: plain black fitted uniform from collar to side seam. No sleeve caps, upper arms, arm holes, gold shoulder insignia, hands, or weapons. The torso has its own continuous left silhouette. This must visibly remove the large round sleeve mass, not just erase its emblem.
2. Make the blonde-haired head (entire hair/face/neck unit) 40 percent SMALLER while its neck remains seamlessly joined to the collar. Keep body and legs unchanged. This fixes a too-large head: adult seven-head heroic proportions, small adult face, NOT child/chibi. Keep face identity, hairstyle and black gold-trimmed beret, all scaled down together. Do not stretch limbs.
No text, guide lines, borders or shadows. True alpha transparency. Output the corrected 4x4 sheet only.

## Male closed shoulder correction

Use case: precise-object-edit. Correct this 4x4 male gunner body-layer sprite atlas. In every cell there is a bare peach-colored oval shoulder next to the navy high collar. Replace ONLY that bare shoulder skin patch with smooth navy blue uniform cloth seamlessly matching the adjacent torso, no skin, no hole, no sleeve, no gold arm insignia. It must be a continuous closed cloth-covered armless torso silhouette where a separate arm sprite can overlay it. Keep exactly all 16 poses, every small head, faces, hair, adult proportions, boots, belts, coats, scarves, cell positions, transparent alpha background and dimensions unchanged. No new arms or hands. No other changes.


## Male prompt

Use case: identity-preserve.
Asset type: game production sprite atlas for Unity 2D, transparent RGBA.
Input image is the MALE character identity reference only. Create a NEW animation sheet of this same brown-haired navy-and-gold fantasy military gunner, facing RIGHT in side/three-quarter view.
CRITICAL: This is the BODY LAYER for a hybrid animation rig. Every pose must have connected head, neck, torso, hips, coat tails, both legs and boots, but NO ARMS, NO HANDS, NO GUNS. The shoulder areas are smooth closed navy uniform fabric ready for separate arm overlays, never hollow sockets. No detached body parts.
Adult heroic proportions approximately SEVEN HEADS tall, slender long legs, small head. Preserve brown spiky hair, blue scarf, gold embroidered navy coat with teal lining, dark pants and brown tall boots.
Canvas 1536x2048. EXACTLY 4 columns by 4 rows of equal 384x512 cells, no lines, labels or text. Sixteen full body poses, isolated transparent background. Each body about 400 px tall standing, pelvis centered horizontally at each cell center, feet baseline 470 px down each cell; same scale in all frames, generous transparent gutters. No shadows.
Row 1: four subtle idle breathing frames, same grounded stance.
Rows 2 and 3 together: eight genuinely different sequential walking frames: right foot forward contact, weight down, passing, lift, left foot forward contact, weight down, passing, lift. Head and trunk almost stationary while legs alternate, subtle coat follow-through. Boots touch baseline except lifted boot.
Row 4: four jump poses: bent-knee anticipation, rising with one knee lifted, apex tucked knees, descending legs reaching down. Keep pelvis/root registration consistent with standing body; jump world translation handled by game.
Sharp detailed fantasy pixel-art, crisp dark contour, no blurry painterly shading. Consistent face/costume/size throughout. Do not include reference original sheet or props. This is usable frame animation art, not an infographic.

## Female prompt

Use case: identity-preserve.
Asset type: game production sprite atlas for Unity 2D, transparent RGBA.
Input image is the FEMALE character identity reference only. Create a NEW animation sheet of this same blonde black-and-gold fantasy military gunner, facing RIGHT in side/three-quarter view.
CRITICAL: This is the BODY LAYER for a hybrid animation rig. Every pose must have connected head, neck, torso, hips, coat tails, both legs and boots, but NO ARMS, NO HANDS, NO GUNS. The shoulder areas are smooth closed black uniform fabric ready for separate arm overlays, never hollow sockets. No detached body parts.
Adult heroic proportions approximately SEVEN HEADS tall, slender long legs, small head. Preserve blonde bob with side fringe, black beret with gold trim, red high collar, black and gold military uniform, long gray/white trouser panels, black tall boots. Match the supplied female reference identity.
Canvas 1536x2048. EXACTLY 4 columns by 4 rows of equal 384x512 cells, no lines, labels or text. Sixteen full body poses, isolated transparent background. Each body about 400 px tall standing, pelvis centered horizontally at each cell center, feet baseline 470 px down each cell; same scale in all frames, generous transparent gutters. No shadows.
Row 1: four subtle idle breathing frames, same grounded stance.
Rows 2 and 3 together: eight genuinely different sequential walking frames: right foot forward contact, weight down, passing, lift, left foot forward contact, weight down, passing, lift. Head and trunk almost stationary while legs alternate, subtle coat follow-through. Boots touch baseline except lifted boot.
Row 4: four jump poses: bent-knee anticipation, rising with one knee lifted, apex tucked knees, descending legs reaching down. Keep pelvis/root registration consistent with standing body; jump world translation handled by game.
Sharp detailed fantasy pixel-art, crisp dark contour, no blurry painterly shading. Consistent face/costume/size throughout. Do not include reference original sheet or props. This is usable frame animation art, not an infographic.
