using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MiniWar.Online
{
    public sealed partial class OnlineLobby
    {
        readonly CancellationTokenSource discoveryStop = new CancellationTokenSource();
        Task<List<LanServerInfo>> discoveryTask;
        List<LanServerInfo> discovered = new List<LanServerInfo>();
        LanServerInfo selectedServer;
        float nextDiscovery;
        bool rememberLogin = true;

        void SearchServers()
        {
            if (discoveryTask != null || connecting) return;
            discoveryTask = Task.Run(() => LanDiscovery.Search(discoveryStop.Token));
        }
        void UpdateDiscovery()
        {
            if (localPreview || profile != null) return;
            if (discoveryTask != null && discoveryTask.IsCompleted)
            {
                if (discoveryTask.Status == TaskStatus.RanToCompletion)
                {
                    discovered = discoveryTask.Result;
                    var previous = selectedServer;
                    selectedServer = previous == null ? null : discovered.Find(s => s.Key == previous.Key && s.Fingerprint == previous.Fingerprint);
                    if (selectedServer == null && discovered.Count == 1) selectedServer = discovered[0];
                }
                else { var ignored = discoveryTask.Exception; discovered.Clear(); selectedServer = null; }
                discoveryTask = null; nextDiscovery = Time.unscaledTime + 5;
            }
            if (!connecting && discoveryTask == null && Time.unscaledTime >= nextDiscovery) SearchServers();
        }
        void DrawServerSelection()
        {
            GUI.Label(new Rect(760, 142, 300, 42), selectedServer != null ? "접속가능" : "접속불가", label);
            GUI.enabled = !connecting && discoveryTask == null;
            if (GUI.Button(new Rect(1070, 136, 115, 42), "새로고침", button)) SearchServers();
            GUI.enabled = true;
        }
    }
}
