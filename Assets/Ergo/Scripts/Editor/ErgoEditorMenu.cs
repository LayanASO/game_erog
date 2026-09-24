using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Ergo.EditorTools
{
    /// <summary>
    /// Editor conveniences: an "Ergo" menu, and opening the game scene when the project is first opened (Unity
    /// otherwise starts with an empty untitled scene).
    /// </summary>
    [InitializeOnLoad]
    internal static class ErgoEditorMenu
    {
        private const string ScenePath = "Assets/Ergo/Scenes/Ergo.unity";
        private const string CheckedThisSessionKey = "Ergo.GameSceneCheck";

        static ErgoEditorMenu()
        {
            EditorApplication.delayCall += OpenGameSceneIfEditorIsEmpty;
        }

        [MenuItem("Ergo/Open Game Scene", false, 0)]
        private static void OpenGameScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        [MenuItem("Ergo/Play", false, 1)]
        private static void Play()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                EditorSceneManager.OpenScene(ScenePath);
            }

            EditorApplication.isPlaying = true;
        }

        private static void OpenGameSceneIfEditorIsEmpty()
        {
            if (SessionState.GetBool(CheckedThisSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(CheckedThisSessionKey, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1)
            {
                return;
            }

            Scene active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }
    }
}
