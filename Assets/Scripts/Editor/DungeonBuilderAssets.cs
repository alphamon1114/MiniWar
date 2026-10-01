using System;
using System.IO;
using MiniWar.Data;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniWar.EditorTools
{
    [InitializeOnLoad]
    public static class DungeonBuilderAssets
    {
        public const string ExamplePath = "Assets/Data/DungeonLayouts/GateOutskirts.asset";
        public const string SandboxPath = "Assets/Scenes/Tools/DungeonPlaytest.unity";
        const string RestoreKey = "MiniWar.DungeonBuilder.RestoreStartScene", PreviousKey = "MiniWar.DungeonBuilder.PreviousStartScene";

        static DungeonBuilderAssets()
        {
            EditorApplication.playModeStateChanged += state => {
                if(state == PlayModeStateChange.EnteredEditMode) RestoreStartScene();
            };
            // Also recover after a failed entry into Play mode or a domain reload in edit mode.
            EditorApplication.delayCall += () => { if(!EditorApplication.isPlayingOrWillChangePlaymode) RestoreStartScene(); };
        }

        static void RestoreStartScene()
        {
            if(!SessionState.GetBool(RestoreKey,false))return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousKey,""));
            SessionState.SetBool(RestoreKey,false);
        }

        public static DungeonLayout CreateGateExample()
        {
            var existing=AssetDatabase.LoadAssetAtPath<DungeonLayout>(ExamplePath);
            if(existing!=null)return existing;
            Directory.CreateDirectory("Assets/Data/DungeonLayouts/Enemies"); AssetDatabase.Refresh();
            var patrol=MakeEnemy("Patrol","순찰대원",new Vector2(.8f,1.9f),false,100);
            var crossbow=MakeEnemy("CrossbowGuard","석궁 경비병",new Vector2(.8f,1.9f),false,120);
            var cannon=MakeEnemy("GateCannon","성문 대포",new Vector2(4,2.8f),true,1800);
            var layout=ScriptableObject.CreateInstance<DungeonLayout>();
            layout.displayName="성문 외곽";
            layout.description="탈환 작전의 시작. 무너진 성벽 밖을 지나 성문에 고정된 대포를 무력화한다.\n현재는 동선과 몬스터 배치를 잡는 뼈대 예제.";
            layout.bounds=new Rect(0,-4,64,16);layout.entrance=new Vector2(3,0);layout.exit=new Vector2(61,0);
            layout.background=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Backdrop/BG_Castle.png");
            layout.floors.Add(new DungeonFloor {rect=new Rect(0,-2,24,2)});
            layout.floors.Add(new DungeonFloor {rect=new Rect(26,-2,38,2)});
            layout.floors.Add(new DungeonFloor {rect=new Rect(10,0,4,1)});
            layout.floors.Add(new DungeonFloor {rect=new Rect(16,0,5,1)});
            layout.floors.Add(new DungeonFloor {rect=new Rect(33,0,4,1)});
            layout.floors.Add(new DungeonFloor {rect=new Rect(39,0,4,1)});
            layout.spawns.Add(new DungeonSpawn {enemy=patrol,position=new Vector2(7,0),count=2,spacing=1.5f});
            layout.spawns.Add(new DungeonSpawn {enemy=crossbow,position=new Vector2(18,1),count=2,spacing=1.5f});
            layout.spawns.Add(new DungeonSpawn {enemy=patrol,position=new Vector2(29,0),count=2,spacing=1.5f});
            layout.spawns.Add(new DungeonSpawn {enemy=crossbow,position=new Vector2(40,1),count=2,spacing=1.5f});
            layout.spawns.Add(new DungeonSpawn {enemy=cannon,position=new Vector2(55,0)});
            AssetDatabase.CreateAsset(layout,ExamplePath);AssetDatabase.SaveAssetIfDirty(layout);
            return layout;
        }

        static EnemyData MakeEnemy(string file,string label,Vector2 size,bool boss,float health)
        {
            string path="Assets/Data/DungeonLayouts/Enemies/"+file+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<EnemyData>(path);if(existing!=null)return existing;
            var enemy=ScriptableObject.CreateInstance<EnemyData>();
            enemy.displayName=label;enemy.bodySize=size;enemy.isBoss=boss;enemy.baseHealth=health;enemy.moveSpeed=boss?0:1.5f;
            AssetDatabase.CreateAsset(enemy,path);return enemy;
        }

        public static void Play(DungeonLayout layout,int body)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || layout==null)return;
            var problems=layout.ValidateLayout();
            if(problems.Count>0)
            {
                DungeonBuilderWindow.ShowLayout(layout).ShowNotification(new GUIContent("'배치 검사'에서 잘못된 배치를 먼저 확인해주세요."));
                Debug.LogWarning("Dungeon playtest not started: "+string.Join(" / ",problems)); return;
            }
            AssetDatabase.SaveAssetIfDirty(layout);
            PrepareSandbox(layout,body);
            StartPlay();
        }

        public static void PlayRooms(RoomDungeon dungeon,int body,int partySize,string initialRoom=null)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||dungeon==null)return;
            var errors=dungeon.ValidateDungeon();
            if(errors.Count>0)
            {
                RoomDungeonBuilderWindow.ShowDungeon(dungeon).ShowNotification(new GUIContent("연결 / 배치 검사의 오류를 먼저 수정해주세요."));
                Debug.LogWarning(string.Join(" / ",errors));return;
            }
            RoomDungeonAuthoring.Save(dungeon);
            var room=dungeon.FindRoom(initialRoom??dungeon.startRoomId);
            PrepareSandbox(room.layout,body,dungeon,partySize,room.id);StartPlay();
        }

        static void StartPlay()
        {
            SessionState.SetString(PreviousKey,EditorSceneManager.playModeStartScene==null?"":AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(RestoreKey,true);
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SandboxPath);
            EditorApplication.EnterPlaymode();
        }

        public static void PrepareSandbox(DungeonLayout layout,int body=0,RoomDungeon dungeon=null,int partySize=1,string initialRoom=null)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before preparing a dungeon test.");
            Directory.CreateDirectory("Assets/Scenes/Tools");AssetDatabase.Refresh();
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                var go=new GameObject("Dungeon placement test");SceneManager.MoveGameObjectToScene(go,scene);
                var sandbox=go.AddComponent<DungeonSandbox>();sandbox.layout=layout;sandbox.characterBody=Mathf.Clamp(body,0,1);
                sandbox.dungeon=dungeon;sandbox.partySize=Mathf.Clamp(partySize,1,6);sandbox.initialRoom=initialRoom;
                if(!EditorSceneManager.SaveScene(scene,SandboxPath))throw new IOException("Cannot save dungeon sandbox scene.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            }
        }
    }
}
