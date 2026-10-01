using System;
using UnityEngine;

namespace MiniWar.Online
{
    public sealed partial class OnlineLobby
    {
        LanEvent partyState;
        bool partyPanel, partyTitleFocused, partyFocusReset;
        string partyTitle = "함께 왕국을 탈환해요", partyNotice = "";
        Vector2 partyScroll;
        float partySendAt;
        int partyPage;
        string selectedDungeon = LanDungeons.Gate;
        GUIStyle partyAction, mapName, mapTiny, dungeonHeading;
        Texture2D campaignMap;
        LanParty MyParty => partyState == null ? null : partyState.party;
        bool IsPartyLeader => MyParty != null && MyParty.leader == profile.nickname;
        bool HasApplication => partyState != null && !string.IsNullOrEmpty(partyState.pendingPartyId);

        void SetPartyPanel(bool open)
        {
            partyPanel = open;
            if (open) { inventory = false; if (MyParty != null) { selectedDungeon = MyParty.dungeonId; partyPage = 1; } }
            chatFocused = false; partyTitleFocused = false; partyFocusReset = true;
        }

        // Shared by UI and the opt-in Windows integration check.
        void SendParty(string op, string partyId = null, string applicationId = null)
        {
            if (client == null || Time.unscaledTime < partySendAt) return;
            partySendAt = Time.unscaledTime + .25f;
            client.Send(new LanCommand { op = op, partyId = partyId, applicationId = applicationId, text = partyTitle,
                dungeonId = selectedDungeon, dungeonRevision = op == "party_start" ? OnlineDungeonAssets.Revision() : null });
        }

        void DrawPartySummary()
        {
            if (partyFocusReset) { GUI.FocusControl(""); partyFocusReset = false; }
            int waiting = IsPartyLeader ? MyParty.applications.Length : 0;
            if (GUI.Button(new Rect(1035, 76, 220, 40), waiting > 0 ? $"파티 [P] · 신청 {waiting}" : "던전 / 파티 [P]", button)) SetPartyPanel(true);
            if (inventory || partyPanel) return;
            if (MyParty != null)
            {
                GUI.Box(new Rect(930, 126, 325, 115), GUIContent.none, uiSkin.Window);
                GUI.Label(new Rect(944, 137, 297, 30), LanDungeons.Find(MyParty.dungeonId)?.Name ?? "던전 파티", label);
                GUI.Label(new Rect(944, 172, 297, 61), $"{MyParty.members.Length}/{MyParty.capacity}명 · 파티장 {MyParty.leader}\n" + (MyParty.inDungeon ? "던전 진행 중 · P 파티 메뉴" : "P를 눌러 파티를 확인하고 출발하세요."), small);
            }
            else if (HasApplication) GUI.Label(new Rect(930, 126, 325, 60), "참가 신청 중 · 파티장의 응답을 기다립니다.", hudSmall);
        }

        void PartyStyles()
        {
            if (partyAction != null) return;
            partyAction = new GUIStyle(button) { fontSize = 14, padding = new RectOffset(8, 8, 4, 4) };
            mapName = new GUIStyle(button) { fontSize = 15, padding = new RectOffset(5, 5, 3, 3) };
            mapTiny = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            dungeonHeading = new GUIStyle(title) { fontSize = 27 };
            campaignMap = Resources.Load<Texture2D>("Online/DungeonCampaignCasual");
        }

