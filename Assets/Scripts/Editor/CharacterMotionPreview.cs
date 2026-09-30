using MiniWar.Online;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniWar.EditorTools
{
    /// <summary>Server-free preview in an isolated preview scene; never changes the open game scene.</summary>
    public sealed class CharacterMotionPreview : EditorWindow
    {
        PreviewRenderUtility preview;
        CharacterRig rig;
        OnlineVisuals visuals;
        int body, loadedBody = -1, family, tier = 1, motion;
        bool animate = true, colors, aiming;
        int facing = 1;
        float cycle, aimAngle;
        double lastTime;

        [MenuItem("MiniWar/Characters/Motion preview")]
        public static void Open() => GetWindow<CharacterMotionPreview>("Character Motion");

        void OnEnable() { minSize = new Vector2(470, 620); lastTime = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
        void OnDisable() { EditorApplication.update -= Tick; if (preview != null) preview.Cleanup(); preview = null; }
        void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - lastTime), .1f);
            float walkRate = rig != null && rig.bodyFrames != null ? rig.bodyFrames.walkCycleRadiansPerSecond : 10;
            if (animate) cycle += dt * (motion == 1 ? walkRate : 2);
            if (rig != null) rig.AdvanceEffects(dt);
            lastTime = now; Repaint();
        }

        void EnsureRig()
        {
            if (preview == null)
            {
                preview = new PreviewRenderUtility();
                preview.camera.orthographic = true; preview.camera.orthographicSize = 1.65f;
                preview.camera.transform.position = new Vector3(.25f, 1.2f, -10);
                preview.camera.transform.rotation = Quaternion.identity;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.09f, .12f, .17f);
                preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 30;
                visuals = new OnlineVisuals();
            }
            if (rig != null && loadedBody == body) return;
            if (rig != null) DestroyImmediate(rig.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterRigTools.PrefabPath(body));
            if (prefab == null) return;
            var instance = Instantiate(prefab); preview.AddSingleGO(instance); rig = instance.GetComponent<CharacterRig>(); loadedBody = body;
        }

        void OnGUI()
        {
            body = GUILayout.Toolbar(body, new[] { "Male", "Female" });
            motion = GUILayout.Toolbar(motion, new[] { "Idle", "Walk", "Jump" });
            EditorGUILayout.BeginHorizontal();
            animate = GUILayout.Toggle(animate, "Animate", "Button");
            colors = GUILayout.Toggle(colors, "Part colors", "Button");
            if (GUILayout.Button(facing == 1 ? "Face left" : "Face right")) facing *= -1;
            EditorGUILayout.EndHorizontal();
            family = EditorGUILayout.Popup("Weapon", family, OnlineVisuals.FamilyNames);
            tier = EditorGUILayout.IntSlider("Tier", tier, 1, 3);
            aiming = EditorGUILayout.Toggle("Aim", aiming);
            aimAngle = EditorGUILayout.Slider("Aim angle", aimAngle, -90, 90);
            EditorGUILayout.BeginHorizontal();
            string[] aimNames = { "위", "윗사선", "앞", "아랫사선", "아래" };
            for (int i = 0; i < aimNames.Length; i++)
                if (GUILayout.Button(aimNames[i])) { aiming = true; aimAngle = LanAim.LocalAngle((AimPose)i); }
            EditorGUILayout.EndHorizontal();
            if (aiming)
            {
                var pose = LanAim.Pose(aimAngle, 1);
                EditorGUILayout.LabelField("조준 자세", aimNames[(int)pose] + " / " + LanAim.LocalAngle(pose) + "°");
                EditorGUILayout.LabelField("발사 방향", aimAngle.ToString("0.0") + "° / 발사 후 직진 (정면 기준)");
            }
            if (!animate) cycle = EditorGUILayout.Slider("Pose phase", Mathf.Repeat(cycle, Mathf.PI * 2), 0, Mathf.PI * 2);
            EnsureRig();
            if (rig == null)
            {
                EditorGUILayout.HelpBox("Build character parts and rigs first.", MessageType.Info);
                if (GUILayout.Button("Build part sprites and rigs")) CharacterRigTools.Build();
                return;
            }
            rig.ShowPartColors(colors);
            if (GUILayout.Button("Fire recoil preview")) { aiming = true; rig.Recoil(); }
            rig.SetWeapon(visuals.Weapon(family, tier), OnlineVisuals.WeaponWidths[family]);
            float jumpTime = Mathf.Repeat(cycle, 4) * .5f - .35f;
            bool airborne = motion == 2 && jumpTime > 0 && jumpTime < 2 * LanRules.JumpSpeed / LanRules.Gravity;
            float height = airborne ? LanRules.JumpSpeed * jumpTime - .5f * LanRules.Gravity * jumpTime * jumpTime : 0;
            float vertical = airborne ? LanRules.JumpSpeed - LanRules.Gravity * jumpTime : 0;
            rig.transform.localPosition = Vector3.up * height;
            preview.camera.orthographicSize = motion == 2 ? (aiming ? 2.7f : 2.2f) : aiming ? 2.0f : 1.65f;
            preview.camera.transform.position = new Vector3(.25f, motion == 2 ? (aiming ? 2.1f : 1.75f) : aiming ? 1.55f : 1.2f, -10);
            rig.Pose(cycle, motion == 1 ? LanRules.WalkSpeed * facing : 0, airborne, vertical, facing, aiming,
                facing == 1 ? aimAngle : 180 - aimAngle, family);
            var area = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint)
            {
                preview.BeginPreview(area, GUIStyle.none); preview.Render(true);
                GUI.DrawTexture(area, preview.EndPreview(), ScaleMode.StretchToFill, false);
            }
            EditorGUILayout.HelpBox("몸체 한 장 + 양팔 한 장. 팔꿈치·손목은 그림에 고정되어 있습니다. 총기는 별도 교체되며, 팔 자세는 5방향으로 바뀝니다.", MessageType.None);
            if (GUILayout.Button("Open editable character prefab")) PrefabStageUtility.OpenPrefab(CharacterRigTools.PrefabPath(body));
        }
    }
}
