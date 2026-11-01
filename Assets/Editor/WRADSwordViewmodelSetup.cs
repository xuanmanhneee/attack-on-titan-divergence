#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WRADSwordViewmodelSetup
{
    const string ArmsPath = "Assets/Models/Player/WRADArms/wrad-arms-main/arms.fbx";
    const string ArmsTexturePath = "Assets/Models/Player/WRADArms/wrad-arms-main/arm_albedo_pale.png";
    const string SwordPath = "Assets/Models/Weapons/AOTGearSword/scene.gltf";

    static readonly string[] ScenePaths =
    {
        "Assets/Scenes/SampleScene.unity",
        "Assets/Scenes/Chapter0_TrainingCamp.unity"
    };

    [MenuItem("Attack on Titan/Install WRAD Sword Viewmodel")]
    public static void Install()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        GameObject arms = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArmsTexturePath);
        GameObject sword = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
        if (arms == null || texture == null || sword == null)
            throw new InvalidOperationException("WRAD arms, texture, or AOT sword asset is missing.");

        string reopen = SceneManager.GetActiveScene().path;
        foreach (string path in ScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ODMController odm = UnityEngine.Object.FindFirstObjectByType<ODMController>();
            if (odm == null) throw new InvalidOperationException(path + " has no ODMController.");

            FirstPersonSwordViewmodel component = odm.GetComponent<FirstPersonSwordViewmodel>();
            if (component == null) component = odm.gameObject.AddComponent<FirstPersonSwordViewmodel>();
            Camera camera = odm.cameraTransform != null ? odm.cameraTransform.GetComponent<Camera>() : null;
            if (camera == null) throw new InvalidOperationException(path + " has no first-person Camera.");
            component.Configure(odm, camera, arms, sword, texture);
            EditorUtility.SetDirty(component);

            DisableLegacyCube(odm.transform.Find("OdmCubeLeft"));
            DisableLegacyCube(odm.transform.Find("OdmCubeRight"));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(reopen))
            EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log("Installed WRAD CC0 rigged arms and imported AOT swords in both playable scenes.");
    }

    static void DisableLegacyCube(Transform legacy)
    {
        if (legacy == null) return;
        Renderer renderer = legacy.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
        Collider collider = legacy.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
    }
}
#endif
