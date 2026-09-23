using System;
using System.IO;
using MiniWar.Online;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MiniWar.EditorTools
{
    public static class OnlineBuild
    {
        public const string ScenePath = "Assets/Scenes/OnlineLobby.unity";

        [MenuItem("MiniWar/Online/Create or refresh lobby scene")]
        public static void CreateScene()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty) throw new InvalidOperationException("Save the current scene before generating the online lobby.");
            AssetDatabase.Refresh();
            PrepareTextures();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 6;
            camera.transform.position = new Vector3(10, 0, -10);
            camera.gameObject.AddComponent<AudioListener>();
            var lobby = new GameObject("Online Lobby").AddComponent<OnlineLobby>();
            lobby.backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Online/SurvivorsRefuge.png");
            lobby.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/DNFBitBitv2.ttf");
            lobby.npcPortraits = new[] {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Portraits/PORTRAIT_Armory.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Portraits/PORTRAIT_Market.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Portraits/PORTRAIT_Gate.png")
            };
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/Town.unity", false), new EditorBuildSettingsScene("Assets/Scenes/Dungeon.unity", false) };
            AssetDatabase.SaveAssets();
            Debug.Log("Online lobby ready: " + ScenePath);
        }

        static void SetTexture(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = true;
            importer.maxTextureSize = 4096; importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static void PrepareTextures()
        {
            foreach (var name in new[] { "SurvivorsRefuge", "GunnerMale", "GunnerFemale", "Weapons", "Handguns" })
                SetTexture("Assets/Resources/Online/" + name + ".png");
        }

        [MenuItem("MiniWar/Online/Build Windows client")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) CreateScene();
            PrepareTextures();
            Directory.CreateDirectory("Builds/OnlineClient");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/OnlineClient/MiniWar.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            File.WriteAllText("Logs/OnlineBuildResult.txt", report.summary.result + " errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + report.summary.result);
        }
    }
}
