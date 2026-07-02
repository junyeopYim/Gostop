using Hwatu.View.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hwatu.View.Editor
{
    /// <summary>
    /// 빈 GameObject "AppRoot" + GameFlowController 하나만 담긴 Main 씬을 생성한다.
    /// 화면 UI는 전부 실행 시 코드로 만든다. 기존 HwatuPrototype 씬은 건드리지 않는다.
    /// </summary>
    public static class CreateMainSceneMenu
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Tools/Hwatu/Create Main Scene")]
        public static void CreateMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var appRoot = new GameObject("AppRoot");
            appRoot.AddComponent<GameFlowController>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Main 씬 생성 완료: {ScenePath}");
        }
    }
}
