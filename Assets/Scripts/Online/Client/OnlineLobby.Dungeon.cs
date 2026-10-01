using MiniWar.Dungeons;
using UnityEngine;

namespace MiniWar.Online
{
    public sealed partial class OnlineLobby
    {
        Transform townRoot, dungeonRoot;
        LanDungeonVisit dungeonVisit;
        DungeonLayout onlineLayout;
        DungeonRoom onlineRoom;
        DungeonCombatView combatView;
        readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<SpriteRenderer>> onlinePortals = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<SpriteRenderer>>();
        bool InDungeon => dungeonVisit != null;
        float WorldGround => InDungeon ? 0 : Ground;

        void SyncDungeon(LanDungeonVisit visit)
        {
            bool sameRoom = dungeonVisit?.instanceId == visit?.instanceId && dungeonVisit?.roomSequence == visit?.roomSequence;
            dungeonVisit = visit;
            if (sameRoom) { RefreshOnlinePortals(); RefreshCombat(); return; }
            SetPartyPanel(false); inventory = false; chat.Clear(); ClearShots();
            foreach (var actor in actors.Values) { actor.gameObject.SetActive(false); Destroy(actor.gameObject); }
            actors.Clear(); positions.Clear(); names.Clear(); views.Clear();
            if (townRoot != null) townRoot.gameObject.SetActive(!InDungeon);
            if (dungeonRoot != null) { dungeonRoot.gameObject.SetActive(false); Destroy(dungeonRoot.gameObject); }
            combatView?.Dispose();combatView=null;
            onlineLayout = null; onlineRoom = null; onlinePortals.Clear();
            worldCamera.orthographicSize = InDungeon ? 5.5f : 6;
            if (!InDungeon) { worldCamera.transform.position = new Vector3(LanRules.TownSpawnX, 0, -10); return; }
            var bindings = Resources.Load<OnlineDungeonAssets>("Online/Dungeons/Bindings");
            var room = bindings?.gateOutskirts?.FindRoom(visit.roomId);
            if (room == null || visit.revision != OnlineDungeonAssets.Revision())
            { notice = "맵 버전이 다릅니다. 최신 게임으로 다시 접속해 주세요."; return; }
            onlineLayout = room.layout;
            onlineRoom = room;
            dungeonRoot = new GameObject("Party dungeon geometry").transform;
            worldCamera.backgroundColor = onlineLayout.backgroundColor;
            if (onlineLayout.background != null)
                DungeonBox("Dungeon background", onlineLayout.bounds, new Color(.65f,.75f,.86f,.28f), -20, onlineLayout.background);
            foreach (var f in onlineLayout.floors)
            {
                if (DungeonStoneSurface.Build(dungeonRoot, f, -1)) continue;
                DungeonBox("Floor " + f.id, f.rect, f.color, -1, f.sprite);
                DungeonBox("Floor top", new Rect(f.rect.xMin, f.rect.yMax - .06f, f.rect.width, .06f), f.oneWay ? new Color(.4f,.93f,.75f) : new Color(.69f,.76f,.78f), 0);
                if (f.sprite == null)
                    for (float x = f.rect.xMin + 1; x < f.rect.xMax; x++)
                        DungeonBox("Stone joint", new Rect(x, f.rect.yMin, .025f, Mathf.Max(.01f,f.rect.height - .06f)), f.color * .65f, 0);
            }
            combatView=new DungeonCombatView(dungeonRoot,square);
            foreach (var spawn in onlineLayout.spawns)
                for (int i = 0; i < Mathf.Clamp(spawn.count, 1, 30); i++)
                    combatView.Bind(spawn.id+":"+i,DungeonBox(spawn.Label, spawn.VisualRectAt(i), spawn.VisualSprite == null ? new Color(.9f,.57f,.27f) : Color.white, 2, spawn.VisualSprite, spawn.faceLeft));
            foreach (var portal in room.portals)
            {
                var pos = portal.position;
                var grey = new Color(.45f,.55f,.65f);
                onlinePortals[portal.id] = new System.Collections.Generic.List<SpriteRenderer> {
                    DungeonBox("Portal threshold", new Rect(pos.x-.6f,pos.y,1.2f,.07f), grey, 1),
                    DungeonBox("Portal", new Rect(pos.x-.6f,pos.y,.07f,2.8f), grey, 1),
                    DungeonBox("Portal", new Rect(pos.x+.53f,pos.y,.07f,2.8f), grey, 1),
                    DungeonBox("Portal", new Rect(pos.x-.6f,pos.y+2.73f,1.2f,.07f), grey, 1),
                    DungeonBox("Portal glow", new Rect(pos.x-.53f,pos.y,1.06f,2.73f), new Color(.2f,.8f,.7f,.14f), -1)
                };
            }
            RefreshOnlinePortals();
            RefreshCombat();
            FollowDungeonCamera(onlineLayout.entrance, true);
        }
        void RefreshCombat(){if(dungeonVisit!=null)combatView?.Refresh(dungeonVisit.enemies,dungeonVisit.projectiles);}
        void DrawCombatHUD()
        {
            if(!InDungeon||snapshot==null||partyPanel)return;
            var self=System.Array.Find(snapshot.actors,p=>p.nickname==profile.nickname);if(self==null)return;
            GUI.Box(new Rect(25,132,292,88),GUIContent.none,uiSkin.Window);
            GUI.Label(new Rect(40,143,270,28),$"HP {Mathf.CeilToInt(self.hp)} / 100",label);
            GUI.Label(new Rect(40,177,270,26),dungeonVisit.completed?"던전 클리어! · P 마을 복귀":$"남은 몬스터 {dungeonVisit.enemiesRemaining}마리",small);
            if(self.dead)
            {
                GUI.Box(new Rect(420,300,460,90),GUIContent.none,uiSkin.Window);
                GUI.Label(new Rect(438,318,424,50),self.reviveUsed?"동료 관전 중 · 부활 기회 소진":"쓰러졌습니다 · R 부활 (던전당 1회)",label);
            }
        }

