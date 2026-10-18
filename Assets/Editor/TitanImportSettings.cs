using UnityEditor;

// Tự đặt Rig = Humanoid cho mọi model và animation trong Assets/Resources/Titans.
// Nhờ vậy animation Mixamo dùng chung được cho các titan có rig Biped (Bip001),
// và code có thể làm titan đi/đập tay bằng cơ (muscle) mà không cần clip.
public class TitanImportSettings : AssetPostprocessor
{
    // Tăng số này để Unity import lại model khi bạn đổi quy tắc bên dưới
    public override uint GetVersion() { return 1; }

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/Resources/Titans/")) return;

        var importer = (ModelImporter)assetImporter;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }
    }
}
