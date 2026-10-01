using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniWar.Data;
using MiniWar.Dungeons;
using MiniWar.Online;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public sealed partial class DungeonBuilderWindow : EditorWindow
    {
        enum Tool { Select, Floor, Monster, Entrance, Exit, Erase }
        static readonly string[] ToolNames = { "1  선택 / 이동", "2  바닥 그리기", "3  몬스터 배치", "4  입구 놓기", "5  출구 놓기", "6  지우개" };
        [SerializeField] DungeonLayout layout;
        [SerializeField] RoomDungeon roomDungeon;
        [SerializeField] string roomId;
        [SerializeField] int portalBrush;
        [SerializeField] Tool tool;
        [SerializeField] EnemyData brushEnemy;
        [SerializeField] Sprite brushFloorSprite, brushMonsterSprite;
        [SerializeField] Color brushColor = new Color(.33f,.41f,.48f);
        [SerializeField] bool gridSnap = true, groundSnap = true;
        [SerializeField] bool brushOneWay;
        [SerializeField] int previewBody;
        [SerializeField] float zoom = 22;
        [SerializeField] Vector2 center = new Vector2(30,4);
        [SerializeField] string selected;
        EnemyData[] enemies = Array.Empty<EnemyData>();
        Vector2 scroll, dragStart, originalPoint;
        Rect canvas, originalRect, paintRect;
        int dragMode, dragGroup, canvasControl;
        bool didDrag;
        GUIStyle small, title;
        string status = "바닥은 드래그 · 몬스터는 클릭해서 배치하세요";
        List<string> validation;

        public static void Open() => GetWindow<DungeonBuilderWindow>("던전 제작기").Show();

        DungeonRoom Room => roomDungeon == null ? null : roomDungeon.FindRoom(roomId);
        RoomPortal SelectedPortal => Room?.portals.Find(p=>selected=="$portal/"+p.id||selected=="$arrival/"+p.id);
        UnityEngine.Object[] UndoTargets => Room==null ? new UnityEngine.Object[]{layout} : new UnityEngine.Object[]{layout,roomDungeon};

        public static DungeonBuilderWindow ShowRoom(RoomDungeon dungeon,string id)
        {
            var room=dungeon.FindRoom(id);
            var previous=Resources.FindObjectsOfTypeAll<DungeonBuilderWindow>().FirstOrDefault();
            if(previous!=null&&previous.roomDungeon!=dungeon)DungeonCampaignAuthoring.SavePending(previous.roomDungeon);
            var window=ShowLayout(room.layout);window.roomDungeon=dungeon;window.roomId=id;
            DungeonCampaignAuthoring.Remember(dungeon);window.ReloadPalette();
            window.status="편집 중: "+dungeon.displayName+" / "+room.displayName+" · Ctrl+S 저장";
            window.titleContent=new GUIContent("방 내부 배치 · "+room.displayName);window.tool=Tool.Select;window.Repaint();return window;
        }

        public static DungeonBuilderWindow ShowLayout(DungeonLayout asset)
        {
            var window = GetWindow<DungeonBuilderWindow>("던전 제작기");
            window.SetLayout(asset); window.Show(); return window;
        }

        [OnOpenAsset]
        static bool OpenAsset(EntityId instanceId, int line)
        {
            var asset = EditorUtility.EntityIdToObject(instanceId) as DungeonLayout;
            if (asset == null) return false;
            var owner=AssetDatabase.LoadAssetAtPath<RoomDungeon>(AssetDatabase.GetAssetPath(asset));
            var room=owner?.rooms.Find(r=>r.layout==asset);
            if(room!=null)ShowRoom(owner,room.id);else ShowLayout(asset); return true;
        }

        void OnEnable()
        {
            minSize = new Vector2(980,640);
            wantsMouseMove = true;
            Undo.undoRedoPerformed += OnUndo; ReloadPalette(); ClearGroupedSelection();
        }
        void OnDisable() { Undo.undoRedoPerformed -= OnUndo; EndDrag(); }
        void OnUndo() { validation = null; ClearGroupedSelection(); Repaint(); }
        void ReloadPalette()
        {
            enemies = AssetDatabase.FindAssets("t:EnemyData").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EnemyData>).OrderBy(e => e.displayName).ToArray();
            if (brushEnemy == null) brushEnemy = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/DungeonLayouts/Enemies/Patrol.asset") ?? enemies.FirstOrDefault();
        }

        void SetLayout(DungeonLayout asset)
        {
            EndDrag(); layout = asset; selected = null; validation = null;roomDungeon=null;roomId=null;
            if (asset != null) { center = asset.bounds.center; Fit(); }
            Repaint();
        }

        void Fit()
        {
            if (layout == null) return;
            center = layout.bounds.center;
            zoom = Mathf.Clamp(Mathf.Min(Mathf.Max(400,position.width-290)/layout.bounds.width,
                Mathf.Max(300,position.height-125)/layout.bounds.height)*.9f,4,100);
        }

        void OnGUI()
        {
            canvasControl = GUIUtility.GetControlID("DungeonCanvas".GetHashCode(),FocusType.Passive);
            if (small == null)
            {
                small = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                small.normal.textColor = Color.white;
                title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            }
            DrawToolbar();
            canvas = new Rect(274,62,Mathf.Max(1,position.width-282),Mathf.Max(1,position.height-92));
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                GUILayout.BeginArea(new Rect(8,65,254,position.height-96));
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawSidebar();
                EditorGUILayout.EndScrollView(); GUILayout.EndArea();
                DrawCanvas();
                if (!EditorApplication.isPlayingOrWillChangePlaymode && layout != null) HandleCanvasEvent(Event.current);
            }
            EditorGUI.DrawRect(new Rect(0,position.height-26,position.width,26),new Color(.13f,.17f,.21f));
            GUI.Label(new Rect(10,position.height-25,position.width-20,24), status, small);
        }

        void DrawToolbar()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                GUILayout.BeginHorizontal(EditorStyles.toolbar);
                if(Room!=null && GUILayout.Button("← 방 연결 지도",EditorStyles.toolbarButton,GUILayout.Width(105)))RoomDungeonBuilderWindow.ShowDungeon(roomDungeon);
                EditorGUI.BeginDisabledGroup(Room!=null);
                var next = (DungeonLayout)EditorGUILayout.ObjectField(layout, typeof(DungeonLayout), false, GUILayout.Width(235));
                EditorGUI.EndDisabledGroup();
                if (next != layout) SetLayout(next);
                if(Room==null)
                {
                if (GUILayout.Button("새 던전",EditorStyles.toolbarButton,GUILayout.Width(65))) NewLayout();
                if (GUILayout.Button("성문 외곽 예제",EditorStyles.toolbarButton,GUILayout.Width(105)))
                { SetLayout(DungeonBuilderAssets.CreateGateExample()); ReloadPalette(); }
                }
                using(new EditorGUI.DisabledScope(layout == null))
                {
                    if (Room==null && GUILayout.Button("복사본",EditorStyles.toolbarButton,GUILayout.Width(60))) SaveCopy();
                    if (GUILayout.Button(layout != null && EditorUtility.IsDirty(layout) ? "저장 *" : "저장",EditorStyles.toolbarButton,GUILayout.Width(60))) Save();
                    if (GUILayout.Button("전체 보기",EditorStyles.toolbarButton,GUILayout.Width(75))) Fit();
                    if (GUILayout.Button("배치 검사",EditorStyles.toolbarButton,GUILayout.Width(75))) Check();
                    if (GUILayout.Button("▶ 이동 테스트",EditorStyles.toolbarButton,GUILayout.Width(110)))
                    { Save(); if(Room==null)DungeonBuilderAssets.Play(layout,previewBody);else DungeonBuilderAssets.PlayRooms(roomDungeon,previewBody,1,roomId); }
                }
                GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(); GUILayout.Space(10);
                DungeonCampaignAuthoring.Picker(roomDungeon, nextDungeon=>{
                    ShowRoom(nextDungeon,nextDungeon.startRoomId);ReloadPalette();scroll=Vector2.zero;hoverInside=false;
                    status="던전 변경 · "+nextDungeon.displayName+" · 이전 작업 저장 완료";
                },170);
                if(Room!=null)DrawRoomPicker();
                else GUILayout.Label(layout == null ? "새 던전을 만들거나 예제를 열어 시작하세요" : layout.displayName, title);
                GUILayout.FlexibleSpace();
                GUILayout.Label("중간 버튼 드래그: 화면 이동    휠: 확대 / 축소", EditorStyles.miniLabel);
                GUILayout.Space(10); GUILayout.EndHorizontal();
            }
        }

        void DrawSidebar()
        {
            if (layout == null)
            {
                if(GUILayout.Button("내 맵 그리기 시작",GUILayout.Height(38)))OpenPainting();
                EditorGUILayout.HelpBox("복층 예제를 복사한 내 맵에서 시작합니다. 바닥은 드래그하고 몬스터는 그림을 골라 놓으세요.",MessageType.Info); return;
            }
            GUILayout.Label("배치 도구",EditorStyles.boldLabel);
            var labels=(string[])ToolNames.Clone();if(Room!=null){labels[3]="4  시작 위치";labels[4]="5  포탈 놓기";}
            Tool next = (Tool)GUILayout.SelectionGrid((int)tool,labels,2,GUILayout.Height(78));
            if (next != tool) { EndDrag(); tool = next; }
            gridSnap = EditorGUILayout.ToggleLeft("격자에 맞추기",gridSnap);
            groundSnap = EditorGUILayout.ToggleLeft("발 위치를 가까운 바닥에 맞추기",groundSnap);
            if(Room!=null&&tool==Tool.Exit)
            {
                if(Room.portals.Count==0)EditorGUILayout.HelpBox("방 연결 지도에서 먼저 이웃 방을 연결해주세요.",MessageType.Info);
                else
                {
                    portalBrush=Mathf.Clamp(portalBrush,0,Room.portals.Count-1);
                    portalBrush=EditorGUILayout.Popup("놓을 포탈",portalBrush,Room.portals.Select(p=>RoomPortal.Label(p.direction)).ToArray());
                    EditorGUILayout.HelpBox("클릭하면 해당 포탈을 옮깁니다. 위/아래 방향 포탈도 캐릭터가 걸어서 들어갈 수 있는 문 위치에 놓아주세요.",MessageType.None);
                }
            }
            if (tool == Tool.Floor)
            {
                DrawFloorBrush();
                brushOneWay = EditorGUILayout.Popup("지형 종류",brushOneWay?1:0,new[]{"단단한 바닥 / 벽","아래에서 통과하는 발판"})==1;
                EditorGUILayout.HelpBox(brushOneWay?"아래에서 점프로 통과하고 위에서는 착지합니다. ↓+Space로 내려갑니다. 3m 층 간격, 0.5m 두께부터 시작하세요.":"원하는 영역을 드래그하세요. 사방을 막는 바닥과 벽을 만듭니다.",MessageType.None);
                brushFloorSprite = (Sprite)EditorGUILayout.ObjectField("바닥 이미지",brushFloorSprite,typeof(Sprite),false);
                brushColor = EditorGUILayout.ColorField("바닥 색",brushColor);
            }
            if (tool == Tool.Monster)
            {
                DrawMonsterPalette();
                int index = Array.IndexOf(enemies,brushEnemy);
                int nextIndex = EditorGUILayout.Popup("몬스터 종류",index,enemies.Select(e=>e.displayName).ToArray());
                if (nextIndex >= 0 && nextIndex < enemies.Length) brushEnemy = enemies[nextIndex];
                brushEnemy = (EnemyData)EditorGUILayout.ObjectField("능력치 에셋",brushEnemy,typeof(EnemyData),false);
                brushMonsterSprite = (Sprite)EditorGUILayout.ObjectField("배치 이미지",brushMonsterSprite,typeof(Sprite),false);
                if (GUILayout.Button("목록 새로고침")) ReloadPalette();
                monsterStroke=EditorGUILayout.ToggleLeft("드래그해서 여러 마리 놓기",monsterStroke);
                if(monsterStroke)monsterSpacing=EditorGUILayout.Slider("배치 간격 (m)",monsterSpacing,1,5);
                EditorGUILayout.HelpBox("그림 선택 → 클릭하면 한 마리. 드래그하면 일정 간격으로 놓습니다. 초록 테두리는 배치 가능, 빨간 테두리는 바닥·범위를 확인하세요.",MessageType.None);
            }
            if(tool==Tool.Erase)EditorGUILayout.HelpBox("몬스터는 한 마리씩, 바닥은 한 개씩 지웁니다. 다른 도구에서도 우클릭으로 지울 수 있습니다. 포탈은 방 연결 지도에서 끊어주세요.",MessageType.None);
            if(tool==Tool.Select||!string.IsNullOrEmpty(selected))
            {GUILayout.Space(8);GUILayout.Label("선택한 배치",EditorStyles.boldLabel);DrawSelection();}
            GUILayout.Space(10);settingsOpen=EditorGUILayout.Foldout(settingsOpen,"맵 크기 / 배경 / 점프 설정",true);
            if(settingsOpen)
            {
            var so = new SerializedObject(layout); so.Update();
            EditorGUI.BeginChangeCheck();
            if(Room==null)Property(so,"displayName","이름"); Property(so,"description","설명");
            Property(so,"bounds","맵 범위"); Property(so,"gridSize","격자 크기");
            Property(so,"jumpHeight","첫 점프 높이 (m)");
            Property(so,"airJumpHeight","공중 점프 높이 (m)");
            Property(so,"background","배경 이미지"); Property(so,"backgroundColor","배경 색");
            if (EditorGUI.EndChangeCheck()) { so.ApplyModifiedProperties(); validation = null; Repaint(); }
            }
            previewBody = EditorGUILayout.Popup("테스트 캐릭터",previewBody,new[]{"남성","여성"});
            GUILayout.Space(6);
            GUILayout.Label("바닥 " + layout.floors.Count + "개  ·  몬스터 " + layout.spawns.Sum(s=>s.count) + "마리",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("우클릭 지우기 · Ctrl+Z 되돌리기\nDelete 삭제 · Ctrl+D 복제 · Esc 그리기 취소\nCtrl+S 저장 · F 전체 보기",MessageType.None);
            if(validation != null)
            {
                if(validation.Count == 0) EditorGUILayout.HelpBox("배치 검사 완료: 문제를 찾지 못했습니다.\n이동 테스트로 동선도 확인해주세요.",MessageType.Info);
                else foreach(var issue in validation.Distinct()) EditorGUILayout.HelpBox(issue,MessageType.Warning);
            }
        }

        static void Property(SerializedObject so,string path,string label) => EditorGUILayout.PropertyField(so.FindProperty(path),new GUIContent(label),true);

        void DrawSelection()
        {
            if(SelectedPortal!=null)
            {
                var portal=SelectedPortal;int r=roomDungeon.rooms.IndexOf(Room),p=Room.portals.IndexOf(portal);
                var data=new SerializedObject(roomDungeon);data.Update();EditorGUI.BeginChangeCheck();
                string root="rooms.Array.data["+r+"].portals.Array.data["+p+"]";
                GUILayout.Label(RoomPortal.Label(portal.direction)+" → "+(roomDungeon.FindRoom(portal.targetRoomId)?.displayName??"연결 없음"),EditorStyles.boldLabel);
                Property(data,root+".position","포탈 발 위치");Property(data,root+".arrival","들어온 뒤 도착 위치");
                if(EditorGUI.EndChangeCheck()){data.ApplyModifiedProperties();validation=null;Repaint();}
                EditorGUILayout.HelpBox("도착 위치는 포탈 밖에 두세요. 분홍색 점을 끌어서 옮길 수도 있습니다.",MessageType.None);
                if(GUILayout.Button("바닥에 붙이기")){Record("포탈 접지");SetSelectedPoint(layout.SnapToFloor(SelectedPoint(),100));Changed();}
                return;
            }
            int floorIndex = layout.floors.FindIndex(f=>f.id==selected), spawnIndex = layout.spawns.FindIndex(s=>s.id==selected);
            var so = new SerializedObject(layout); so.Update(); EditorGUI.BeginChangeCheck();
            if(floorIndex >= 0)
            {
                string root = "floors.Array.data["+floorIndex+"]";
                GUILayout.Label("바닥 / 벽  ·  모서리를 끌어 크기 변경",EditorStyles.miniLabel);
                Property(so,root+".rect","영역"); Property(so,root+".sprite","이미지"); Property(so,root+".color","색");
                Property(so,root+".oneWay","아래에서 통과하는 발판");
            }
            else if(spawnIndex >= 0)
            {
                if(layout.spawns[spawnIndex].count>1)
                {
                    EditorGUI.EndChangeCheck();
                    EditorGUILayout.HelpBox("예전에 묶어서 배치한 몬스터입니다. 화면에서 원하는 한 마리를 클릭해 편집하세요.",MessageType.None);
                    return;
                }
                string root = "spawns.Array.data["+spawnIndex+"]";
                Property(so,root+".enemy","몬스터 종류"); Property(so,root+".position","발 위치");
                Property(so,root+".faceLeft","왼쪽 바라보기"); Property(so,root+".sprite","배치 이미지");
            }
            else if(selected=="$entrance") Property(so,"entrance","입구 발 위치");
            else if(selected=="$exit") Property(so,"exit","출구 발 위치");
            else GUILayout.Label("선택 도구로 배치를 클릭하세요.",EditorStyles.wordWrappedMiniLabel);
            if(EditorGUI.EndChangeCheck()) { so.ApplyModifiedProperties(); validation = null; Repaint(); }
            if(floorIndex >= 0 || spawnIndex >= 0)
            {
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("복제")) DuplicateSelection();
                if(GUILayout.Button("삭제")) DeleteSelection();
                GUILayout.EndHorizontal();
            }
            if(spawnIndex >= 0 || selected=="$entrance" || selected=="$exit")
                if(GUILayout.Button("바닥에 붙이기")) { Record("배치 접지"); SetSelectedPoint(layout.SnapToFloor(SelectedPoint(),100)); Changed(); }
        }

        Vector2 WorldToCanvas(Vector2 p) => new Vector2(canvas.width/2+(p.x-center.x)*zoom,canvas.height/2-(p.y-center.y)*zoom);
        Vector2 ScreenToWorld(Vector2 p) => center+new Vector2(p.x-canvas.x-canvas.width/2,-(p.y-canvas.y-canvas.height/2))/zoom;
        Rect WorldRect(Rect r)
        {
            var a = WorldToCanvas(new Vector2(r.xMin,r.yMax)); return new Rect(a.x,a.y,r.width*zoom,r.height*zoom);
        }
        static void Outline(Rect r,Color c,float width=1)
        {
            EditorGUI.DrawRect(new Rect(r.x,r.y,r.width,width),c); EditorGUI.DrawRect(new Rect(r.x,r.yMax-width,r.width,width),c);
            EditorGUI.DrawRect(new Rect(r.x,r.y,width,r.height),c); EditorGUI.DrawRect(new Rect(r.xMax-width,r.y,width,r.height),c);
        }

        void DrawCanvas()
        {
            EditorGUI.DrawRect(canvas,new Color(.07f,.095f,.13f));
            if(layout == null) return;
            GUI.BeginGroup(canvas);
            var bounds = WorldRect(layout.bounds);
            EditorGUI.DrawRect(bounds,layout.backgroundColor);
            if(layout.background != null)
            {
                GUI.color = new Color(1,1,1,.18f); OnlineVisuals.DrawSprite(bounds,layout.background); GUI.color = Color.white;
            }
            float step = Mathf.Max(.25f,layout.gridSize);
            while(step*zoom < 12) step *= 2;
            Vector2 min = ScreenToWorld(new Vector2(canvas.x,canvas.yMax)), max = ScreenToWorld(new Vector2(canvas.xMax,canvas.y));
            for(float x=Mathf.Ceil(min.x/step)*step;x<max.x;x+=step)
            { float px=WorldToCanvas(new Vector2(x,0)).x; EditorGUI.DrawRect(new Rect(px,0,1,canvas.height),new Color(1,1,1,.045f)); }
            for(float y=Mathf.Ceil(min.y/step)*step;y<max.y;y+=step)
            { float py=WorldToCanvas(new Vector2(0,y)).y; EditorGUI.DrawRect(new Rect(0,py,canvas.width,1),new Color(1,1,1,.045f)); }
            Outline(bounds,new Color(.4f,.5f,.6f));
            foreach(var floor in layout.floors)
            {
                var r=WorldRect(floor.rect);
                if(!DungeonStoneSurface.DrawPreview(r,floor))EditorGUI.DrawRect(r,floor.color);
                if(floor.sprite != null) { GUI.color=floor.color; GUI.DrawTextureWithTexCoords(r,floor.sprite.texture,SpriteUV(floor.sprite)); GUI.color=Color.white; }
                EditorGUI.DrawRect(new Rect(r.x,r.y,r.width,2),floor.oneWay?new Color(.4f,.93f,.75f):new Color(.64f,.74f,.8f));
                if(floor.oneWay)GUI.Label(new Rect(r.x,r.y,r.width,Mathf.Max(15,r.height)),"↑ 점프 발판",small);
                if(floor.id==selected)
                {
                    Outline(r,new Color(.3f,.87f,1),2);
                    EditorGUI.DrawRect(new Rect(r.xMax-5,r.yMax-5,10,10),Color.cyan);
                    Caption(new Vector2(r.center.x,r.y-14),floor.rect.width.ToString("0.#")+" × "+floor.rect.height.ToString("0.#"));
                }
            }
            foreach(var spawn in layout.spawns)
            {
                var size=spawn.Size; bool boss=spawn.enemy!=null&&spawn.enemy.isBoss;
                for(int i=0;i<Mathf.Clamp(spawn.count,1,30);i++)
                {
                    Vector2 p=spawn.PositionAt(i);
                    var r=WorldRect(spawn.VisualRectAt(i));
                    var sprite=spawn.VisualSprite;
                    if(sprite!=null)
                    {
                        var uv=SpriteUV(sprite);
                        if(spawn.faceLeft){uv.x+=uv.width;uv.width=-uv.width;}
                        GUI.DrawTextureWithTexCoords(r,sprite.texture,uv);
                    }
                    else
                    {
                        EditorGUI.DrawRect(r,boss?new Color(.76f,.25f,.31f):new Color(.91f,.56f,.25f));
                        EditorGUI.DrawRect(new Rect(spawn.faceLeft?r.x+3:r.xMax-7,r.y+r.height*.22f,4,3),Color.white);
                    }
                    if(spawn.id==selected&&i==0) Outline(r,Color.cyan,2);
                    Caption(new Vector2(r.center.x,r.y-13),(boss?"BOSS  ":"")+spawn.Label);
                }
            }
            DrawPortal(layout.entrance,Room==null?"입구":"시작 위치","$entrance",new Color(.35f,.88f,.79f));
            if(Room==null)DrawPortal(layout.exit,"출구","$exit",new Color(.6f,.68f,1));
            else foreach(var portal in Room.portals)
            {
                DrawPortal(portal.position,RoomPortal.Label(portal.direction)+" · "+(roomDungeon.FindRoom(portal.targetRoomId)?.displayName??"?"),"$portal/"+portal.id,new Color(.65f,.69f,1));
                if(SelectedPortal==portal)
                {
                    var p=WorldToCanvas(portal.arrival);EditorGUI.DrawRect(new Rect(p.x-5,p.y-5,10,10),new Color(1,.5f,.8f));
                    Caption(p+Vector2.down*15,"도착 위치");
                }
            }
            if(dragMode==1) { var r=WorldRect(paintRect); EditorGUI.DrawRect(r,new Color(.3f,.8f,1,.24f)); Outline(r,Color.cyan,2); }
            DrawBrushPreview();
            GUI.EndGroup();
            string toolLabel=Room!=null&&tool==Tool.Exit?"5  포탈 놓기":ToolNames[(int)tool];
            GUI.Label(new Rect(canvas.x+10,canvas.y+8,360,22),toolLabel+"  |  격자 "+layout.gridSize.ToString("0.##")+"m",EditorStyles.whiteMiniLabel);
        }

        static Rect SpriteUV(Sprite sprite) { var r=sprite.rect; return new Rect(r.x/sprite.texture.width,r.y/sprite.texture.height,r.width/sprite.texture.width,r.height/sprite.texture.height); }
        void Caption(Vector2 p,string text)
        {
            float width=Mathf.Max(48,small.CalcSize(new GUIContent(text)).x+12);
            var r=new Rect(p.x-width/2,p.y-9,width,18);
            EditorGUI.DrawRect(r,new Color(.08f,.1f,.15f,.92f)); GUI.Label(r,text,small);
        }
        void DrawPortal(Vector2 p,string text,string id,Color color)
        {
            var r=WorldRect(new Rect(p.x-.65f,p.y,1.3f,2.8f)); Outline(r,selected==id?Color.cyan:color,2);
            Caption(new Vector2(r.center.x,r.y-13),text);
        }

        void HandleCanvasEvent(Event e)
        {
            int control=canvasControl;
            bool inside=canvas.Contains(e.mousePosition);
            if(e.type==EventType.KeyDown && !EditorGUIUtility.editingTextField && GUIUtility.keyboardControl==0)
            {
                if(e.control||e.command)
                {
                    if(e.keyCode==KeyCode.S) { Save(); e.Use(); }
                    else if(e.keyCode==KeyCode.D) { DuplicateSelection(); e.Use(); }
                }
                else if(e.keyCode==KeyCode.Delete||e.keyCode==KeyCode.Backspace) { DeleteSelection(); e.Use(); }
                else if(e.keyCode==KeyCode.F) { Fit(); e.Use(); }
                else if(e.keyCode>=KeyCode.Alpha1&&e.keyCode<=KeyCode.Alpha6) { EndDrag(); tool=(Tool)(e.keyCode-KeyCode.Alpha1); e.Use(); }
                else if(e.keyCode==KeyCode.Escape) { CancelStroke(); e.Use(); }
                Repaint();
            }
            if(e.type==EventType.ScrollWheel && inside)
            {
                Vector2 before=ScreenToWorld(e.mousePosition);
                zoom=Mathf.Clamp(zoom*Mathf.Pow(1.1f,-e.delta.y),4,120);
                center+=before-ScreenToWorld(e.mousePosition); e.Use(); Repaint(); return;
            }
            Vector2 world=ScreenToWorld(e.mousePosition), snapped=gridSnap?layout.Snap(world):world;
            if(e.type==EventType.MouseMove){hoverWorld=snapped;hoverInside=inside;Repaint();}
            if(e.type==EventType.MouseLeaveWindow){hoverInside=false;Repaint();}
            if(e.type==EventType.MouseDown && inside && (e.button==0||e.button==1||e.button==2))
            {
                GUI.FocusControl(null); GUIUtility.keyboardControl=0;
                GUIUtility.hotControl=control; didDrag=false;
                dragStart=snapped; originalPoint=world;
                if(e.button==2||e.alt) { dragMode=4; originalPoint=center; dragStart=e.mousePosition; }
                else if(e.button==1||tool==Tool.Erase) { BeginStroke(6,"배치 지우기");lastStrokePoint=world;EraseAt(world); }
                else if(tool==Tool.Floor) { dragMode=1; paintRect=new Rect(snapped,Vector2.zero); }
                else if(tool==Tool.Monster)
                {
                    if(brushEnemy==null) status="먼저 몬스터 종류를 선택해주세요.";
                    else
                    {
                        BeginStroke(5,"몬스터 그리기");lastStrokePoint=snapped;strokeDistance=0;strokeFeet.Clear();StampMonster(snapped);
                    }
                }
                else if(tool==Tool.Entrance||tool==Tool.Exit)
                {
                    if(Room==null||tool==Tool.Entrance||Room.portals.Count>0)
                    {
                        Record("입구 / 포탈 배치"); selected=tool==Tool.Entrance?"$entrance":Room==null?"$exit":"$portal/"+Room.portals[Mathf.Clamp(portalBrush,0,Room.portals.Count-1)].id;
                        SetSelectedPoint(PlaceFeet(snapped,true)); Changed();
                    }
                }
                else
                {
                    var floor=layout.floors.Find(f=>f.id==selected);
                    var handle=floor==null?Vector2.positiveInfinity:new Vector2(floor.rect.xMax,floor.rect.yMin);
                    if((world-handle).magnitude*zoom < 12) { dragMode=3; originalRect=floor.rect; }
                    else
                    {
                        selected=HitTest(world);
                        if(selected!=null) dragMode=2;
                    }
                    if(dragMode==2||dragMode==3) { Undo.IncrementCurrentGroup(); dragGroup=Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(UndoTargets,"던전 배치 이동"); }
                    if(dragMode==2) { selected=IndividualMonsterAt(selected,world); originalPoint=SelectedPoint(); }
                }
                e.Use(); Repaint();
            }
            else if(e.type==EventType.MouseDrag && GUIUtility.hotControl==control)
            {
                didDrag=true;
                if(dragMode==4) center=originalPoint+new Vector2(dragStart.x-e.mousePosition.x,e.mousePosition.y-dragStart.y)/zoom;
                else if(dragMode==1) paintRect=FloorBrushRect(dragStart,snapped);
                else if(dragMode==5&&monsterStroke) PaintMonstersTo(snapped);
                else if(dragMode==6) EraseTo(world);
                else if(dragMode==2) { SetSelectedPoint(originalPoint+snapped-dragStart); Changed(); }
                else if(dragMode==3)
                {
                    var floor=layout.floors.Find(f=>f.id==selected); float min=Mathf.Max(.25f,layout.gridSize);
                    if(floor!=null) floor.rect=Rect.MinMaxRect(originalRect.xMin,Mathf.Min(snapped.y,originalRect.yMax-min),Mathf.Max(snapped.x,originalRect.xMin+min),originalRect.yMax);
                    Changed();
                }
                e.Use(); Repaint();
            }
            else if(e.type==EventType.MouseUp && GUIUtility.hotControl==control)
            {
                if(dragMode==1 && didDrag && paintRect.width >= .1f && paintRect.height >= .1f)
                {
                    Record("바닥 그리기"); var floor=new DungeonFloor { rect=paintRect, sprite=brushFloorSprite,color=brushColor,oneWay=brushOneWay };
                    layout.floors.Add(floor); selected=floor.id; Changed();
                }
                if(dragMode==2 && didDrag && groundSnap)
                {
                    var spawn=layout.spawns.Find(s=>s.id==selected);
                    if(selected=="$entrance"||selected=="$exit"||SelectedPortal!=null||(spawn!=null&&(spawn.enemy==null||spawn.enemy.locomotion==LocomotionKind.Ground)))
                    { SetSelectedPoint(layout.SnapToFloor(SelectedPoint())); Changed(); }
                }
                EndDrag(); e.Use(); Repaint();
            }
        }

        Vector2 PlaceFeet(Vector2 p,bool ground) => groundSnap&&ground?layout.SnapToFloor(p):p;
        string HitTest(Vector2 p)
        {
            if(Room!=null)
            {
                if(SelectedPortal!=null&&(p-SelectedPortal.arrival).magnitude*zoom<12)return "$arrival/"+SelectedPortal.id;
                foreach(var portal in Room.portals)if(new Rect(portal.position.x-.65f,portal.position.y,1.3f,2.8f).Contains(p))return "$portal/"+portal.id;
            }
            if(new Rect(layout.entrance.x-.65f,layout.entrance.y,1.3f,2.8f).Contains(p)) return "$entrance";
            if(Room==null&&new Rect(layout.exit.x-.65f,layout.exit.y,1.3f,2.8f).Contains(p)) return "$exit";
            for(int i=layout.spawns.Count-1;i>=0;i--)
            { var s=layout.spawns[i]; for(int n=0;n<Mathf.Clamp(s.count,1,30);n++) if(s.VisualRectAt(n).Contains(p))return s.id; }
            for(int i=layout.floors.Count-1;i>=0;i--) if(layout.floors[i].rect.Contains(p))return layout.floors[i].id;
            return null;
        }
        Vector2 SelectedPoint()
        {
            if(SelectedPortal!=null)return selected.StartsWith("$arrival/")?SelectedPortal.arrival:SelectedPortal.position;
            if(selected=="$entrance")return layout.entrance; if(selected=="$exit")return layout.exit;
            var floor=layout.floors.Find(f=>f.id==selected); if(floor!=null)return floor.rect.position;
            var spawn=layout.spawns.Find(s=>s.id==selected); return spawn==null?Vector2.zero:spawn.position;
        }
        void SetSelectedPoint(Vector2 p)
        {
            if(SelectedPortal!=null){if(selected.StartsWith("$arrival/"))SelectedPortal.arrival=p;else SelectedPortal.position=p;}
            else if(selected=="$entrance")layout.entrance=p;
            else if(selected=="$exit")layout.exit=p;
            else { var floor=layout.floors.Find(f=>f.id==selected); if(floor!=null)floor.rect=new Rect(p,floor.rect.size);
                var spawn=layout.spawns.Find(s=>s.id==selected); if(spawn!=null)spawn.position=p; }
        }
        void EndDrag()
        {
            if(dragMode==2||dragMode==3||dragMode==5||dragMode==6) Undo.CollapseUndoOperations(dragGroup);
            if(GUIUtility.hotControl==canvasControl) GUIUtility.hotControl=0;
            dragMode=0; didDrag=false;
        }
        void Record(string text) { Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName(text); Undo.RecordObjects(UndoTargets,text); }
        void Changed() { EditorUtility.SetDirty(layout);if(Room!=null)EditorUtility.SetDirty(roomDungeon); validation=null; Repaint(); }
        void DeleteSelection()
        {
            if(layout==null||string.IsNullOrEmpty(selected))return;
            if(selected.StartsWith("$")) { status=Room==null?"입구와 출구는 지우지 않고 다른 위치로 옮겨주세요.":"포탈 연결 해제는 방 연결 지도에서 할 수 있습니다."; return; }
            Record("배치 삭제"); selected=IndividualMonsterAt(selected,null);layout.floors.RemoveAll(f=>f.id==selected); layout.spawns.RemoveAll(s=>s.id==selected); selected=null; Changed();
        }
        void DuplicateSelection()
        {
            if(layout==null)return;
            var floor=layout.floors.Find(f=>f.id==selected);var spawn=layout.spawns.Find(s=>s.id==selected);
            if(floor==null&&spawn==null)return; Record("배치 복제");
            if(spawn!=null){selected=IndividualMonsterAt(selected,null);spawn=layout.spawns.Find(s=>s.id==selected);}
            if(floor!=null) { var copy=JsonUtility.FromJson<DungeonFloor>(JsonUtility.ToJson(floor)); copy.id=Guid.NewGuid().ToString("N"); copy.rect.position+=Vector2.right*Mathf.Max(.5f,layout.gridSize);layout.floors.Add(copy);selected=copy.id; }
            if(spawn!=null) { var copy=JsonUtility.FromJson<DungeonSpawn>(JsonUtility.ToJson(spawn));copy.id=Guid.NewGuid().ToString("N");copy.position.x+=Mathf.Max(1,monsterSpacing);layout.spawns.Add(copy);selected=copy.id; }
            Changed();
        }
        void Save() { if(layout==null)return;if(Room!=null)RoomDungeonAuthoring.Save(roomDungeon);else AssetDatabase.SaveAssetIfDirty(layout);status="저장 완료 · "+AssetDatabase.GetAssetPath(layout); }
        void Check() { validation=Room==null?layout.ValidateLayout():roomDungeon.ValidateDungeon();status=validation.Count==0?"배치 검사 완료":validation.Count+"개 항목을 왼쪽에서 확인해주세요."; }
        void NewLayout()
        {
            string path=EditorUtility.SaveFilePanelInProject("새 던전 저장","NewDungeon","asset","던전 파일을 저장할 위치를 선택하세요.","Assets/Data");
            if(string.IsNullOrEmpty(path))return;
            var asset=CreateInstance<DungeonLayout>();asset.displayName=Path.GetFileNameWithoutExtension(path);asset.jumpHeight=3.5f;
            asset.floors.Add(new DungeonFloor {rect=new Rect(0,-2,60,2)});
            AssetDatabase.CreateAsset(asset,path);SetLayout(asset);Save();
        }
        void SaveCopy()
        {
            string path=EditorUtility.SaveFilePanelInProject("던전 복사본 저장",layout.name+"_Copy","asset","원본을 보존하면서 새로운 던전을 만듭니다.","Assets/Data");
            if(string.IsNullOrEmpty(path))return;
            var asset=Instantiate(layout);asset.name=Path.GetFileNameWithoutExtension(path);asset.displayName+=" (복사)";
            AssetDatabase.CreateAsset(asset,path);SetLayout(asset);Save();
        }
    }
}
