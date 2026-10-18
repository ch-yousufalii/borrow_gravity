using UnityEditor;
using UnityEditor.SceneManagement;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Pressing Play always starts the game from the main menu, whichever scene is open for editing
    /// (the editor goes back to that scene when play mode ends).
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromMenu
    {
        static PlayFromMenu() => EditorApplication.delayCall += Apply;

        static void Apply() => EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameBuilder.MenuPath);
    }
}
