using UnityEditor;
using UnityEngine;

// Menu Tools > Cutscenes: chủ động quyết định lúc nào cần xem lại cutscene khi test trong Editor.
public static class CutsceneMenu
{
    const string Always = "Tools/Cutscenes/Always Play (ignore seen flags)";

    [MenuItem(Always)]
    static void ToggleAlways()
    {
        EditorPrefs.SetBool(Cutscene.ForceKey, !EditorPrefs.GetBool(Cutscene.ForceKey, false));
    }

    [MenuItem(Always, true)]
    static bool ToggleAlwaysValidate()
    {
        Menu.SetChecked(Always, EditorPrefs.GetBool(Cutscene.ForceKey, false));
        return true;
    }

    [MenuItem("Tools/Cutscenes/Reset Prologue (play next time)")]
    static void ResetPrologue() { Reset(Cutscene.PrologueFlag); }

    [MenuItem("Tools/Cutscenes/Reset First-Titan Cutscene")]
    static void ResetTitan() { Reset(Cutscene.TitanFlag); }

    [MenuItem("Tools/Cutscenes/Reset Both")]
    static void ResetBoth() { Reset(Cutscene.PrologueFlag); Reset(Cutscene.TitanFlag); }

    static void Reset(string key)
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        Debug.Log("[Cutscenes] Reset " + key);
    }
}
