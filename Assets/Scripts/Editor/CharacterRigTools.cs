using System;
using System.IO;
using System.Linq;
using MiniWar.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MiniWar.EditorTools
{
    public static class CharacterRigTools
    {
        public const string AtlasPath = "Assets/Resources/Online/GunnerPartsV2.png";
        public const string RigFolder = "Assets/Resources/Online/Rigs";
        public static string PrefabPath(int body) => RigFolder + (body == 0 ? "/GunnerMale.prefab" : "/GunnerFemale.prefab");

        [MenuItem("MiniWar/Characters/Build part sprites and rigs")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before rebuilding character assets.");
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 4096;
            importer.alphaIsTransparency = true; importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = 100;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            var partRects = FindPartBounds(texture.GetPixels32(), texture.width, texture.height);
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(x => x.name);
            var rectangles = new SpriteRect[24];
            for (int body = 0; body < 2; body++)
            for (int part = 0; part < 12; part++)
            {
                var rect = partRects[body * 12 + part];
                if (rect.width < 10 || rect.height < 10) throw new InvalidDataException("Empty rig part: " + body + "/" + part);
                string name = (body == 0 ? "Male_" : "Female_") + CharacterRig.PartNames[part];
                Vector2 pivot = part == 0 ? new Vector2(.5f, .1f) : part == 1 ? new Vector2(.5f, .08f)
                    : part == 8 || part == 10 ? new Vector2(.38f, .92f) : new Vector2(.5f, .91f);
                rectangles[body * 12 + part] = new SpriteRect {
                    name = name, spriteID = previous.TryGetValue(name, out var old) ? old.spriteID : GUID.Generate(),
                    rect = rect, pivot = pivot, alignment = SpriteAlignment.Custom
                };
            }
            provider.SetSpriteRects(rectangles);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rectangles.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            Directory.CreateDirectory(RigFolder); AssetDatabase.Refresh();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToDictionary(x => x.name);
            PixelCharacterTools.BuildEquipment();
            var visuals = new OnlineVisuals();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                for (int body = 0; body < 2; body++)
                {
                    var go = new GameObject(body == 0 ? "GunnerMale" : "GunnerFemale");
                    SceneManager.MoveGameObjectToScene(go, preview);
                    var rig = CreateRig(go, body, CharacterRig.PartNames.Select(n => sprites[(body == 0 ? "Male_" : "Female_") + n]).ToArray());
                    rig.bodyFrames = RedrawCharacterTools.BuildBody(body);
                    rig.upperArmLength = .52f; rig.forearmLength = .48f; rig.useShoulderRelativePoses = true;
                    rig.connectedBody = Joint(rig.facingRoot, "ConnectedBody", Vector2.zero).gameObject.AddComponent<SpriteRenderer>();
                    rig.connectedBody.sortingOrder = 5;
                    for (int i = 0; i < rig.parts.Length; i++) rig.parts[i].enabled = false;
                    rig.frontUpperArm.SetParent(rig.facingRoot, false);
                    rig.backUpperArm.SetParent(rig.facingRoot, false);
                    rig.armFrames = RedrawCharacterTools.BuildArms(body);
                    rig.connectedArms = Joint(rig.facingRoot, "ConnectedArms", Vector2.zero).gameObject.AddComponent<SpriteRenderer>();
                    rig.connectedArms.sortingOrder = 8;
                    rig.weaponGrip.SetParent(rig.connectedArms.transform, false);
                    rig.SetWeapon(visuals.Weapon(0, 1), OnlineVisuals.WeaponWidths[0]);
                    rig.Pose(0, 0, false, 0, 1, false, 0, 0);
                    PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(body));
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            AssetDatabase.SaveAssets();
            Debug.Log("MiniWar two-piece characters ready: connected body + painted arm pair + separate equipment. Open MiniWar > Characters > Motion preview.");
        }

        static Transform Joint(Transform parent, string name, Vector2 position)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
        }

        // Generated art can cross an ideal grid boundary. Follow each connected alpha island first,
        // then assign it to its cell; this prevents cropped fingers, stray coat pixels, and padded neck gaps.
        internal static Rect[] FindPartBounds(Color32[] pixels, int width, int height, int columns = 6, int rows = 4)
        {
            var seen = new bool[pixels.Length];
            var queue = new System.Collections.Generic.Queue<int>();
            var result = new Rect[columns * rows]; var largest = new int[columns * rows];
            void Visit(int index)
            {
                if (seen[index] || pixels[index].a <= 32) return;
                seen[index] = true; queue.Enqueue(index);
            }
            for (int index = 0; index < pixels.Length; index++)
            {
                if (seen[index] || pixels[index].a <= 32) continue;
                Visit(index);
                int left = width, right = 0, bottom = height, top = 0, area = 0;
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue(), x = current % width, y = current / width;
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); area++;
                    if (x > 0) Visit(current - 1); if (x + 1 < width) Visit(current + 1);
                    if (y > 0) Visit(current - width); if (y + 1 < height) Visit(current + width);
                }
                int col = Mathf.Clamp(((left + right) / 2) * columns / width, 0, columns - 1);
                int row = rows - 1 - Mathf.Clamp(((bottom + top) / 2) * rows / height, 0, rows - 1);
                int slot = row * columns + col;
                if (area <= largest[slot]) continue;
                largest[slot] = area;
                result[slot] = new Rect(left, bottom, right - left + 1, top - bottom + 1);
            }
            return result;
        }

        static CharacterRig CreateRig(GameObject root, int body, Sprite[] sprites)
        {
            root.AddComponent<SortingGroup>().sortingOrder = 3;
            var rig = root.AddComponent<CharacterRig>(); rig.body = body;
            rig.facingRoot = Joint(root.transform, "Facing", Vector2.zero);
            rig.pelvis = Joint(rig.facingRoot, "Pelvis", new Vector2(0, CharacterRig.StandingHipHeight));
            rig.torso = Joint(rig.pelvis, "Torso", new Vector2(0, .05f));
            rig.head = Joint(rig.torso, "Head", new Vector2(.03f, body == 0 ? .63f : .60f));
            rig.coat = Joint(rig.pelvis, "CoatTails", new Vector2(-.04f, .06f));
            rig.frontUpperArm = Joint(rig.torso, "FrontUpperArm", new Vector2(-.19f, .51f));
            rig.frontForearm = Joint(rig.frontUpperArm, "FrontForearm", Vector2.down * CharacterRig.UpperArmLength);
            rig.backUpperArm = Joint(rig.torso, "BackUpperArm", new Vector2(.12f, .56f));
            rig.backForearm = Joint(rig.backUpperArm, "BackForearm", Vector2.down * CharacterRig.UpperArmLength);
            rig.frontThigh = Joint(rig.pelvis, "FrontThigh", new Vector2(.09f, 0));
            rig.frontShin = Joint(rig.frontThigh, "FrontShinBoot", Vector2.down * CharacterRig.ThighLength);
            rig.backThigh = Joint(rig.pelvis, "BackThigh", new Vector2(-.09f, 0));
            rig.backShin = Joint(rig.backThigh, "BackShinBoot", Vector2.down * CharacterRig.ThighLength);
            rig.weaponGrip = Joint(rig.frontForearm, "WeaponGrip", Vector2.down * CharacterRig.ForearmLength);
            rig.weapon = Joint(rig.weaponGrip, "Weapon", Vector2.zero).gameObject.AddComponent<SpriteRenderer>();
            rig.weapon.sortingOrder = 7;
            Transform[] joints = { rig.head, rig.torso, rig.pelvis, rig.frontUpperArm, rig.frontForearm, rig.backUpperArm,
                rig.backForearm, rig.frontThigh, rig.frontShin, rig.backThigh, rig.backShin, rig.coat };
            // The torso collar covers the neck edge and the collar baked into the female head sprite.
            int[] order = { 4, 5, 4, 8, 9, 0, 0, 3, 3, 1, 1, 2 };
            Vector2[] sizes = { new Vector2(.40f,.46f), new Vector2(body == 0 ? .60f : .53f,.70f), new Vector2(.47f,.36f),
                new Vector2(.235f,.59f), new Vector2(.19f,.55f), new Vector2(.225f,.59f), new Vector2(.18f,.55f),
                new Vector2(.24f,.83f), new Vector2(.27f,.84f), new Vector2(.24f,.83f), new Vector2(.27f,.84f), new Vector2(.60f, body == 0 ? .82f : 1.12f) };
            rig.parts = new SpriteRenderer[12];
            for (int i = 0; i < 12; i++)
            {
                var art = Joint(joints[i], "Art", Vector2.zero).gameObject.AddComponent<SpriteRenderer>();
                var sprite = sprites[i];
                art.sprite = sprite; art.sortingOrder = order[i];
                art.transform.localScale = new Vector3(sizes[i].x / sprite.bounds.size.x, sizes[i].y / sprite.bounds.size.y, 1);
                // Extend the hip art upward under the jacket while keeping its lower edge in place.
                if (i == 2) art.transform.localPosition = Vector3.up * .073f;
                if (i == 4 || i == 6) art.transform.localPosition = Vector3.down * .030f;
                rig.parts[i] = art;
            }
            return rig;
        }

        [MenuItem("MiniWar/Characters/Validate rigs")]
        public static void ValidateRigs()
        {
            int checkedPoses = 0;
            var visuals = new OnlineVisuals();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                for (int body = 0; body < 2; body++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(body));
                    if (prefab == null) throw new InvalidDataException("Missing character prefab");
                    var obj = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(obj, preview);
                    var rig = obj.GetComponent<CharacterRig>();
                    if (rig.parts.Length != 12 || rig.parts.Any(x => x == null || x.sprite == null)) throw new InvalidDataException("Missing body part");
                    if (!rig.IsTwoPiece || rig.bodyFrames.frames.Length != 16 || rig.bodyFrames.frames.Any(x => x == null)
                        || rig.parts.Any(x => x.enabled) || rig.armFrames.frames.Length != 12 || rig.armFrames.frames.Any(x => x == null))
                        throw new InvalidDataException("Missing two-piece frames or duplicate body parts");
                    if (AssetDatabase.GetAssetPath(rig.bodyFrames) != RedrawCharacterTools.BodyPath(body)
                        || rig.silhouetteWidth != 1 || rig.silhouetteHeight != 1 || rig.armThickness != 1)
                        throw new InvalidDataException("Rig must use the approved redraw with its natural proportions");
                    foreach (var sprite in rig.bodyFrames.frames.Concat(rig.armFrames.frames).Distinct())
                        if (!AssetDatabase.GetAssetPath(sprite).StartsWith(RedrawCharacterTools.Folder, StringComparison.Ordinal)
                            || sprite.texture.filterMode != FilterMode.Point)
                            throw new InvalidDataException("Character still references old art or filtered pixels: " + sprite.name);
                    foreach (int facing in new[] { -1, 1 })
                    for (int family = 0; family < 7; family++)
                    for (int tier = 1; tier <= 3; tier++)
                    for (int pose = 0; pose < 30; pose++)
                    {
                        rig.SetWeapon(visuals.Weapon(family, tier), OnlineVisuals.WeaponWidths[family]);
                        if (rig.weapon.sprite == null) throw new InvalidDataException("Missing equipment sprite");
                        if (!AssetDatabase.GetAssetPath(rig.weapon.sprite).StartsWith(PixelCharacterTools.Folder, StringComparison.Ordinal))
                            throw new InvalidDataException("Equipment still references old art");
                        bool dashing = pose >= 25;
                        rig.bodyFrameOverride = pose >= 9 && pose < 25 ? pose - 9 : -1;
                        bool aiming = (pose >= 4 && pose < 9) || dashing;
                        float angle = aiming ? new[] { -90, -45, 0, 45, 90 }[dashing ? pose - 25 : pose - 4] : 0;
                        if (facing < 0) angle = 180 - angle;
                        rig.Pose(pose * 1.1f, pose == 0 ? 0 : 5 * facing, pose == 3, 3, facing, aiming, angle, family, dashing);
                        if (dashing && rig.CurrentBodyFrame != 12) throw new InvalidDataException("Missing lowered dash pose");
                        foreach (var joint in rig.GetComponentsInChildren<Transform>())
                            if (float.IsNaN(joint.position.x) || float.IsNaN(joint.rotation.z) || float.IsInfinity(joint.position.y))
                                throw new InvalidDataException("Invalid joint transform");
                        if (rig.GetComponentsInChildren<SpriteRenderer>().Count(x => x.enabled) != 3
                            || rig.weaponGrip.parent != rig.connectedArms.transform)
                            throw new InvalidDataException("Expected body, whole arms, and equipment only");
                        Vector2 shoulder = rig.facingRoot.InverseTransformPoint(rig.connectedArms.transform.position);
                        if (Vector2.Distance(shoulder,rig.bodyFrames.frontShoulders[rig.CurrentBodyFrame]) > .025f)
                            throw new InvalidDataException("Whole arm layer detached from body socket");
                        if (rig.connectedArms.sprite != rig.armFrames.frames[rig.CurrentArmFrame]
                            || Vector2.Distance(rig.weaponGrip.localPosition,rig.armFrames.grips[rig.CurrentArmFrame]) > .001f)
                            throw new InvalidDataException("Wrong arm frame or weapon grip");
                        if (aiming)
                        {
                            Vector2 muzzle = rig.weaponGrip.TransformVector(Vector3.right);
                            float actual = Mathf.Atan2(muzzle.y, muzzle.x) * Mathf.Rad2Deg;
                            float expected = LanAim.WorldAngle(LanAim.Pose(angle, facing), facing);
                            if (Mathf.Abs(Mathf.DeltaAngle(actual, expected)) > .1f) throw new InvalidDataException("Five-direction muzzle mismatch");
                        }
                        checkedPoses++;
                    }
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            Directory.CreateDirectory("Logs/QA");
            string result = "PASS " + checkedPoses + " poses: 2 bodies, both facings, 7 weapon families, 3 tiers, 16 body frames plus idle/walk/jump/5-way aim/dash; Mac/Elias RedrawV1 with uncompressed proportions, PixelV1 equipment, Point filtering, exactly 2 character layers + equipment, shoulder socket, authored grip and muzzle direction. Visual art quality requires separate review.";
            File.WriteAllText("Logs/QA/CharacterRigValidation.txt", result); Debug.Log(result);
        }

        [MenuItem("MiniWar/Characters/Capture motion sheet")]
        public static void CaptureMotionSheet()
        {
            var renderer = new PreviewRenderUtility();
            try
            {
                var visuals = new OnlineVisuals();
                renderer.camera.orthographic = true; renderer.camera.orthographicSize = 3.65f;
                renderer.camera.transform.position = new Vector3(5.2f, -.15f, -10);
                renderer.camera.transform.rotation = Quaternion.identity;
                renderer.camera.nearClipPlane = .1f; renderer.camera.farClipPlane = 30;
                renderer.camera.clearFlags = CameraClearFlags.SolidColor;
                renderer.camera.backgroundColor = new Color(.12f, .16f, .21f);
                var font = Resources.Load<Font>("Fonts/DNFBitBitv2");
                string[] labels = { "IDLE / PISTOL", "WALK / RIFLE", "WALK / PARTS", "JUMP / SMG", "AIM / REVOLVER", "LEFT / SHOTGUN" };
                for (int body = 0; body < 2; body++)
                for (int pose = 0; pose < 6; pose++)
                {
                    var go = UnityEngine.Object.Instantiate(visuals.RigPrefab(body)); renderer.AddSingleGO(go);
                    go.transform.position = new Vector3(pose * 2.05f, -body * 3.1f, 0);
                    var rig = go.GetComponent<CharacterRig>();
                    int family = new[] { 0, 3, 3, 1, 6, 2 }[pose];
                    rig.SetWeapon(visuals.Weapon(family, 1), OnlineVisuals.WeaponWidths[family]);
                    rig.ShowPartColors(pose == 2);
                    rig.Pose(pose == 2 ? 4.2f : 1.05f, pose == 1 || pose == 2 ? 5 : 0, pose == 3, 3, pose == 5 ? -1 : 1,
                        pose >= 4, pose == 5 ? 170 : 60, family);
                    var label = new GameObject("Pose label"); renderer.AddSingleGO(label);
                    label.transform.position = go.transform.position + new Vector3(0, -.32f, 0);
                    var text = label.AddComponent<TextMesh>(); text.text = labels[pose]; text.font = font;
                    text.anchor = TextAnchor.MiddleCenter; text.fontSize = 28; text.characterSize = .025f;
                    label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                    label.GetComponent<MeshRenderer>().sortingOrder = 30;
                }
                renderer.BeginStaticPreview(new Rect(0, 0, 1920, 1080)); renderer.Render(true);
                var texture = renderer.EndStaticPreview();
                Directory.CreateDirectory("Logs/QA");
                File.WriteAllBytes("Logs/QA/CharacterMotions.png", texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                Debug.Log("Character motion sheet saved: Logs/QA/CharacterMotions.png");
            }
            finally { renderer.Cleanup(); }
        }
    }
}
