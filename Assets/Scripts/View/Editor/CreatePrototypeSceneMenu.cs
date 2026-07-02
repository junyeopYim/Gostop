using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hwatu.View.Editor
{
    /// <summary>
    /// 빈 GameObject "Bootstrap" + GameController 하나만 담긴 부트스트랩 씬을 생성한다.
    /// UI는 전부 GameController가 실행 시 코드로 만든다.
    /// </summary>
    public static class CreatePrototypeSceneMenu
    {
        private const string ScenePath = "Assets/Scenes/HwatuPrototype.unity";

        [MenuItem("Tools/Hwatu/Create Prototype Scene")]
        public static void CreatePrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("Bootstrap");
            bootstrap.AddComponent<GameController>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Hwatu 프로토타입 씬 생성 완료: {ScenePath}");
        }
    }
}
