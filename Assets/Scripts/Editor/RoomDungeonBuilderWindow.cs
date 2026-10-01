using System;
using System.Collections.Generic;
using System.Linq;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public sealed partial class RoomDungeonBuilderWindow : EditorWindow
    {
        [SerializeField] RoomDungeon dungeon;
        [SerializeField] string selected;
        [SerializeField] int body, partySize=4;
        [SerializeField] Vector2 pan;
        [SerializeField] float scale=1;
        Vector2 scroll;
        Rect canvas;
        List<string> problems;
        GUIStyle nameStyle,small;
        string status="2 방 배치 → 3 연결로 원하는 통로만 만드세요. 4 끊기는 연결선을 클릭합니다.";
        public RoomDungeon Dungeon=>dungeon;

        [MenuItem("MiniWar/던전 제작기")]
        public static void Open() {ShowDungeon(DungeonCampaignAuthoring.Last());}
        public static RoomDungeonBuilderWindow ShowDungeon(RoomDungeon asset)
        {
            var w=GetWindow<RoomDungeonBuilderWindow>("던전 제작기 · 방 연결");
            w.EndGraphDrag();if(w.dungeon!=asset)DungeonCampaignAuthoring.SavePending(w.dungeon);
            w.dungeon=asset;DungeonCampaignAuthoring.Remember(asset);
            w.status="편집 중: "+asset.displayName+" · 방 배치와 통로 연결은 별도로 편집합니다.";
            w.selected=asset.startRoomId;w.problems=null;w.linkSource=null;w.FitGraph();w.Show();w.Repaint();return w;
        }
        [OnOpenAsset]
        static bool OnOpen(EntityId id,int line)
        {var asset=EditorUtility.EntityIdToObject(id) as RoomDungeon;if(asset==null)return false;ShowDungeon(asset);return true;}
        void OnEnable(){minSize=new Vector2(1080,690);wantsMouseMove=true;Undo.undoRedoPerformed+=OnUndo;}
        void OnDisable(){Undo.undoRedoPerformed-=OnUndo;EndGraphDrag();}
        void OnUndo(){problems=null;linkSource=null;EndGraphDrag();Repaint();}

        void OnGUI()
        {
            graphControl=GUIUtility.GetControlID("RoomGraphCanvas".GetHashCode(),FocusType.Passive);
            if(nameStyle==null)
            {
                nameStyle=new GUIStyle(EditorStyles.boldLabel){alignment=TextAnchor.MiddleCenter,fontSize=14};nameStyle.normal.textColor=Color.white;
                small=new GUIStyle(EditorStyles.miniLabel){alignment=TextAnchor.MiddleCenter};small.normal.textColor=new Color(.75f,.83f,.9f);
            }
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                Toolbar();
                canvas=new Rect(314,56,position.width-322,position.height-88);
                GUILayout.BeginArea(new Rect(10,60,291,position.height-94));scroll=EditorGUILayout.BeginScrollView(scroll);Sidebar();EditorGUILayout.EndScrollView();GUILayout.EndArea();
                Graph();
                if(!EditorApplication.isPlayingOrWillChangePlaymode)HandleNavigation();
            }
            EditorGUI.DrawRect(new Rect(0,position.height-26,position.width,26),new Color(.1f,.14f,.18f));
            GUI.Label(new Rect(6,position.height-25,position.width-12,24),status,small);
        }

        void Toolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            var next=(RoomDungeon)EditorGUILayout.ObjectField(dungeon,typeof(RoomDungeon),false,GUILayout.Width(225));
            if(next!=dungeon){EndGraphDrag();DungeonCampaignAuthoring.SavePending(dungeon);dungeon=next;DungeonCampaignAuthoring.Remember(next);selected=next==null?null:next.startRoomId;problems=null;linkSource=null;FitGraph();}
            if(GUILayout.Button("새 던전",EditorStyles.toolbarButton,GUILayout.Width(70)))NewDungeon();
            if(GUILayout.Button("방형 예제",EditorStyles.toolbarButton,GUILayout.Width(80)))ShowDungeon(RoomDungeonAuthoring.CreateExample());
            if(GUILayout.Button("복층 예제",EditorStyles.toolbarButton,GUILayout.Width(80)))ShowDungeon(RoomDungeonPlatforms.CreateExample());
            using(new EditorGUI.DisabledScope(dungeon==null))
            {
                if(GUILayout.Button("복사본",EditorStyles.toolbarButton,GUILayout.Width(65)))Copy();
                if(GUILayout.Button(dungeon!=null&&EditorUtility.IsDirty(dungeon)?"저장 *":"저장",EditorStyles.toolbarButton,GUILayout.Width(65)))Save();
                if(GUILayout.Button("전체 보기",EditorStyles.toolbarButton,GUILayout.Width(75)))FitGraph();
                if(GUILayout.Button("연결 / 배치 검사",EditorStyles.toolbarButton,GUILayout.Width(110))){problems=dungeon.ValidateDungeon();status=problems.Count==0?"연결과 배치 검사 통과":problems.Count+"개 항목을 왼쪽에서 확인해주세요.";}
                if(GUILayout.Button("▶ 던전 테스트",EditorStyles.toolbarButton,GUILayout.Width(110))){Save();DungeonBuilderAssets.PlayRooms(dungeon,body,partySize);}
            }
            GUILayout.FlexibleSpace();GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();GUILayout.Space(12);
            DungeonCampaignAuthoring.Picker(dungeon,asset=>ShowDungeon(asset),170);
            var nextMode=(GraphTool)GUILayout.Toolbar((int)graphTool,new[]{"1 선택 / 이동","2 방 배치","3 연결","4 끊기"},GUILayout.Width(490));
            if(nextMode!=graphTool){graphTool=nextMode;linkSource=null;EndGraphDrag();}
            if(GUILayout.Button("내부 배치",GUILayout.Width(80))&&dungeon!=null)DungeonBuilderWindow.ShowRoom(dungeon,dungeon.FindRoom(selected)!=null?selected:dungeon.startRoomId);
            GUILayout.FlexibleSpace();GUILayout.EndHorizontal();
        }

        void Sidebar()
        {
            if(dungeon==null){EditorGUILayout.HelpBox("상단의 '편집할 던전'에서 제작할 던전을 선택하세요.",MessageType.Info);return;}
            var so=new SerializedObject(dungeon);so.Update();EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("displayName"),new GUIContent("던전 이름"));
            EditorGUILayout.PropertyField(so.FindProperty("allowBacktracking"),new GUIContent("이전 방으로 돌아가기"));
            if(EditorGUI.EndChangeCheck()){so.ApplyModifiedProperties();problems=null;}
            int campaign=DungeonCampaignAuthoring.IndexOf(dungeon);
            if(campaign>0)EditorGUILayout.HelpBox("배치 제작용 던전입니다. 보스는 임시 표시이며, 온라인 입장은 아직 준비 중입니다.",MessageType.None);
            EditorGUILayout.HelpBox("클리어한 방의 몹은 다시 생성되지 않습니다. 관전 중인 사망자는 이동 가능한 파티원들과 함께 다음 방으로 따라갑니다.",MessageType.None);
            EditorGUILayout.HelpBox(GraphHelp,MessageType.Info);
            var room=dungeon.FindRoom(selected);
            GUILayout.Space(8);GUILayout.Label("선택한 방",EditorStyles.boldLabel);
            if(room!=null)
            {
                EditorGUI.BeginChangeCheck();string name=EditorGUILayout.TextField("방 이름",room.displayName);
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(dungeon,"방 이름");room.displayName=name;EditorUtility.SetDirty(dungeon);}
                EditorGUI.BeginChangeCheck();var cell=EditorGUILayout.Vector2IntField("지도 좌표",room.cell);
                if(EditorGUI.EndChangeCheck())MoveSelectedRoom(room,cell);
                if(GUILayout.Button("이 방의 바닥 / 몹 / 포탈 편집",GUILayout.Height(32)))DungeonBuilderWindow.ShowRoom(dungeon,room.id);
                GUILayout.BeginHorizontal();
                using(new EditorGUI.DisabledScope(room.id==dungeon.startRoomId))
                    if(GUILayout.Button(room.id==dungeon.startRoomId?"시작 방":"시작 방 지정")){Undo.RecordObject(dungeon,"시작 방 지정");dungeon.startRoomId=room.id;Changed();}
                using(new EditorGUI.DisabledScope(room.isBoss))
                    if(GUILayout.Button(room.isBoss?"보스 방":"보스 방 지정")){Undo.RecordObject(dungeon,"보스 방 지정");foreach(var r in dungeon.rooms)r.isBoss=r==room;Changed();}
                GUILayout.EndHorizontal();
                GUILayout.Space(10);GUILayout.Label("선택한 방의 통로",EditorStyles.boldLabel);
                foreach(PortalDirection direction in Enum.GetValues(typeof(PortalDirection)))
                {
                    var portal=room.portals.Find(p=>p.direction==direction);GUILayout.BeginHorizontal();
                    if(portal==null)
                    {
                        bool exists=dungeon.rooms.Any(r=>r.cell==room.cell+RoomPortal.Offset(direction));
                        if(GUILayout.Button(RoomPortal.Label(direction)+(exists?" · 연결하기":" · 방만 배치"),GUILayout.Height(26)))
                        {
                            if(exists)ConnectPair(room,dungeon.rooms.Find(r=>r.cell==room.cell+RoomPortal.Offset(direction)));
                            else PlaceRoom(room.cell+RoomPortal.Offset(direction));
                        }
                    }
                    else
                    {
                        var target=dungeon.FindRoom(portal.targetRoomId);
                        if(GUILayout.Button(RoomPortal.Label(direction)+"  "+(target==null?"연결 오류":target.displayName),GUILayout.Height(26))&&target!=null)selected=target.id;
                        if(GUILayout.Button("×",GUILayout.Width(25),GUILayout.Height(26)))DisconnectPair(room,portal);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(8);
                using(new EditorGUI.DisabledScope(dungeon.rooms.Count<=1))if(GUILayout.Button("선택한 방 삭제 (되돌리기 가능)")){RoomDungeonAuthoring.RemoveRoom(dungeon,room);selected=dungeon.startRoomId;Changed();}
            }
            GUILayout.Space(12);GUILayout.Label("이동 / 집결 테스트",EditorStyles.boldLabel);
            body=EditorGUILayout.Popup("캐릭터",body,new[]{"남성","여성"});
            partySize=EditorGUILayout.IntSlider("모의 파티 인원",partySize,1,campaign==4?6:4);
            EditorGUILayout.HelpBox("F6: 현재 방 몹 처치 처리\nS: 포탈 진입 / 나가기 · G: 모의 동료 진입\n배치와 방 이동을 확인하는 로컬 테스트입니다.",MessageType.None);
            GUILayout.Label("방 "+dungeon.rooms.Count+"개 · 연결 "+dungeon.rooms.Sum(r=>r.portals.Count)/2+"개",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("더블클릭: 방 내부 편집\n휠: 확대 / 축소 · 중간 버튼: 화면 이동\nCtrl+S: 저장 · Ctrl+Z: 되돌리기",MessageType.None);
            foreach(var warning in dungeon.ConnectionWarnings())EditorGUILayout.HelpBox(warning,MessageType.Warning);
            if(problems!=null)
            {
                if(problems.Count==0)EditorGUILayout.HelpBox("검사 통과",MessageType.Info);
                foreach(var error in problems.Distinct())EditorGUILayout.HelpBox(error,MessageType.Warning);
            }
        }

        void Changed(){EditorUtility.SetDirty(dungeon);problems=null;Repaint();}
        void Save(){if(dungeon==null)return;RoomDungeonAuthoring.Save(dungeon);status="저장 완료 · "+AssetDatabase.GetAssetPath(dungeon);}
        void NewDungeon()
        {
            string path=EditorUtility.SaveFilePanelInProject("방형 던전 만들기","NewRoomDungeon","asset","던전 전체를 하나의 에셋에 저장합니다.","Assets/Data");
            if(string.IsNullOrEmpty(path)||AssetDatabase.LoadMainAssetAtPath(path)!=null)return;
            var asset=RoomDungeonAuthoring.Create(path,"새 던전");var first=RoomDungeonAuthoring.AddRoom(asset,Vector2Int.zero,"시작 방");
            var boss=RoomDungeonAuthoring.AddRoom(asset,Vector2Int.right,"보스 방");boss.isBoss=true;
            RoomDungeonAuthoring.Save(asset);ShowDungeon(asset);
        }
        void Copy()
        {
            string path=EditorUtility.SaveFilePanelInProject("던전 복사본",dungeon.name+"_Copy","asset","방 배치까지 독립적으로 복사합니다.","Assets/Data");
            if(string.IsNullOrEmpty(path)||AssetDatabase.LoadMainAssetAtPath(path)!=null)return;
            ShowDungeon(RoomDungeonAuthoring.Copy(dungeon,path));
        }
    }
}
