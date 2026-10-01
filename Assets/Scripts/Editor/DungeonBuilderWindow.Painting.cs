using System.Collections.Generic;
using System.Linq;
using MiniWar.Data;
using MiniWar.Dungeons;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public sealed partial class DungeonBuilderWindow
    {
        public const string WorkingMapPath="Assets/Data/RoomDungeons/MyDungeon.asset";
        [SerializeField] bool brushLine, settingsOpen, monsterStroke=true;
        [SerializeField] float brushThickness=.5f, monsterSpacing=2;
        readonly List<Vector2> strokeFeet=new List<Vector2>();
        Vector2 hoverWorld,lastStrokePoint;
        float strokeDistance;
        bool hoverInside;
        static readonly string[] PaletteNames={"Patrol","DaggerGuard","CrossbowGuard","DarkMage","GateCannon"};

        public static RoomDungeon GetWorkingMap()
        {
            var asset=AssetDatabase.LoadAssetAtPath<RoomDungeon>(WorkingMapPath);
            if(asset!=null)return asset;
            asset=RoomDungeonAuthoring.Copy(RoomDungeonPlatforms.CreateExample(),WorkingMapPath);
            asset.displayName="내 던전 · 성문 외곽";
            RoomDungeonAuthoring.Save(asset);return asset;
        }

        [MenuItem("MiniWar/맵 배치 그리기")]
        public static void OpenPainting()
        {
            var asset=DungeonCampaignAuthoring.Last();
            var existing=Resources.FindObjectsOfTypeAll<DungeonBuilderWindow>().FirstOrDefault();
            string id=existing!=null&&existing.roomDungeon==asset&&asset.FindRoom(existing.roomId)!=null?existing.roomId:asset.startRoomId;
            var window=ShowRoom(asset,id);
            window.tool=Tool.Floor;window.brushLine=true;window.brushOneWay=true;
            window.brushThickness=.5f;window.selected=null;window.scroll=Vector2.zero;
            window.status=asset.displayName+" · 상단에서 던전을 선택하세요. 바닥은 드래그, 몬스터는 클릭, 우클릭은 지우기입니다.";
        }

        void DrawRoomPicker()
        {
            GUILayout.Label("편집할 방",GUILayout.Width(65));
            int index=roomDungeon.rooms.FindIndex(r=>r.id==roomId);
            int next=EditorGUILayout.Popup(index,roomDungeon.rooms.Select(r=>(r.isBoss?"[보스] ":"")+r.displayName).ToArray(),GUILayout.Width(220));
            if(next!=index&&next>=0)
            {
                var dungeon=roomDungeon;var nextTool=tool;
                ShowRoom(dungeon,dungeon.rooms[next].id);tool=nextTool;scroll=Vector2.zero;hoverInside=false;
            }
        }

        void DrawFloorBrush()
        {
            GUILayout.Space(5);GUILayout.BeginHorizontal();
            if(GUILayout.Button("바닥 / 벽")){brushOneWay=false;brushLine=false;}
            if(GUILayout.Button("점프 발판")){brushOneWay=true;brushLine=true;brushThickness=.5f;}
            GUILayout.EndHorizontal();
            brushLine=EditorGUILayout.Popup("그리는 방식",brushLine?1:0,new[]{"사각형 영역 드래그","가로로 한 줄 그리기"})==1;
            if(brushLine)
            {
                brushThickness=EditorGUILayout.Slider("두께 (m)",brushThickness,.25f,3);
                EditorGUILayout.HelpBox("시작한 높이가 발판 윗면입니다. 좌우로 끌면 일정한 두께로 그려집니다.",MessageType.None);
            }
        }

        void DrawMonsterPalette()
        {
            GUILayout.Space(5);GUILayout.Label("놓을 몬스터를 골라주세요",EditorStyles.boldLabel);
            for(int row=0;row<2;row++)
            {
                GUILayout.BeginHorizontal();
                for(int col=0;col<3;col++)
                {
                    int i=row*3+col;if(i>=PaletteNames.Length)break;
                    var enemy=enemies.FirstOrDefault(e=>e.name==PaletteNames[i]);if(enemy==null)continue;
                    var rect=GUILayoutUtility.GetRect(68,84,GUILayout.ExpandWidth(true));
                    if(GUI.Button(rect,GUIContent.none))
                    {EndDrag();brushEnemy=enemy;brushMonsterSprite=null;selected=null;Repaint();}
                    if(brushEnemy==enemy)Outline(rect,Color.cyan,2);
                    var sprite=enemy.idleSprite;
                    if(sprite!=null)
                    {
                        float h=57,w=Mathf.Min(rect.width-8,h*sprite.rect.width/sprite.rect.height);
                        h=w*sprite.rect.height/sprite.rect.width;
                        GUI.DrawTextureWithTexCoords(new Rect(rect.center.x-w/2,rect.y+5+(57-h),w,h),sprite.texture,SpriteUV(sprite));
                    }
                    else GUI.Label(new Rect(rect.x,rect.y+14,rect.width,40),enemy.isBoss?"BOSS":"?",small);
                    GUI.Label(new Rect(rect.x+2,rect.yMax-22,rect.width-4,18),enemy.displayName,small);
                }
                GUILayout.EndHorizontal();
            }
        }

        Rect FloorBrushRect(Vector2 from,Vector2 to)
        {
            float bottom=brushLine?from.y-Mathf.Max(.25f,brushThickness):Mathf.Min(from.y,to.y);
            float top=brushLine?from.y:Mathf.Max(from.y,to.y);
            var bounds=layout.bounds;
            return Rect.MinMaxRect(Mathf.Clamp(Mathf.Min(from.x,to.x),bounds.xMin,bounds.xMax),
                Mathf.Clamp(bottom,bounds.yMin,bounds.yMax),Mathf.Clamp(Mathf.Max(from.x,to.x),bounds.xMin,bounds.xMax),
                Mathf.Clamp(top,bounds.yMin,bounds.yMax));
        }

        void BeginStroke(int mode,string label)
        {
            dragMode=mode;Undo.IncrementCurrentGroup();dragGroup=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);Undo.RegisterCompleteObjectUndo(UndoTargets,label);
        }

        DungeonSpawn BrushSpawn(Vector2 point)=>new DungeonSpawn {enemy=brushEnemy,sprite=brushMonsterSprite,
            position=PlaceFeet(point,brushEnemy==null||brushEnemy.locomotion==LocomotionKind.Ground)};

        void StampMonster(Vector2 point)
        {
            if(brushEnemy==null)return;
            var spawn=BrushSpawn(gridSnap?layout.Snap(point):point);
            if(!DungeonLayout.ContainsRect(layout.bounds,new Rect(spawn.position.x-spawn.Size.x/2,spawn.position.y,spawn.Size.x,spawn.Size.y)))
            {status="몬스터의 몸이 맵 범위 안에 들어오도록 놓아주세요.";return;}
            // Snapping a vertical stroke onto one floor must not stack duplicate enemies.
            if(strokeFeet.Any(p=>Vector2.Distance(p,spawn.position)<Mathf.Max(.5f,monsterSpacing)*.8f))return;
            strokeFeet.Add(spawn.position);layout.spawns.Add(spawn);selected=spawn.id;Changed();
            status=SpawnSupported(spawn)?"몬스터 배치 · 드래그로 이어서 놓기 / Ctrl+Z로 이번 그리기 되돌리기":"배치됨 · 발밑 바닥이나 벽 겹침을 확인해주세요.";
        }

        void PaintMonstersTo(Vector2 point)
        {
            float length=Vector2.Distance(lastStrokePoint,point),spacing=Mathf.Max(1,monsterSpacing);
            if(length<.001f)return;
            var direction=(point-lastStrokePoint)/length;
            float offset=spacing-strokeDistance;
            for(;offset<=length;offset+=spacing)StampMonster(lastStrokePoint+direction*offset);
            strokeDistance=(strokeDistance+length)%spacing;lastStrokePoint=point;
        }

        void EraseAt(Vector2 point)
        {
            string id=HitTest(point);if(string.IsNullOrEmpty(id)||id.StartsWith("$"))return;
            id=IndividualMonsterAt(id,point);
            layout.floors.RemoveAll(f=>f.id==id);layout.spawns.RemoveAll(s=>s.id==id);
            if(selected==id)selected=null;Changed();status="배치 지움 · Ctrl+Z로 이번 드래그 전체 되돌리기";
        }

        void ClearGroupedSelection()
        {
            if(layout!=null&&layout.spawns.Any(s=>s.id==selected&&s.count>1))selected=null;
        }

        // Legacy layouts store evenly spaced monsters in one spawn. Materialize that
        // group only when edited, inside the caller's undo transaction, at identical feet positions.
        string IndividualMonsterAt(string id,Vector2? hitPoint)
        {
            var group=layout.spawns.Find(s=>s.id==id);
            if(group==null||group.count<=1)return id;
            int index=layout.spawns.IndexOf(group),member=0,count=group.count;
            if(hitPoint.HasValue)
                for(int i=0;i<count;i++)if(group.VisualRectAt(i).Contains(hitPoint.Value)){member=i;break;}
            var copies=new List<DungeonSpawn>();
            string source=JsonUtility.ToJson(group);
            for(int i=1;i<count;i++)
            {
                var copy=JsonUtility.FromJson<DungeonSpawn>(source);
                copy.id=System.Guid.NewGuid().ToString("N");copy.position=group.PositionAt(i);copy.count=1;
                copies.Add(copy);
            }
            group.count=1;layout.spawns.InsertRange(index+1,copies);Changed();
            status="몬스터는 한 마리씩 편집됩니다. 다른 몬스터의 위치는 그대로 유지됩니다.";
            return member==0?id:copies[member-1].id;
        }

        void EraseTo(Vector2 point)
        {
            float length=Vector2.Distance(lastStrokePoint,point);
            int samples=Mathf.Clamp(Mathf.CeilToInt(length/.15f),1,2048);
            for(int i=1;i<=samples;i++)EraseAt(Vector2.Lerp(lastStrokePoint,point,(float)i/samples));
            lastStrokePoint=point;
        }

        void CancelStroke()
        {
            int mode=dragMode,group=dragGroup;EndDrag();
            if(mode==2||mode==3||mode==5||mode==6)Undo.RevertAllDownToGroup(group);
            status="그리기 취소";
        }

        bool SpawnSupported(DungeonSpawn spawn)=>
            DungeonLayout.ContainsRect(layout.bounds,new Rect(spawn.position.x-spawn.Size.x/2,spawn.position.y,spawn.Size.x,spawn.Size.y))&&
            (spawn.enemy.locomotion!=LocomotionKind.Ground||layout.HasSupport(spawn.position,spawn.Size.x/2))&&layout.HasClearance(spawn.position,spawn.Size);

        void DrawBrushPreview()
        {
            if(!hoverInside||dragMode!=0||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(tool==Tool.Monster&&brushEnemy!=null)
            {
                var spawn=BrushSpawn(hoverWorld);var rect=WorldRect(spawn.VisualRectAt(0));
                var color=SpawnSupported(spawn)?new Color(.35f,1,.68f):new Color(1,.4f,.36f);
                var sprite=spawn.VisualSprite;
                if(sprite!=null)
                {
                    var uv=SpriteUV(sprite);uv.x+=uv.width;uv.width=-uv.width;
                    GUI.color=new Color(1,1,1,.6f);GUI.DrawTextureWithTexCoords(rect,sprite.texture,uv);GUI.color=Color.white;
                }
                Outline(rect,color,2);var feet=WorldToCanvas(spawn.position);
                EditorGUI.DrawRect(new Rect(feet.x-8,feet.y-1,16,2),color);
            }
            else if(tool==Tool.Floor)
            {
                var p=WorldToCanvas(hoverWorld);
                EditorGUI.DrawRect(new Rect(p.x-9,p.y,18,1),Color.cyan);
                EditorGUI.DrawRect(new Rect(p.x,p.y-9,1,18),Color.cyan);
            }
        }
    }
}
