using System;
using System.IO;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace MiniWar.EditorTools
{
    /// <summary>Project-specific connection to the loopback MCP service launched by Tools/Start-UnityMcp.ps1.</summary>
    [InitializeOnLoad]
    public static class UnityMcpConnection
    {
        static string LocalFile(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library", name));

        static UnityMcpConnection()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            if (File.Exists(LocalFile("MiniWarMcpAutoConnect"))) EditorApplication.delayCall += Connect;
        }

        [MenuItem("MiniWar/Unity MCP/Connect local server")]
        public static async void Connect()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Connect;
                return;
            }
            try
            {
                EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
                EditorPrefs.SetString("MCPForUnity.HttpTransportScope", "local");
                EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://127.0.0.1:8765");
                EditorPrefs.SetBool("MCPForUnity.TelemetryDisabled", true);
                EditorConfigurationCache.Instance.Refresh();
                bool connected = await MCPServiceLocator.Bridge.StartAsync();
                File.WriteAllText(LocalFile("MiniWarMcpStatus.txt"), DateTime.UtcNow.ToString("O") + " connected=" + connected);
                if (connected) Debug.Log("MiniWar Unity MCP connected: http://127.0.0.1:8765/mcp");
                else Debug.LogWarning("Start Tools/Start-UnityMcp.ps1, then use MiniWar > Unity MCP > Connect local server.");
            }
            catch (Exception ex)
            {
                File.WriteAllText(LocalFile("MiniWarMcpStatus.txt"), ex.ToString());
                Debug.LogException(ex);
            }
        }
    }
}
