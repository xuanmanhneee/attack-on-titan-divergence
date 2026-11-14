using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Removes orphaned MonoBehaviour components from currently loaded scenes.
/// This is intentionally limited to loaded scenes so imported third-party
/// sample scenes are not modified just because they exist in the project.
/// </summary>
public static class MissingScriptRepair
{
    [InitializeOnLoadMethod]
    static void RepairAfterScriptReload()
    {
        // Run after Unity finishes reconstructing the active scene so orphaned
        // components from an older scene version are cleaned automatically.
        EditorApplication.delayCall += RepairLoadedScenes;
    }

    [MenuItem("Attack on Titan/Repair Missing Scripts In Loaded Scenes")]
    public static void RepairLoadedScenes()
    {
        int removed = 0;
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            var scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded) continue;

            int sceneRemoved = 0;
            foreach (var root in scene.GetRootGameObjects())
                sceneRemoved += RepairHierarchy(root);

            if (sceneRemoved > 0)
            {
                removed += sceneRemoved;
                EditorSceneManager.MarkSceneDirty(scene);
                if (scene.path.StartsWith("Assets/")) EditorSceneManager.SaveScene(scene);
            }
        }

        Debug.Log(removed == 0
            ? "[MissingScriptRepair] No missing scripts found in loaded scenes."
            : $"[MissingScriptRepair] Removed {removed} missing script component(s) from loaded scenes.");
    }

    static int RepairHierarchy(GameObject go)
    {
        int removed = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        if (removed > 0) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        for (int i = 0; i < go.transform.childCount; i++)
            removed += RepairHierarchy(go.transform.GetChild(i).gameObject);
        return removed;
    }
}