        void DrawPartyPanel()
        {
            PartyStyles();
            Panel(new Rect(0, 65, 1280, 655), new Color(0, .03f, .06f, .35f));
            UiWindow(new Rect(30, 79, 1220, 625));
            GUI.Label(new Rect(53, 99, 400, 38), "왕국 탈환 작전", title);
            GUI.Label(new Rect(448, 107, 480, 27), "던전을 고르고, 동료들과 함께 출발하세요", small);
            if (GUI.Button(new Rect(1147, 96, 80, 38), "닫기", button)) SetPartyPanel(false);
            if (GUI.Toggle(new Rect(53, 150, 156, 34), partyPage == 0, "던전 지도", button)) ChangePartyPage(0);
            if (GUI.Toggle(new Rect(217, 150, 156, 34), partyPage == 2, "파티 목록", button)) ChangePartyPage(2);
            if (MyParty != null && GUI.Toggle(new Rect(381, 150, 190, 34), partyPage == 1,
                "우리 파티" + (IsPartyLeader && MyParty.applications.Length > 0 ? $" · 신청 {MyParty.applications.Length}" : ""), button)) ChangePartyPage(1);
            GUI.Label(new Rect(673, 157, 195, 27), $"CH {channel} · 탈환군 모집", small);
            if (partyPage == 0) DrawDungeonMap(new Rect(53, 193, 810, 478));
            else if (partyState == null) GUI.Label(new Rect(80, 240, 730, 50), "파티 목록을 불러오는 중…", label);
            else if (partyPage == 1 && MyParty != null) DrawMyParty();
            else DrawRecruitment();
            DrawDungeonDetails(new Rect(879, 150, 347, 521));
            if (!string.IsNullOrEmpty(partyNotice)) GUI.Label(new Rect(56, 676, 1164, 25), partyNotice, small);
        }
        void ChangePartyPage(int page)
        {
            if (partyPage == page) return;
            partyPage = page; partyScroll = Vector2.zero; partyTitleFocused = false; GUI.FocusControl("");
            if (page == 1 && MyParty != null) selectedDungeon = MyParty.dungeonId;
        }