        SpriteRenderer DungeonBox(string partName, Rect rect, Color color, int order, Sprite sprite = null, bool flip = false)
        {
            var go = new GameObject(partName); go.transform.SetParent(dungeonRoot, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite == null ? square : sprite;
            sr.color = color; sr.sortingOrder = order; sr.flipX = flip;
            go.transform.localScale = new Vector3(rect.width / sr.sprite.bounds.size.x, rect.height / sr.sprite.bounds.size.y, 1);
            go.transform.position = new Vector3(rect.center.x, rect.center.y, 0) - Vector3.Scale(sr.sprite.bounds.center, go.transform.localScale);
            return sr;
        }
        void RefreshOnlinePortals()
        {
            if (dungeonVisit == null) return;
            foreach (var portal in dungeonVisit.portals)
            {
                if (!onlinePortals.TryGetValue(portal.id, out var renderers)) continue;
                var color = !portal.open ? new Color(.55f,.3f,.3f) : portal.id == dungeonVisit.countingPortalId
                    ? new Color(1,.78f,.3f) : new Color(.35f,.83f,.67f);
                for (int i = 0; i < renderers.Count; i++) { var c = color; if (i == renderers.Count - 1) c.a = .18f; renderers[i].color = c; }
            }
        }
        bool HandlePortalInput(UnityEngine.InputSystem.Keyboard kb, bool allowed)
        {
            if (!InDungeon || snapshot == null) return false;
            var self = System.Array.Find(snapshot.actors, a => a.nickname == profile.nickname);
            bool waiting = self != null && !string.IsNullOrEmpty(self.portalId);
            if (allowed && kb.sKey.wasPressedThisFrame && !kb.spaceKey.isPressed && self != null && !self.dead && onlineRoom != null)
            {
                var portal = waiting ? onlineRoom.portals.Find(p => p.id == self.portalId)
                    : onlineRoom.portals.Find(p => p.Contains(new Vector2(self.x,self.y)));
                if (portal != null) client.Send(new LanCommand { op = "portal_enter", portalId = portal.id,
                    roomId = dungeonVisit.roomId, roomSequence = dungeonVisit.roomSequence });
            }
            return waiting || (self != null && self.dead);
        }
        void DrawOnlinePortals()
        {
            if (!InDungeon || onlineRoom == null || partyPanel) return;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            foreach (var portal in onlineRoom.portals)
            {
                var state = System.Array.Find(dungeonVisit.portals, p => p.id == portal.id);
                if (state == null) continue;
                var screen = worldCamera.WorldToScreenPoint(portal.position + Vector2.up * 3.15f);
                float x = (screen.x - (Screen.width - 1280*scale)/2)/scale;
                float y = (Screen.height-screen.y - (Screen.height-720*scale)/2)/scale;
                if (x < -130 || x > 1410 || y < 66 || y > 620) continue;
                var rect = new Rect(x-140,y-21,280,57);
                GUI.Box(rect,GUIContent.none,uiSkin.Slot);
                var centered = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(rect.x,rect.y+3,rect.width,23),RoomPortal.Label(portal.direction)+" · "+state.targetName,centered);
                GUI.Label(new Rect(rect.x,rect.y+28,rect.width,24),state.open ? $"[S] 진입 / 나가기 · {state.entered}/{dungeonVisit.aliveCount}명" : "방 클리어 후 열립니다",centered);
            }
            if (!string.IsNullOrEmpty(dungeonVisit.countingPortalId))
            {
                GUI.Box(new Rect(400,95,480,84),GUIContent.none,uiSkin.Window);
                var state = System.Array.Find(dungeonVisit.portals,p=>p.id==dungeonVisit.countingPortalId);
                GUI.Label(new Rect(420,110,440,30),$"{Mathf.CeilToInt(dungeonVisit.countdown)}초 후 {state?.targetName}으로 이동",label);
                GUI.Label(new Rect(420,145,440,24),"살아 있는 동료가 모두 들어오면 즉시 출발합니다.",small);
            }
        }
        void FollowDungeonCamera(Vector3 feet, bool instant = false)
        {
            if (onlineLayout == null) return;
            var bounds = onlineLayout.bounds;
            float height = worldCamera.orthographicSize, half = height * worldCamera.aspect;
            float x = bounds.width <= half * 2 ? bounds.center.x : Mathf.Clamp(feet.x + 2, bounds.xMin + half, bounds.xMax - half);
            float y = bounds.height <= height * 2 ? bounds.center.y : Mathf.Clamp(feet.y + 2, bounds.yMin + height, bounds.yMax - height);
            var target = new Vector3(x, y, -10);
            worldCamera.transform.position = instant ? target : Vector3.Lerp(worldCamera.transform.position, target, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
        }
    }
}
