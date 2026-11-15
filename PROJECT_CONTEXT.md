# Context dự án: Attack on Titan: Divergence

Ghi chú tóm tắt trạng thái dự án, tạo ngày 2026-11-14. Dùng làm tài liệu nền cho các phiên làm việc sau.

## 1. Tổng quan

- **Loại dự án:** Game fan-made phi thương mại, lấy cảm hứng từ *Attack on Titan*.
- **Trọng tâm gameplay:** Di chuyển ODM nhanh, dựa trên đà (momentum), với hai móc (trái/phải), kéo dây, gas và điều khiển trên không.
- **Engine:** Unity `6000.5.6f1`, render pipeline URP.
- **Trạng thái:** Đang phát triển sớm. API, điều khiển, scene và asset có thể đổi thường xuyên.
- **Đường dẫn:** `D:\School\Projects\Unity\Attack-On-Titan_Divergence`
- **Git:** Nhánh hiện tại `codex/odm-movement-snapshot`, nhánh chính `main`.

## 2. Tính năng đã có (theo README)

- Nhân vật góc nhìn thứ nhất, controller tự viết.
- ODM: hai móc, kéo dây (reel), gas tăng tốc và lái, ngắm móc thủ công, dây giữ đà.
- Crosshair phản hồi khi móc vào mục tiêu hợp lệ.
- Titan placeholder dựng bằng code: AI đuổi và đập, điểm yếu ở gáy.
- Chiến đấu cận chiến bằng kiếm: áp sát và chém gáy để hạ titan. Kiếm bị mòn và phải thay (`R`).
- Đợt titan (wave), máu người chơi, thắng/thua, tạm dừng (`Esc`).
- HUD: máu, gas, tốc độ, độ bền kiếm.
- Hệ thống thời gian chậm (`TimeFocus`, phím `T`).
- Điểm tiếp tế xanh để nạp lại gas và kiếm.

## 3. Điều khiển chính

| Phím | Chức năng |
| --- | --- |
| `WASD` | Di chuyển trên mặt đất (`Shift` để sprint dùng gas) |
| Chuột trái / phải | Bắn và giữ móc tương ứng; thả để nhả |
| `Space` | Rút ngắn dây đang móc |
| `Shift` | Dùng gas theo hướng camera |
| `F` / chuột giữa | Chém |
| `R` | Thay kiếm |
| `T` | Bật/tắt hỗ trợ thời gian |
| `Esc` | Tạm dừng |

Giá trị điều khiển và chuyển động vẫn đang được tinh chỉnh.

## 4. Scene và luồng game

Scene nằm trong `Assets/Scenes/`, và đã được đưa vào Build Settings theo thứ tự:

1. `MainMenu.unity`: menu chính (`MainMenuController`)
2. `Chapter0_TrainingCamp.unity`: Chương 0, huấn luyện (`Chapter0Training`)
3. `Chapter1_BattleOfTrost.unity`: Chương 1, Trận Trost (`TrostMission`, `TrostCity`)

`SampleScene.unity` vẫn còn trong dự án, README vẫn hướng dẫn mở nó, nhưng không nằm trong Build Settings. Cần cập nhật README nếu scene này không còn dùng.

Các scene khác trong `Assets/EmaceArt/Slavic World Free/` là asset bên thứ ba, không phải scene của game.

## 5. Cấu trúc mã nguồn

Khoảng **5.900 dòng C#** trong `Assets/Scripts/` (chưa tính Editor).

### Runtime (`Assets/Scripts/`)

| Nhóm | File | Vai trò |
| --- | --- | --- |
| Điều khiển | `GameInput.cs` | Lớp bọc input, hỗ trợ cả Input System mới và legacy |
| Di chuyển | `ODMController.cs` | Controller chính: móc, dây, gas, đà, va chạm tường |
| Di chuyển | `ThirdPersonODMEquipment.cs` | Thiết bị ODM ở góc nhìn thứ ba |
| Di chuyển | `TimeFocus.cs` | Làm chậm thời gian khi đang tìm neo |
| Chiến đấu | `PlayerCombat.cs` | Chém, độ bền kiếm, lunge hỗ trợ |
| Chiến đấu | `Health.cs` | Máu dùng chung |
| Titan | `Titan.cs` | AI titan, điểm gáy |
| Titan | `TitanModels.cs`, `TitanClipPlayer.cs`, `TitanProceduralAnim.cs`, `TitanFootGrounder.cs` | Model, animation (clip hoặc sinh bằng code), bám chân xuống đất |
| Nhiệm vụ | `GameManager.cs` | Quản lý trạng thái, thắng/thua, tạm dừng |
| Nhiệm vụ | `TrostMission.cs`, `TrostCity.cs`, `TrostMapAuthoring.cs` | Nhiệm vụ 3 pha ở Trost, thành phố procedural |
| Chương | `Chapter0Training.cs` | Logic chương huấn luyện |
| Menu | `MainMenuController.cs` | Menu chính |
| Hiển thị | `Crosshair.cs`, `GasHUD.cs` | HUD |
| Hiển thị | `FirstPersonCharacterVisual.cs`, `FirstPersonSwordViewmodel.cs` | Nhân vật và kiếm góc nhìn thứ nhất |
| Camera | `ThirdPersonObservationCamera.cs` | Camera quan sát góc nhìn thứ ba |

