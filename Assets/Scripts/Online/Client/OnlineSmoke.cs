#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MiniWar.Online
{
    /// <summary>Opt-in Windows build smoke test. Never runs during ordinary gameplay.</summary>
    public sealed class OnlineSmoke : MonoBehaviour
    {
        [Serializable] sealed class Config
        {
            public string host, pin, nickname, password, result, screenshot;
            public int port, body;
        }
        Config config;
        OnlineLobby lobby;
        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        T Get<T>(string name) => (T)typeof(OnlineLobby).GetField(name, Flags).GetValue(lobby);
        void Set(string name, object value) => typeof(OnlineLobby).GetField(name, Flags).SetValue(lobby, value);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--miniwar-smoke-config");
            if (index < 0 || index + 1 >= args.Length) return;
            var go = new GameObject("Explicit build smoke test");
            var smoke = go.AddComponent<OnlineSmoke>();
            smoke.config = JsonUtility.FromJson<Config>(File.ReadAllText(args[index + 1]));
        }

        IEnumerator Start()
        {
            yield return null;
            lobby = FindFirstObjectByType<OnlineLobby>();
            if (lobby == null || config == null) { Finish("FAIL no lobby/config"); yield break; }
            Set("localPreview", true);
            Set("host", config.host); Set("portText", config.port.ToString()); Set("fingerprint", config.pin);
            Set("nickname", config.nickname); Set("password", config.password); Set("selectedBody", config.body);
            typeof(OnlineLobby).GetMethod("BeginLogin", Flags).Invoke(lobby, new object[] { true });
            config.password = null;
            float until = Time.realtimeSinceStartup + 20;
            while (Get<LanProfile>("profile") == null && Time.realtimeSinceStartup < until) yield return null;
            var profile = Get<LanProfile>("profile");
            if (profile == null || profile.body != config.body) { Finish("FAIL login/character " + Get<string>("notice")); yield break; }
            until = Time.realtimeSinceStartup + 25;
            while ((Get<LanEvent>("snapshot") == null || Get<LanEvent>("snapshot").actors.Length < 2) && Time.realtimeSinceStartup < until) yield return null;
            var snapshot = Get<LanEvent>("snapshot");
            if (snapshot == null || snapshot.actors.Length < 2) { Finish("FAIL second client missing"); yield break; }
            var client = Get<LanClient>("client");
            string melee = profile.items.Single(x => x.family == 5).id;
            client.Send(new LanCommand { op = "equip", itemId = melee });
            until = Time.realtimeSinceStartup + 5;
            while (Get<LanProfile>("profile").activeItemId != melee && Time.realtimeSinceStartup < until) yield return null;
            if (Get<LanProfile>("profile").activeItemId != melee) { Finish("FAIL equip"); yield break; }
            yield return new WaitForSecondsRealtime(1);
            client.Send(new LanCommand { op = "equip", itemId = profile.items[0].id });
            yield return new WaitForSecondsRealtime(1);
            client.Send(new LanCommand { op = "chat", text = "동료 접속 확인 · " + config.nickname });
            yield return new WaitForSecondsRealtime(3);
            var messages = Get<System.Collections.Generic.List<string>>("chat");
            if (!messages.Any(x => x.Contains("동료 접속 확인") && !x.Contains(config.nickname))) { Finish("FAIL peer chat"); yield break; }
            var views = FindObjectsByType<OnlineActorView>(FindObjectsSortMode.None);
            bool sprites = views.Length >= 2 && views.All(x => x.GetComponentsInChildren<SpriteRenderer>().All(s => s.sprite != null));
            if (!sprites) { Finish("FAIL missing character/weapon sprite"); yield break; }
            bool rigged = views.All(x => x.Rig != null && x.Rig.IsTwoPiece
                && x.Rig.weaponGrip.parent == x.Rig.connectedArms.transform);
            rigged &= views.All(x => x.Rig.bodyFrames.frames.Length == 16 && x.Rig.armFrames.frames.Length == 12
                && x.Rig.parts.All(p => !p.enabled) && x.Rig.connectedBody.sprite != null && x.Rig.connectedArms.sprite != null);
            if (!rigged) { Finish("FAIL connected body or procedural arm attachment missing"); yield break; }
            // Suppress ordinary keyboard input; use the same authenticated input protocol as the UI.
            Set("sendAt", Time.unscaledTime + 10);
            long shotSequence = Get<long>("sequence");
            float dashStartX = Get<LanEvent>("snapshot").actors.Single(x => x.nickname == profile.nickname).x;
            int inputIndex = 0;
            bool sawDashPose = false;
            until = Time.realtimeSinceStartup + 4;
            while (Time.realtimeSinceStartup < until)
            {
                client.Send(new LanCommand { op = "input", sequence = ++shotSequence,
                    fire = true, aiming = true, aimAngle = config.body == 0 ? 32 : 148,
                    dash = inputIndex == 0 || inputIndex == 5 || inputIndex == 9 });
                inputIndex++;
                yield return new WaitForSecondsRealtime(.1f);
                sawDashPose |= views.Any(x => x.IsDashing && x.Rig.CurrentBodyFrame == 12);
            }
            Set("sequence", shotSequence);
            if (Get<int>("shotsReceived") < 2 || Get<int>("peerShotsReceived") < 1)
            { Finish("FAIL own/peer practice shots"); yield break; }
            var dashed = Get<LanEvent>("snapshot").actors.Single(x => x.nickname == profile.nickname);
            float expected = 2 * LanRules.DashSpeed * LanRules.DashDuration;
            if (views.Any(x => x.DashSequence != 2) || Mathf.Abs(Mathf.Abs(dashed.x - dashStartX) - expected) > .03f || !sawDashPose)
            { Finish("FAIL two-dash distance, peer sync, pose or third-tap rejection"); yield break; }
            ScreenCapture.CaptureScreenshot(config.screenshot);
            yield return new WaitForSecondsRealtime(1);
            Finish("PASS login, 2 real Windows clients, equipment swap, Korean peer chat, two-piece character frames, own and peer practice shooting, two-dash distance/pose/peer sync, third-tap rejection");
        }

        void Finish(string result)
        {
            if (config != null && !string.IsNullOrEmpty(config.result)) File.WriteAllText(config.result, result);
            Debug.Log(result);
            Application.Quit(result.StartsWith("PASS") ? 0 : 1);
        }
    }
}
#endif
