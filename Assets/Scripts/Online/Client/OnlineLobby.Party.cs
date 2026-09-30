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
        GUIStyle partyAction;
        LanParty MyParty => partyState == null ? null : partyState.party;
        bool IsPartyLeader => MyParty != null && MyParty.leader == profile.nickname;
        bool HasApplication => partyState != null && !string.IsNullOrEmpty(partyState.pendingPartyId);

        void SetPartyPanel(bool open)
        {
            partyPanel = open;
            if (open) inventory = false;
            chatFocused = false; partyTitleFocused = false; partyFocusReset = true;
        }

        // All buttons and the opt-in build smoke test use this same command path.
        void SendParty(string op, string partyId = null, string applicationId = null)
        {
            if (client == null || Time.unscaledTime < partySendAt) return;
            partySendAt = Time.unscaledTime + .25f;
            client.Send(new LanCommand { op = op, partyId = partyId, applicationId = applicationId, text = partyTitle });
        }

        void DrawPartySummary()
        {
            if (partyFocusReset) { GUI.FocusControl(""); partyFocusReset = false; }
            int waiting = IsPartyLeader ? MyParty.applications.Length : 0;
            string text = waiting > 0 ? $"파티 [P] · 신청 {waiting}" : "파티 [P]";
            if (GUI.Button(new Rect(1035, 76, 220, 40), text, button)) SetPartyPanel(true);
            if (inventory || partyPanel) return;
            if (MyParty != null)
            {
                GUI.Box(new Rect(930, 126, 325, 107), GUIContent.none, uiSkin.Window);
                GUI.Label(new Rect(944, 137, 297, 39), MyParty.title, label);
                GUI.Label(new Rect(944, 179, 297, 43), $"{MyParty.members.Length}/{MyParty.capacity}명 · 파티장 {MyParty.leader}\nP를 눌러 파티원과 신청을 확인하세요.", small);
            }
            else if (HasApplication)
                GUI.Label(new Rect(930, 126, 325, 60), "참가 신청 중 · 파티장의 응답을 기다립니다.", hudSmall);
        }

        void DrawPartyPanel()
        {
            if (partyAction == null) partyAction = new GUIStyle(button) { fontSize = 15, padding = new RectOffset(8, 8, 4, 4) };
            Panel(new Rect(0, 65, 1280, 655), new Color(0, 0, 0, .16f));
            UiWindow(new Rect(210, 110, 860, 570));
            GUI.Label(new Rect(240, 135, 620, 52), MyParty == null ? "탈환군 파티 모집" : "우리 파티", title);
            if (GUI.Button(new Rect(967, 132, 73, 39), "닫기", button)) SetPartyPanel(false);
            GUI.Label(new Rect(240, 190, 790, 26), $"CH {channel} · 최대 {LanRules.PartyCapacity}명 · 참가 신청 후 파티장이 수락하면 가입됩니다.", small);
            if (partyState == null)
                GUI.Label(new Rect(240, 245, 750, 40), "파티 목록을 불러오는 중…", label);
            else if (MyParty == null) DrawRecruitment();
            else DrawMyParty();
            GUI.Label(new Rect(240, 628, 800, 42), partyNotice, small);
        }

        void DrawRecruitment()
        {
            GUI.SetNextControlName("party-title");
            partyTitle = GUI.TextField(new Rect(240, 227, 590, 40), partyTitle, 24, field);
            partyTitleFocused = GUI.GetNameOfFocusedControl() == "party-title";
            GUI.enabled = !HasApplication && partyTitle.Trim().Length >= 2;
            if (GUI.Button(new Rect(844, 227, 196, 40), "새 파티 만들기", button)) SendParty("party_create");
            GUI.enabled = true;
            var list = partyState.parties ?? Array.Empty<LanPartyListing>();
            GUI.Label(new Rect(242, 282, 760, 28), $"이 채널의 파티 {list.Length}개", small);
            partyScroll = GUI.BeginScrollView(new Rect(240, 315, 800, 242), partyScroll, new Rect(0, 0, 775, Mathf.Max(242, list.Length * 72)));
            if (list.Length == 0) GUI.Label(new Rect(15, 45, 735, 60), "아직 모집 중인 파티가 없습니다.\n첫 번째 파티를 만들어 보세요.", label);
            for (int i = 0; i < list.Length; i++)
            {
                var p = list[i]; float y = i * 72;
                UiSlot(new Rect(0, y, 775, 65));
                GUI.Label(new Rect(14, y + 8, 530, 27), p.title, label);
                GUI.Label(new Rect(14, y + 36, 530, 24), $"파티장 {p.leader}  ·  {p.count}/{p.capacity}명", small);
                bool mine = partyState.pendingPartyId == p.id;
                GUI.enabled = !HasApplication && p.count < p.capacity;
                if (GUI.Button(new Rect(615, y + 12, 145, 41), mine ? "신청 대기 중" : p.count >= p.capacity ? "인원 마감" : "참가 신청", button)) SendParty("party_apply", p.id);
                GUI.enabled = true;
            }
            GUI.EndScrollView();
            if (HasApplication)
            {
                GUI.Label(new Rect(240, 574, 585, 43), "신청 대기 · " + partyState.pendingPartyTitle, label);
                if (GUI.Button(new Rect(864, 574, 176, 40), "신청 취소", button)) SendParty("party_cancel", partyState.pendingPartyId);
            }
            else GUI.Label(new Rect(240, 580, 780, 32), "참가 신청은 한 번에 한 파티에 보낼 수 있습니다.", small);
        }

        void DrawMyParty()
        {
            var party = MyParty;
            GUI.Label(new Rect(240, 225, 800, 29), party.title, label);
            GUI.Label(new Rect(240, 267, 345, 25), $"파티원 {party.members.Length}/{party.capacity}명", small);
            for (int i = 0; i < party.capacity; i++)
            {
                float y = 303 + i * 60;
                UiSlot(new Rect(240, y, 338, 52), i >= party.members.Length);
                if (i >= party.members.Length)
                { GUI.Label(new Rect(254, y + 14, 310, 28), "빈 자리 · 동료 모집 중", small); continue; }
                var member = party.members[i];
                string role = member.nickname == party.leader ? "파티장" : "파티원";
                if (member.nickname == profile.nickname) role += " · 나";
                GUI.Label(new Rect(254, y + 4, 310, 27), member.nickname, label);
                GUI.Label(new Rect(254, y + 29, 310, 21), role + (member.body == 0 ? " · 남성" : " · 여성"), small);
            }
            if (IsPartyLeader)
            {
                var requests = party.applications;
                GUI.Label(new Rect(606, 267, 420, 25), $"참가 신청 {requests.Length}건", small);
                partyScroll = GUI.BeginScrollView(new Rect(600, 303, 440, 241), partyScroll, new Rect(0, 0, 415, Mathf.Max(241, requests.Length * 92)));
                if (requests.Length == 0) GUI.Label(new Rect(8, 22, 398, 65), "새로운 참가 신청을 기다리고 있습니다.", label);
                for (int i = 0; i < requests.Length; i++)
                {
                    var request = requests[i]; float y = i * 92;
                    UiSlot(new Rect(0, y, 415, 84));
                    GUI.Label(new Rect(12, y + 8, 385, 28), request.nickname, label);
                    if (GUI.Button(new Rect(12, y + 42, 187, 33), "수락", partyAction)) SendParty("party_accept", party.id, request.id);
                    if (GUI.Button(new Rect(211, y + 42, 190, 33), "거절", partyAction)) SendParty("party_reject", party.id, request.id);
                }
                GUI.EndScrollView();
            }
            else GUI.Label(new Rect(608, 307, 410, 100), "파티에 참가했습니다.\n새 참가 신청은 파티장이 확인합니다.", label);
            GUI.Label(new Rect(240, 567, 570, 49), IsPartyLeader
                ? "파티장이 나가면 먼저 참가한 동료에게 파티장이 넘어갑니다. 마지막 인원이 나가면 해산됩니다."
                : "파티를 탈퇴하면 다른 파티에 신청할 수 있습니다.", small);
            if (GUI.Button(new Rect(864, 574, 176, 40), party.members.Length == 1 ? "파티 해산" : "파티 탈퇴", button)) SendParty("party_leave", party.id);
        }
    }
}
