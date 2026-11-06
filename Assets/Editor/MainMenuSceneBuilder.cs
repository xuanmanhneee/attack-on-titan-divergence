using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class MainMenuSceneBuilder
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string BackgroundPath = "Assets/Textures/background.jpg";
    const string TitleFontPath = "Assets/Fonts/Ditty.ttf";
    const string FirstMenuTrackPath = "Assets/Resources/Audio/Music/theme_music_0_shinzou_wo_sasageyo.mp3";

    [InitializeOnLoadMethod]
    static void BuildWhenLegacySceneIsOpen()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) return;
            if (GameObject.Find("Start Navigation/ARCHIVES") != null) return;
            BuildCurrentScene(scene);
        };
    }

    [MenuItem("Attack on Titan/Build Main Menu Scene")]
    public static void BuildMenuScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        BuildCurrentScene(scene);
    }

    static void BuildCurrentScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            Object.DestroyImmediate(root);

        var rootObject = new GameObject("MainMenu");
        var controller = rootObject.AddComponent<MainMenuController>();

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("backgroundTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        serialized.FindProperty("titleFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
        var playlist = serialized.FindProperty("menuPlaylist");
        var firstTrack = AssetDatabase.LoadAssetAtPath<AudioClip>(FirstMenuTrackPath);
        playlist.arraySize = firstTrack == null ? 0 : 1;
        if (firstTrack != null) playlist.GetArrayElementAtIndex(0).objectReferenceValue = firstTrack;
        serialized.FindProperty("menuMusicBaseVolume").floatValue = 0.65f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CreateCamera();
        CreateEventSystem();
        controller.BuildSceneUI();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = rootObject;
        Debug.Log("[MainMenu] Authored UI hierarchy rebuilt and saved to MainMenu.unity.", rootObject);
    }

    static void CreateCamera()
    {
        var cameraObject = new GameObject("Menu Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.043f, 0.067f, 0.078f, 1f);
        camera.cullingMask = ~0;
        camera.depth = -100f;
        camera.targetDisplay = 0;
    }

    static void CreateEventSystem()
    {
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystem.transform.SetAsLastSibling();
    }
}
