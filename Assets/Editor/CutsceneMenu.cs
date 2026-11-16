using UnityEditor;
using UnityEngine;

// Tools > Reset Cutscene Flags: xoá cờ "đã xem" để cutscene phát lại khi test.
public static class CutsceneMenu
{
    [MenuItem("Tools/Reset Cutscene Flags")]
    static void ResetFlags()
    {
        PlayerPrefs.DeleteKey(Cutscene.PrologueKey);
        PlayerPrefs.DeleteKey(Cutscene.TitanKey);
        PlayerPrefs.Save();
        Debug.Log("[Cutscenes] Đã reset cờ Prologue và First-Titan.");
    }
}
