using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Merges the level scenes (intro, stages 1-3, ending) into one Game scene so levels switch in place without a
    /// scene load. Each level's objects go under a <see cref="StageRoot"/> (switched off until shown) that remembers
    /// the level's sky, fog and ambient light. The separate level scenes stay as the editable sources.
    /// </summary>
    public static class GameSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";

        static readonly (int index, string name, string path)[] Levels =
        {
            (GameStages.Intro, "Level_Intro", IntroBuilder.ScenePath),
            (1, "Level_Stage1", Stage1Builder.ScenePath),
            (2, "Level_Stage2", Stage2Builder.ScenePath),
            (3, "Level_Stage3", Stage3Builder.ScenePath),
            (GameStages.Outro, "Level_Outro", OutroBuilder.ScenePath),
        };

        [MenuItem("BorrowGravity/Combine Levels Into Game Scene", priority = 40)]
        public static void Build()
        {
            var game = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var manager = new GameObject("GameStages").AddComponent<GameStages>();
            var roots = new StageRoot[GameStages.Outro + 1];

            foreach (var (index, name, path) in Levels)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogWarning($"[BorrowGravity] {path} is missing — build it first. Skipped.");
                    continue;
                }
                var src = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(src); // RenderSettings belong to the active scene

                var rootGo = new GameObject(name);
                SceneManager.MoveGameObjectToScene(rootGo, src);
                var root = rootGo.AddComponent<StageRoot>();
                root.index = index;
                root.skybox = RenderSettings.skybox;
                root.ambientMode = RenderSettings.ambientMode;
                root.ambientIntensity = RenderSettings.ambientIntensity;
                root.ambientLight = RenderSettings.ambientLight;
                root.fog = RenderSettings.fog;
                root.fogMode = RenderSettings.fogMode;
                root.fogDensity = RenderSettings.fogDensity;
                root.fogColor = RenderSettings.fogColor;
                root.sun = RenderSettings.sun;

                foreach (var go in src.GetRootGameObjects().Where(g => g != rootGo))
                    go.transform.SetParent(rootGo.transform, true);
                SceneManager.MoveGameObjectToScene(rootGo, game);
                if (root.sun == null) root.sun = rootGo.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.type == LightType.Directional);
                rootGo.SetActive(false);
                roots[index] = root;
                EditorSceneManager.CloseScene(src, true);
            }

            SceneManager.SetActiveScene(game);
            manager.roots = roots;
            // the editor view of the Game scene uses Stage 1's look
            if (roots[1] != null) roots[1].ApplyLighting();
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game, ScenePath);
            Debug.Log($"[BorrowGravity] Combined {roots.Count(r => r != null)} levels into {ScenePath}.");
        }
    }
}