        // Hotspots are aligned to the landmarks in the selected production map.
        static readonly Vector2[] MapSites = { new Vector2(.25f,.65f), new Vector2(.49f,.40f),
            new Vector2(.82f,.27f), new Vector2(.85f,.535f), new Vector2(.70f,.74f) };
        void DrawDungeonMap(Rect area)
        {
            GUI.Box(area, GUIContent.none, uiSkin.Inset);
            Rect picture = MapPicture(area);
            if (campaignMap != null) GUI.DrawTexture(picture, campaignMap, ScaleMode.StretchToFill);
            for (int i = 0; i < LanDungeons.All.Length; i++)
            {
                var dungeon = LanDungeons.All[i]; var point = MapSites[i];
                float x = picture.x + point.x * picture.width, y = picture.y + point.y * picture.height;
                var hit = new Rect(x - 59, y - 68, 118, 86);
                if (GUI.Button(hit, GUIContent.none, GUIStyle.none)) selectedDungeon = dungeon.Id;
                bool selected = selectedDungeon == dungeon.Id;
                var old = GUI.backgroundColor;
                GUI.backgroundColor = selected ? new Color(1f, .84f, .38f) : dungeon.Available ? Color.white : new Color(.83f,.88f,.9f);
                if (GUI.Toggle(new Rect(x - 83, y + 18, 166, 34), selected, dungeon.Name, mapName)) selectedDungeon = dungeon.Id;
                GUI.backgroundColor = old;
                string status = dungeon.Available ? (selected ? "선택됨 · 1~4인" : "1~4인 던전") : dungeon.Capacity == 6 ? "4~6인 레이드 · 준비 중" : "준비 중";
                GUI.Box(new Rect(x - 77, y + 54, 154, 23), GUIContent.none, uiSkin.Slot);
                GUI.Label(new Rect(x - 77, y + 53, 154, 23), status, mapTiny);
            }
        }
        Rect MapPicture(Rect area)
        {
            float aspect = campaignMap == null ? 1.5f : (float)campaignMap.width / campaignMap.height;
            float w = Mathf.Min(area.width - 8, (area.height - 8) * aspect), h = w / aspect;
            return new Rect(area.center.x - w / 2, area.center.y - h / 2, w, h);
        }
        void DrawDungeonDetails(Rect r)
        {
            UiSlot(r);
            var dungeon = LanDungeons.Find(selectedDungeon) ?? LanDungeons.All[0];
            if (partyPage == 1 && MyParty != null) dungeon = LanDungeons.Find(MyParty.dungeonId) ?? dungeon;
            int index = Array.IndexOf(LanDungeons.All, dungeon);
            GUI.Label(new Rect(r.x + 18, r.y + 15, r.width - 36, 24), dungeon.Region, small);
            GUI.Label(new Rect(r.x + 18, r.y + 46, r.width - 36, 43), dungeon.Name, dungeonHeading);
            var preview = new Rect(r.x + 18, r.y + 96, r.width - 36, 114);
            GUI.Box(preview, GUIContent.none, uiSkin.Inset);
            if (campaignMap != null)
            {
                var p = MapSites[index];
                float cw = .31f, ch = cw / (preview.width / preview.height) * campaignMap.width / campaignMap.height;
                GUI.DrawTextureWithTexCoords(new Rect(preview.x + 3, preview.y + 3, preview.width - 6, preview.height - 6), campaignMap,
                    new Rect(Mathf.Clamp(p.x - cw / 2, 0, 1 - cw), Mathf.Clamp(1 - p.y - .04f, 0, 1 - ch), cw, ch));
            }
            GUI.Label(new Rect(r.x + 18, r.y + 224, r.width - 36, 61), dungeon.Description, label);
            GUI.Label(new Rect(r.x + 18, r.y + 291, r.width - 36, 48), $"보스  {dungeon.Boss}\n입장 인원  {dungeon.Minimum}~{dungeon.Capacity}명", small);
            if (!dungeon.Available)
            {
                GUI.Label(new Rect(r.x + 18, r.y + 358, r.width - 36, 70), "준비 중\n맵이 완성되면 출발할 수 있어요.", label);
                GUI.enabled = false; GUI.Button(new Rect(r.x + 18, r.y + 454, r.width - 36, 48), "아직 출발할 수 없어요", button); GUI.enabled = true;
                return;
            }
            GUI.Label(new Rect(r.x + 18, r.y + 344, r.width - 36, 45), "파티와 함께 맵을 둘러볼 수 있어요.\n몬스터 전투와 방 클리어는 준비 중이에요.", small);
            if (MyParty == null)
            {
                GUI.SetNextControlName("party-title");
                partyTitle = GUI.TextField(new Rect(r.x + 18, r.y + 404, r.width - 36, 38), partyTitle, 24, field);
                partyTitleFocused = GUI.GetNameOfFocusedControl() == "party-title";
                GUI.enabled = partyState != null && !HasApplication && partyTitle.Trim().Length >= 2;
                if (GUI.Button(new Rect(r.x + 18, r.y + 454, r.width - 36, 48), "이 던전으로 파티 만들기", button)) SendParty("party_create");
                GUI.enabled = true;
            }
            else
            {
                var p = MyParty;
                GUI.Label(new Rect(r.x + 18, r.y + 405, r.width - 36, 41), p.dungeonId == dungeon.Id
                    ? $"{p.members.Length}/{p.capacity}명 · " + (p.inDungeon ? "던전 진행 중" : "출발 준비 완료") : "현재 파티의 던전: " + LanDungeons.Find(p.dungeonId)?.Name, small);
                GUI.enabled = IsPartyLeader && p.dungeonId == dungeon.Id;
                if (GUI.Button(new Rect(r.x + 18, r.y + 454, r.width - 36, 48), !IsPartyLeader ? "파티장의 출발을 기다리는 중" : p.inDungeon ? "파티와 마을로 복귀" : "던전 출발", button))
                    SendParty(p.inDungeon ? "party_return" : "party_start", p.id);
                GUI.enabled = true;
            }
        }