### Editor (`Assets/Editor/`)

Chỉ còn tool nhỏ chạy trong Unity Editor (UI và scene là asset dựng sẵn, không còn script sinh scene):

- `TitanImportSettings.cs`: cấu hình import model titan (Rig Humanoid).
- `CutsceneMenu.cs`: menu Tools/Cutscenes để reset prologue và cutscene biến hình khi test.

## 6. Asset và tài nguyên

- `Assets/Models/`: model (Titans, Player, Weapons, Environment).
- `Assets/Resources/Titans/`: model titan dạng FBX, animation tùy chọn đặt tên `Idle.fbx`, `Walk.fbx`/`Run.fbx`, `Attack.fbx` trong `Resources/Titans/Animations/`.
- `Assets/Resources/Audio/`, `Assets/Audio/`: âm thanh và nhạc nền.
- `Assets/Animations/`, `Assets/VFX/`, `Assets/Textures/`, `Assets/Fonts/`.
- `Assets/Chapter0/`, `Assets/Chapter1/`: terrain và material riêng cho từng chương.
- `Assets/Settings/`: cấu hình URP.
- `Assets/InputSystem_Actions.inputactions`: action map của Input System.
- `Assets/CREDITS.md`: ghi nguồn và giấy phép.

### Nguồn và giấy phép đã ghi trong CREDITS.md

| Asset | Tác giả | Giấy phép |
| --- | --- | --- |
| Attack On Titan: Gear Sword - Low Poly | Cem Alagozlu | CC BY 4.0 |
| WRAD Arms | wriks | CC0 1.0 |
| Low Poly Arms (Rigged) | DevMops | CC0 1.0 |
| Realistic ODM gear | aap75 | CC BY 4.0 |

Lưu ý: Các asset CC BY 4.0 cần ghi công khi phát hành.

## 7. Package và công cụ

Khai báo trong `Packages/manifest.json`:

- **URP** `17.5.0`
- **Input System** `1.20.0`
- **AI Navigation** `2.0.14`
- **glTFast** `6.19.0` (nạp glTF/GLB runtime)
- **Timeline** `1.8.12`, **Visual Scripting** `1.9.11`, **uGUI** `2.5.0`
- **Test Framework** `1.7.0`
- **Unity MCP** (`com.coplaydev.unity-mcp` v10.3.0, lấy từ GitHub) để AI agent điều khiển Unity Editor
- IDE: Rider `3.0.38`, Visual Studio `2.0.26`

Ngoài ra có module Unity cho physics, terrain, particle, audio, UI, v.v.

## 8. Lịch sử git gần đây

Nhánh `codex/odm-movement-snapshot`, commit mới nhất:

- `9cb15ef` Add main menu and improve Trost gameplay
- `8511662` Build baked Trost Chapter 1 scene
- `eec1f5c` Polish ODM traversal and add background music
- `e91e1ea` Add first-person sword viewmodel
- `3f8f949` Add Chapter 0, character assets, and expanded ODM gameplay
- `f8c2463` Refine landing brake and gas supply loop
- `63ed2b0` Snapshot refined ODM movement and city traversal

Các commit trước đó tập trung vào: hệ thống ODM (móc, tường, gas), titan nổi/đứng trên đất, nhiệm vụ Trost, và model titan thật.

## 9. Thay đổi chưa commit tại thời điểm ghi

- `Packages/manifest.json` (M)
- `Packages/packages-lock.json` (M)

Cần kiểm tra diff trước khi commit, vì có thể liên quan đến Unity MCP.

## 10. Quy ước và lưu ý

- Ngôn ngữ UI và ghi chú trong dự án chủ yếu là tiếng Việt. Một số mã và comment là tiếng Anh.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `TempAssetInspect/` là thư mục sinh tự động, không commit.
- Titan và nhiều đối tượng được dựng bằng code, không chỉ bằng prefab. Khi sửa, cần kiểm tra cả Editor builder tương ứng.
- Muốn có animation titan thật: tải từ Mixamo (FBX, tick **In Place**), đặt vào `Resources/Titans/Animations/`.
- Ghi nguồn cho mỗi asset mới vào `Assets/CREDITS.md`.
