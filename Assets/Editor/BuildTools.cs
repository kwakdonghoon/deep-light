using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>
    /// 공유용 빌드. 메뉴 Abyss/Build 에서 고르거나, 배치로 실행한다:
    ///   Unity.exe -batchmode -nographics -projectPath &lt;경로&gt; -quit -executeMethod Abyss.EditorTools.BuildTools.BuildWindows
    ///   Unity.exe -batchmode -nographics -projectPath &lt;경로&gt; -quit -executeMethod Abyss.EditorTools.BuildTools.BuildAndroid
    /// 결과는 프로젝트 폴더의 Builds/ 아래에 만들어진다 (git 에는 올라가지 않는다).
    /// </summary>
    public static class BuildTools
    {
        const string Scene = "Assets/Scenes/Main.unity";
        const string Product = "Deep Light";
        const string Version = "0.1.0";

        static string OutRoot { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds")); } }

        /// <summary>회사 공유용 공통 설정: 이름, 버전, 가로 전용, 창 모드.</summary>
        const string Company = "EKGAMES";
        const string IconPath = "Assets/Icons/AppIcon.png";

        /// <summary>에디터가 열릴 때마다 회사명·제품명·앱 아이콘이 맞는지 확인해 프로젝트 설정에 남겨 둔다.</summary>
        [InitializeOnLoadMethod]
        internal static void EnsureBranding()
        {
            bool dirty = false;
            if (PlayerSettings.companyName != Company) { PlayerSettings.companyName = Company; dirty = true; }
            if (PlayerSettings.productName != Product) { PlayerSettings.productName = Product; dirty = true; }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                var cur = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Application);
                if (cur == null || cur.Length == 0 || cur[0] != icon) { PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application); dirty = true; }
            }
            if (dirty) AssetDatabase.SaveAssets();
        }

        static void ApplyCommonSettings()
        {
            EnsureBranding();
            PlayerSettings.productName = Product;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            // PC: 1280x720 창으로 시작, 크기 조절 가능 (HUD 는 해상도에 맞춰 다시 배치된다)
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
        }

        [MenuItem("Abyss/Build/Windows (폴더)")]
        public static void BuildWindows()
        {
            ApplyCommonSettings();
            string dir = Path.Combine(OutRoot, "Windows", "DeepLight");
            Directory.CreateDirectory(dir);
            Run(new BuildPlayerOptions
            {
                scenes = new[] { Scene }, target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone,
                locationPathName = Path.Combine(dir, "DeepLight.exe"), options = BuildOptions.None
            }, dir);
        }

        [MenuItem("Abyss/Build/Android (APK)")]
        public static void BuildAndroid()
        {
            ApplyCommonSettings();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.deeplight.game");
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);   // ARM64 는 IL2CPP 가 필요하다
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;                                                   // 공유는 APK 로 (스토어 제출 때만 AAB)
            string dir = Path.Combine(OutRoot, "Android");
            Directory.CreateDirectory(dir);
            Run(new BuildPlayerOptions
            {
                scenes = new[] { Scene }, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
                locationPathName = Path.Combine(dir, "DeepLight-" + Version + ".apk"), options = BuildOptions.None
            }, dir);
        }

        [MenuItem("Abyss/Build/WebGL (브라우저)")]
        public static void BuildWebGL()
        {
            ApplyCommonSettings();
            // 사내 웹서버가 압축 헤더(Content-Encoding)를 못 맞춰 줘도 열리도록 압축은 끈다. 용량이 필요하면 Gzip + 서버 설정으로 바꾼다.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            string dir = Path.Combine(OutRoot, "WebGL", "DeepLight");
            Directory.CreateDirectory(dir);
            bool ok = Run(new BuildPlayerOptions
            {
                scenes = new[] { Scene }, target = BuildTarget.WebGL, targetGroup = BuildTargetGroup.WebGL,
                locationPathName = dir, options = BuildOptions.None
            }, dir);
            // 브라우저는 file:// 로 열면 WebGL 이 동작하지 않으므로, 폴더 안에서 더블클릭하면 로컬 서버를 띄우는 배치 파일을 같이 둔다 (Python 필요). 빌드가 성공했을 때만 만든다.
            if (ok)
                File.WriteAllText(Path.Combine(dir, "run_local.bat"),
                    "@echo off\r\ncd /d \"%~dp0\"\r\necho Open http://localhost:8080 in your browser (Ctrl+C to stop)\r\nstart \"\" http://localhost:8080\r\npython -m http.server 8080 --bind 127.0.0.1\r\n");
        }

        static bool Run(BuildPlayerOptions o, string outDir)
        {
            AssetDatabase.SaveAssets();
            BuildReport r = BuildPipeline.BuildPlayer(o);
            var s = r.summary;
            Debug.Log("Build " + s.result + ": " + o.locationPathName + "  (" + (s.totalSize / (1024 * 1024)) + " MB, " + s.totalTime.TotalSeconds.ToString("0") + "s, errors " + s.totalErrors + ")");
            if (s.result == BuildResult.Succeeded) { EditorUtility.RevealInFinder(o.locationPathName); return true; }
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return false;
        }
    }
}
