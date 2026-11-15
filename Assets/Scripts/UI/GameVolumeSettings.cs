using UnityEngine;

/// <summary>Âm lượng Master/Music/SFX lưu trong PlayerPrefs; dùng chung cho menu và gameplay.</summary>
public static class GameVolumeSettings
{
    const string MasterKey = "audio.master";
    const string MusicKey = "audio.music";
    const string SfxKey = "audio.sfx";

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterKey, 1f);
        set { PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, 0.75f);
        set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxKey, 1f);
        set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static void ApplyMasterVolume() => AudioListener.volume = MasterVolume;
}
