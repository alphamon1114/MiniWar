using System.Linq;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class RoomDungeonPlatforms
    {
        public const string ExamplePath="Assets/Data/RoomDungeons/GateOutskirtsPlatforms.asset";

        [MenuItem("MiniWar/복층 던전 예제")]
        public static void Open()=>RoomDungeonBuilderWindow.ShowDungeon(CreateExample());

        public static RoomDungeon CreateExample()
        {
            var existing=AssetDatabase.LoadAssetAtPath<RoomDungeon>(ExamplePath);
            if(existing!=null)return existing;
            // Keep the approved topology and its editable original; layer geometry lives in its own asset.
            var dungeon=RoomDungeonAuthoring.Copy(RoomDungeonAuthoring.CreateExample(),ExamplePath);
            dungeon.displayName="성문 외곽 · 복층 탈환";
            foreach(var room in dungeon.rooms)Populate(room);
            RoomDungeonAuthoring.Save(dungeon);
            return dungeon;
        }

        public static void Populate(DungeonRoom room)
        {
            var layout=room.layout;
            layout.bounds=new Rect(0,-2,48,16);layout.entrance=new Vector2(4,0);layout.exit=new Vector2(45,3);
            layout.jumpHeight=3.5f;
            layout.description="가로 48m의 긴 방. 왼쪽 진입 → 중앙 교전 → 오른쪽 출구로 진행합니다.\n지상·3m·6m 발판을 오가세요. Space 점프, ↓+Space 하강. 몹 8마리 처치 후 포탈에 집결합니다.";
            layout.floors.Clear();
            layout.floors.Add(new DungeonFloor {rect=new Rect(0,-2,48,2)});
            Platform(layout,2,3,10);
            Platform(layout,18,3,11);
            Platform(layout,8,6,14);
            if(!room.isBoss)
            {
                Platform(layout,35,3,11);
                Platform(layout,27,6,13);
            }

            var enemies=DungeonBuilderAssets.CreateGateExample().spawns;
            var patrol=enemies.First().enemy;var guard=enemies[1].enemy;
            layout.spawns.Clear();
            if(room.isBoss)
            {
                layout.spawns.Add(new DungeonSpawn {enemy=enemies.Last().enemy,position=new Vector2(40,0)});
                layout.spawns.Add(new DungeonSpawn {enemy=patrol,position=new Vector2(22,0),count=2,spacing=7});
                layout.spawns.Add(new DungeonSpawn {enemy=patrol,position=new Vector2(5,3),count=3,spacing=3});
                layout.spawns.Add(new DungeonSpawn {enemy=guard,position=new Vector2(13,6),count=2,spacing=5});
            }
            else
            {
                layout.spawns.Add(new DungeonSpawn {enemy=patrol,position=new Vector2(8,0),count=2,spacing=17});
                layout.spawns.Add(new DungeonSpawn {enemy=patrol,position=new Vector2(7,3)});
                layout.spawns.Add(new DungeonSpawn {enemy=patrol,position=new Vector2(23,3)});
                layout.spawns.Add(new DungeonSpawn {enemy=guard,position=new Vector2(38,3),count=2,spacing=5});
                layout.spawns.Add(new DungeonSpawn {enemy=guard,position=new Vector2(17,6)});
                layout.spawns.Add(new DungeonSpawn {enemy=guard,position=new Vector2(34,6)});
            }
            foreach(var portal in room.portals)
            {
                switch(portal.direction)
                {
                    case PortalDirection.Left: portal.position=new Vector2(1.5f,0);portal.arrival=new Vector2(4,0);break;
                    case PortalDirection.Right: portal.position=new Vector2(45,3);portal.arrival=new Vector2(42.5f,3);break;
                    case PortalDirection.Up: portal.position=new Vector2(31,6);portal.arrival=new Vector2(33,6);break;
                    case PortalDirection.Down: portal.position=new Vector2(24,0);portal.arrival=new Vector2(26,0);break;
                }
            }
            EditorUtility.SetDirty(layout);
        }

        static void Platform(DungeonLayout layout,float x,float top,float width)
        {layout.floors.Add(new DungeonFloor {rect=new Rect(x,top-.5f,width,.5f),oneWay=true,color=new Color(.29f,.4f,.42f)});}
    }
}
