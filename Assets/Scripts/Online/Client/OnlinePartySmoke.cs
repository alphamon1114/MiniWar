#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MiniWar.Online
{
    /// <summary>Explicit two-player recruitment check in real Windows builds, using the UI command path.</summary>
    public sealed partial class OnlinePartySmoke : MonoBehaviour
    {
        [Serializable] sealed class Config
        {
            public string pin, nickname, password, result, screenshots;
            public int port;
            public bool leader;
            public bool automaticDiscovery;
            public bool dungeon;
        }
        Config config;
        OnlineLobby lobby;
        bool ready;
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        T Get<T>(string name) => (T)typeof(OnlineLobby).GetField(name, Flags).GetValue(lobby);
        void Set(string name, object value) => typeof(OnlineLobby).GetField(name, Flags).SetValue(lobby, value);
        LanEvent State => Get<LanEvent>("partyState");
        void Send(string op, string id = null, string ticket = null)
            => typeof(OnlineLobby).GetMethod("SendParty", Flags).Invoke(lobby, new object[] { op, id, ticket });

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--miniwar-party-smoke-config");
            if (index < 0 || index + 1 >= args.Length) return;
            var smoke = new GameObject("Explicit party build smoke test").AddComponent<OnlinePartySmoke>();
            smoke.config = JsonUtility.FromJson<Config>(File.ReadAllText(args[index + 1]));
        }

        IEnumerator Start()
        {
            var run = Run();
            while (true)
            {
                object current;
                try { if (!run.MoveNext()) yield break; current = run.Current; }
                catch (Exception ex) { Finish("FAIL " + ex); yield break; }
                yield return current;
            }
        }

        IEnumerator Until(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            ready = condition();
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame();
            var picture = ScreenCapture.CaptureScreenshotAsTexture();
            if (picture == null) { Debug.LogWarning("Party QA capture unavailable: " + name); yield break; }
            File.WriteAllBytes(Path.Combine(config.screenshots, name + ".png"), picture.EncodeToPNG());
            Destroy(picture);
        }

        IEnumerator Run()
        {
            yield return null;
            lobby = FindFirstObjectByType<OnlineLobby>();
            Set("localPreview", !config.automaticDiscovery);
            Set("rememberLogin", false); // Keep real player's saved login settings untouched.
            Set("host", "127.0.0.1"); Set("portText", config.port.ToString()); Set("fingerprint", config.pin);
            Set("nickname", config.nickname); Set("password", config.password); Set("selectedBody", config.leader ? 0 : 1);
            if (config.automaticDiscovery)
            {
                yield return Until(() => Get<LanServerInfo>("selectedServer") != null && Get<LanServerInfo>("selectedServer").Port == config.port);
                if (!ready) { Finish("FAIL automatic server discovery"); yield break; }
            }
            typeof(OnlineLobby).GetMethod("BeginLogin", Flags).Invoke(lobby, new object[] { true });
            config.password = null;
            yield return Until(() => Get<LanProfile>("profile") != null && State != null);
            if (!ready) { Finish("FAIL login: " + Get<string>("notice")); yield break; }
            if (State.party != null) { Finish("FAIL new player has phantom party"); yield break; }
            typeof(OnlineLobby).GetMethod("SetPartyPanel", Flags).Invoke(lobby, new object[] { true });
            if (config.dungeon) { yield return RunDungeon(); yield break; }
            if (config.leader)
            {
                Set("partyTitle", "성문 외곽 · 함께 탈환해요"); Send("party_create");
                yield return Until(() => State.party != null && State.party.applications.Length == 1);
                if (!ready) { Finish("FAIL initial application missing"); yield break; }
                yield return Capture("leader-request");
                string id = State.party.id, oldTicket = State.party.applications[0].id;
                Send("party_reject", id, oldTicket);
                yield return Until(() => State.party != null && State.party.applications.Length == 1 && State.party.applications[0].id != oldTicket);
                if (!ready) { Finish("FAIL reapplication missing"); yield break; }
                Send("party_accept", id, State.party.applications[0].id);
                yield return Until(() => State.party != null && State.party.members.Length == 2);
                if (!ready) { Finish("FAIL member not accepted"); yield break; }
                yield return Capture("leader-members");
                yield return new WaitForSecondsRealtime(3);
                Send("party_leave", id);
                yield return Until(() => State.party == null);
                if (!ready) { Finish("FAIL leader leave"); yield break; }
                yield return new WaitForSecondsRealtime(1);
                Finish("PASS Windows leader: create, receive application, reject, accept reapplication, two members, leave");
            }
            else
            {
                yield return Until(() => State.parties.Length == 1);
                if (!ready) { Finish("FAIL party list"); yield break; }
                yield return Capture("applicant-list");
                string id = State.parties[0].id;
                Send("party_apply", id);
                yield return Until(() => Get<string>("partyNotice").Contains("거절") && string.IsNullOrEmpty(State.pendingPartyId));
                if (!ready) { Finish("FAIL rejection not received"); yield break; }
                yield return Capture("applicant-rejected");
                yield return new WaitForSecondsRealtime(.6f);
                Send("party_apply", id);
                yield return Until(() => State.party != null && State.party.members.Length == 2);
                if (!ready) { Finish("FAIL approval not received"); yield break; }
                if (State.party.applications.Length != 0) { Finish("FAIL private application inbox leak"); yield break; }
                yield return Capture("applicant-members");
                yield return Until(() => State.party != null && State.party.leader == config.nickname);
                if (!ready) { Finish("FAIL leadership transfer"); yield break; }
                yield return Capture("new-leader");
                Send("party_leave", id);
                yield return Until(() => State.party == null && State.parties.Length == 0);
                if (!ready) { Finish("FAIL disband listing"); yield break; }
                Finish("PASS Windows applicant: browse, apply, rejection, reapply, join, private inbox, become leader, disband");
            }
        }

        void Finish(string result)
        {
            File.WriteAllText(config.result, result); Debug.Log(result);
            Application.Quit(result.StartsWith("PASS") ? 0 : 1);
        }
    }
}
#endif