        void DrawRecruitment()
        {
            var list = partyState.parties ?? Array.Empty<LanPartyListing>();
            GUI.Label(new Rect(69, 203, 740, 30), $"이 채널의 파티 {list.Length}개", label);
            partyScroll = GUI.BeginScrollView(new Rect(63, 242, 790, 344), partyScroll, new Rect(0, 0, 765, Mathf.Max(344, list.Length * 83)));
            if (list.Length == 0) GUI.Label(new Rect(20, 64, 710, 80), "모집 중인 파티가 없습니다.\n던전 지도에서 첫 파티를 만들어 보세요.", label);
            for (int i = 0; i < list.Length; i++)
            {
                var p = list[i]; float y = i * 83;
                UiSlot(new Rect(0, y, 765, 75));
                GUI.Label(new Rect(14, y + 8, 570, 27), p.title, label);
                GUI.Label(new Rect(14, y + 40, 570, 25), $"{LanDungeons.Find(p.dungeonId)?.Name}  ·  {p.count}/{p.capacity}명  ·  파티장 {p.leader}", small);
                bool mine = partyState.pendingPartyId == p.id;
                GUI.enabled = MyParty == null && !HasApplication && !p.inDungeon && p.count < p.capacity;
                if (GUI.Button(new Rect(611, y + 17, 140, 40), p.inDungeon ? "던전 진행 중" : mine ? "신청 대기 중" : p.count >= p.capacity ? "인원 마감" : "참가 신청", partyAction)) SendParty("party_apply", p.id);
                GUI.enabled = true;
            }
            GUI.EndScrollView();
            if (HasApplication)
            {
                GUI.Label(new Rect(72, 606, 577, 43), "신청 대기 · " + partyState.pendingPartyTitle, label);
                if (GUI.Button(new Rect(665, 607, 175, 40), "신청 취소", button)) SendParty("party_cancel", partyState.pendingPartyId);
            }
            else GUI.Label(new Rect(72, 617, 750, 30), "참가 신청을 보내면 파티장이 수락하거나 거절할 수 있어요.", small);
        }

        void DrawMyParty()
        {
            var party = MyParty;
            GUI.Label(new Rect(71, 204, 770, 30), party.title, label);
            GUI.Label(new Rect(72, 249, 344, 27), $"파티원 {party.members.Length}/{party.capacity}명", small);
            for (int i = 0; i < party.capacity; i++)
            {
                float y = 283 + i * 59;
                UiSlot(new Rect(69, y, 344, 51), i >= party.members.Length);
                if (i >= party.members.Length) { GUI.Label(new Rect(83, y + 14, 312, 26), "빈 자리 · 동료 모집 중", small); continue; }
                var member = party.members[i];
                GUI.Label(new Rect(83, y + 5, 312, 26), member.nickname, label);
                GUI.Label(new Rect(83, y + 30, 312, 20), (member.nickname == party.leader ? "파티장" : "파티원") + (member.nickname == profile.nickname ? " · 나" : ""), small);
            }
            if (IsPartyLeader)
            {
                var requests = party.applications;
                GUI.Label(new Rect(439, 249, 398, 27), $"참가 신청 {requests.Length}건", small);
                partyScroll = GUI.BeginScrollView(new Rect(433, 283, 418, 236), partyScroll, new Rect(0, 0, 393, Mathf.Max(236, requests.Length * 92)));
                if (requests.Length == 0) GUI.Label(new Rect(9, 20, 366, 90), party.inDungeon ? "던전 진행 중에는\n새 파티원을 받을 수 없어요." : "새로운 참가 신청을\n기다리고 있어요.", label);
                for (int i = 0; i < requests.Length; i++)
                {
                    var request = requests[i]; float y = i * 92;
                    UiSlot(new Rect(0, y, 393, 84));
                    GUI.Label(new Rect(12, y + 8, 369, 27), request.nickname, label);
                    if (GUI.Button(new Rect(12, y + 43, 175, 32), "수락", partyAction)) SendParty("party_accept", party.id, request.id);
                    if (GUI.Button(new Rect(203, y + 43, 178, 32), "거절", partyAction)) SendParty("party_reject", party.id, request.id);
                }
                GUI.EndScrollView();
            }
            else GUI.Label(new Rect(443, 300, 378, 90), "파티에 참가했습니다.\n파티장이 던전 출발을 누르면 함께 입장해요.", label);
            GUI.Label(new Rect(72, 544, 750, 48), party.inDungeon ? "파티장이 마을로 복귀하면 함께 돌아갑니다.\n혼자 나가려면 파티를 탈퇴하세요." : $"현재 인원 출발 시 공격력 +{Mathf.RoundToInt((LanRules.DungeonPower(party.members.Length)-1)*100)}%\n인원이 부족해도 출발할 수 있어요.", small);
            if (GUI.Button(new Rect(663, 610, 178, 40), party.members.Length == 1 ? "파티 해산" : "파티 탈퇴", button)) SendParty("party_leave", party.id);
        }
    }
}
