using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>프로젝트 초기 설정. 메뉴 Abyss/Setup Project, 또는 배치: -executeMethod Abyss.EditorTools.ProjectSetup.Run</summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Abyss/Setup Project")]
        public static void Run()
        {
            PlayerSettings.productName = "Abyss Keeper";
            // 가로 화면 전용 (좌/우 가로만 허용)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.useAnimatedAutorotation = false;

            // 아트 다시 임포트 (임포터 규칙 적용)
            foreach (string dir in new[] { "Assets/Resources/Art/Icons", "Assets/Resources/Art/Frames" })
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

            // 씬: 빈 씬 + Bootstrap 하나
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Bootstrap").AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Abyss project setup done: " + ScenePath);
        }
    }
}
