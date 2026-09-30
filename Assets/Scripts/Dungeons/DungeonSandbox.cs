using MiniWar.Online;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniWar.Dungeons
{
    /// <summary>Disposable local placement test. Does not connect to a server or grant rewards.</summary>
    public sealed partial class DungeonSandbox : MonoBehaviour
    {
        public DungeonLayout layout;
        public int characterBody;
        public DungeonPreviewMotor Motor { get; private set; }
        CharacterRig rig;
        Camera view;
        Sprite square;
        Texture2D texture;
        Material unlit;
        DashAfterimages ghosts;
        GUIStyle label, heading;
        bool jump, dash, drop;
        float move, cycle, ghostTime;
        Font font;
        Transform worldRoot;
        readonly System.Collections.Generic.List<GameObject> enemyVisuals=new System.Collections.Generic.List<GameObject>();
        bool drawingEnemies;

        void Start()
        {
            InitializeRoomRun();
            if (layout == null) { enabled = false; return; }
            texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white); texture.Apply();
            square = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            unlit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            font = Resources.Load<Font>("Fonts/DNFBitBitv2");
            view = new GameObject("Dungeon test camera").AddComponent<Camera>();
            view.transform.SetParent(transform); view.orthographic = true; view.orthographicSize = 5.5f;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = layout.backgroundColor;
            view.tag = "MainCamera";
            var visuals = new OnlineVisuals();
            rig = Instantiate(visuals.RigPrefab(characterBody), transform).GetComponent<CharacterRig>();
            rig.SetWeapon(visuals.Weapon(3, 1), OnlineVisuals.WeaponWidths[3]);
            BuildWorld();
        }

        void BuildWorld(Vector2? arrival=null)
        {
            if(worldRoot!=null){worldRoot.gameObject.SetActive(false);Destroy(worldRoot.gameObject);}
            worldRoot=new GameObject("Current room geometry").transform;worldRoot.SetParent(transform);
            enemyVisuals.Clear();
            if(RoomRun!=null)layout=RoomRun.Current.layout;
            view.backgroundColor=layout.backgroundColor;
            if (layout.background != null)
                Box("Background", layout.bounds, new Color(.65f,.75f,.86f,.28f), -20, layout.background);
            foreach (var floor in layout.floors)
            {
                Box("Floor " + floor.id, floor.rect, floor.color, 0, floor.sprite);
                Box("Floor edge", new Rect(floor.rect.xMin, floor.rect.yMax - .06f, floor.rect.width, .06f), floor.oneWay?new Color(.4f,.93f,.75f):new Color(.69f,.76f,.78f), 1);
                if (floor.sprite == null)
                    for (float x = floor.rect.xMin + 1; x < floor.rect.xMax; x += 1)
                        Box("Stone joint", new Rect(x, floor.rect.yMin, .025f, floor.rect.height - .06f), floor.color * .65f, 1);
            }
            drawingEnemies=true;
            if(RoomRun==null||!RoomRun.Cleared)foreach (var spawn in layout.spawns)
                for (int i = 0; i < Mathf.Clamp(spawn.count, 1, 30); i++)
                {
                    var p = spawn.PositionAt(i); var size = spawn.Size;
                    var sprite = spawn.VisualSprite;
                    Color color = spawn.enemy != null && spawn.enemy.isBoss ? new Color(.77f,.27f,.31f) : new Color(.91f,.57f,.27f);
                    Box(spawn.Label, spawn.VisualRectAt(i), sprite == null ? color : Color.white, 3, sprite, spawn.faceLeft);
                    if (sprite == null)
                        Box("Marker eye", new Rect(p.x + (spawn.faceLeft ? -.22f : .08f) * size.x, p.y + size.y * .76f, size.x * .14f, .07f), Color.white, 4);
                }
            drawingEnemies=false;
            if(RoomRun==null)
            {
                Portal(layout.entrance, new Color(.34f,.88f,.79f), "Entrance");
                Portal(layout.exit, new Color(.56f,.65f,1), "Exit");
            }
            else BuildRoomPortals(arrival??layout.entrance);
            Motor = new DungeonPreviewMotor(layout);
            if(arrival.HasValue)Motor.Reset(arrival.Value);
            ghosts?.Dispose();
            ghosts = new DashAfterimages(go => go.transform.SetParent(transform));
            UpdatePose(); FollowCamera(true);
        }

        GameObject Box(string name, Rect rect, Color color, int order, Sprite sprite = null, bool flip = false)
        {
            var go = new GameObject(name); go.transform.SetParent(worldRoot);
            if(drawingEnemies)enemyVisuals.Add(go);
            go.transform.position = new Vector3(rect.center.x, rect.center.y, 0);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite == null ? square : sprite;
            sr.sharedMaterial = unlit; sr.color = color; sr.sortingOrder = order; sr.flipX = flip;
            go.transform.localScale = new Vector3(rect.width / sr.sprite.bounds.size.x, rect.height / sr.sprite.bounds.size.y, 1);
            // Sprite pivots can be at the feet; keep the visible image centered in its authored rectangle.
            go.transform.position -= Vector3.Scale(sr.sprite.bounds.center, go.transform.localScale);
            return go;
        }

        System.Collections.Generic.List<SpriteRenderer> Portal(Vector2 p, Color color, string name)
        {
            return new System.Collections.Generic.List<SpriteRenderer>{
                Box(name, new Rect(p.x-.65f,p.y,1.3f,.08f), color, 2).GetComponent<SpriteRenderer>(),
                Box(name, new Rect(p.x-.65f,p.y,.06f,2.8f), color, 2).GetComponent<SpriteRenderer>(),
                Box(name, new Rect(p.x+.59f,p.y,.06f,2.8f), color, 2).GetComponent<SpriteRenderer>(),
                Box(name, new Rect(p.x-.65f,p.y+2.74f,1.3f,.06f), color, 2).GetComponent<SpriteRenderer>()};
        }

        void Update()
        {
            if (Motor == null) return;
            var kb = Keyboard.current;
            move = kb == null ? 0 : ((kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0));
            if (kb != null)
            {
                bool down=kb.sKey.isPressed || kb.downArrowKey.isPressed;
                jump |= kb.spaceKey.wasPressedThisFrame && !down;
                drop |= kb.spaceKey.wasPressedThisFrame && down;
                dash |= kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame;
                if (kb.rKey.wasPressedThisFrame) { Motor.Reset(); jump = dash = drop = false; }
                if(Application.isFocused&&RoomRun!=null)
                {
                    if(kb.f6Key.wasPressedThisFrame)ClearRoomForPreview();
                    if(kb.gKey.wasPressedThisFrame)GatherCompanionsForPreview();
                }
            }
            if (!Application.isFocused) { move = 0; jump = dash = drop = false; }
            cycle += Time.deltaTime; ghosts.Advance(Time.deltaTime);
            rig.AdvanceEffects(Time.deltaTime); UpdatePose();
            ghostTime -= Time.deltaTime;
            if (Motor.Dashing && ghostTime <= 0) { ghosts.Emit(rig); ghostTime = .035f; }
            if(RoomRun!=null)UpdatePartyMarkers();
        }

        void FixedUpdate()
        {
            if (Motor == null) return;
            Motor.Step(Time.fixedDeltaTime, move, jump, dash, drop); jump = dash = drop = false;
            if(RoomRun!=null)UpdateRoomFlow();
        }

        void LateUpdate() { if (Motor != null) FollowCamera(false); }

        void UpdatePose()
        {
            rig.transform.position = Motor.Position;
            rig.Pose(cycle, Motor.HorizontalSpeed, !Motor.Grounded, Motor.VerticalSpeed, Motor.Facing, false, Motor.Facing > 0 ? 0 : 180, 3, Motor.Dashing);
        }

        void FollowCamera(bool instant)
        {
            float half = view.orthographicSize * view.aspect;
            float x = layout.bounds.width <= half*2 ? layout.bounds.center.x : Mathf.Clamp(Motor.Position.x+2, layout.bounds.xMin+half, layout.bounds.xMax-half);
            float y = layout.bounds.height <= view.orthographicSize*2 ? layout.bounds.center.y
                : Mathf.Clamp(Motor.Position.y+2,layout.bounds.yMin+view.orthographicSize,layout.bounds.yMax-view.orthographicSize);
            var target = new Vector3(x, y, -10);
            view.transform.position = instant ? target : Vector3.Lerp(view.transform.position, target, 1-Mathf.Exp(-8*Time.deltaTime));
        }

        void OnGUI()
        {
            if (Motor == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, alignment = TextAnchor.MiddleCenter };
                label.normal.textColor = Color.white;
                heading = new GUIStyle(label) { fontSize = 19, alignment = TextAnchor.MiddleLeft };
            }
            if(RoomRun!=null){DrawRoomHUD();return;}
            GUI.Box(new Rect(14,14,540,90), GUIContent.none);
            GUI.Label(new Rect(30,20,510,29), layout.displayName + "  ·  배치 테스트", heading);
            GUI.Label(new Rect(25,48,515,24), "A/D 이동  Space 점프  ↓+Space 하강  Shift 대쉬", label);
            GUI.Label(new Rect(25,73,515,24), "몬스터는 위치 표시용 · 전투와 보상은 진행되지 않습니다", label);
            foreach (var spawn in layout.spawns)
            {
                Vector2 center=spawn.position+Vector2.right*((Mathf.Clamp(spawn.count,1,30)-1)*spawn.spacing*.5f);
                DrawLabel(center+Vector2.up*(spawn.Size.y+.3f),spawn.Label+(spawn.count>1?" ×"+spawn.count:""));
            }
            DrawLabel(layout.entrance+Vector2.up*3.1f, "입구"); DrawLabel(layout.exit+Vector2.up*3.1f, "출구");
            GUI.Box(new Rect(14,Screen.height-51,380,36), GUIContent.none);
            GUI.Label(new Rect(20,Screen.height-49,365,31), Motor.CanChain ? "대쉬 한 번 더 가능" : Motor.Cooldown > 0 ? "대쉬 " + Motor.Cooldown.ToString("0.0") + "초" : "대쉬 준비", label);
        }

        void DrawLabel(Vector2 p, string text)
        {
            var screen = view.WorldToScreenPoint(p);
            if (screen.x < -100 || screen.x > Screen.width+100) return;
            float width=Mathf.Clamp(label.CalcSize(new GUIContent(text)).x+20,64,260);
            var rect = new Rect(screen.x-width/2, Screen.height-screen.y-13,width,26);
            GUI.Box(rect, GUIContent.none); GUI.Label(rect, text, label);
        }

        void OnDestroy()
        {
            ghosts?.Dispose();
            if(square != null) Destroy(square); if(texture != null) Destroy(texture); if(unlit != null) Destroy(unlit);
        }
    }
}
