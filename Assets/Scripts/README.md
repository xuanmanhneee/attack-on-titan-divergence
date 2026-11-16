# Scripts – bản đồ code

Dự án **tự dựng gần hết bằng code lúc chạy**. Trong scene chỉ có vài component; phần còn lại do script tự tạo
(`[RuntimeInitializeOnLoadMethod]` + `AddComponent`). Muốn biết "cái này từ đâu ra" thì xem bảng bên dưới.

## Thư mục

| Thư mục | Nội dung |
| --- | --- |
| `Core/` | `GameManager` (trạng thái game, nối các hệ thống), `GameInput`, `Health`, `TimeFocus` |
| `Player/` | `ODMController` (điều phối ODM) + các phần ODM, `PlayerCombat`, `PlayerStamina`, camera/viewmodel/trang bị, `Crosshair` |
| `Titan/` | `Titan` (AI), `TitanForm` (Eren biến hình), các script animation: `TitanModels`, `TitanClipPlayer`, `TitanProceduralAnim`, `TitanPunchAnimator`, `TitanFootGrounder` |
| `Missions/` | `TrostMission`, `TrostCity`, `TrostMapAuthoring`, `Chapter0Training`, `Cutscene` |
| `Audio/` | `GameAudio` (singleton SFX/nhạc), `BackgroundMusicPlayer` |
| `Vfx/` | `GameVfx` (hạt), `TransformLightning` (sét biến hình) |
| `UI/` | `GameHud` (HUD), `MangaMenu` (menu trong game), `MainMenuController` (menu scene MainMenu), `MangaGraphics`, `GameSettings` |
| `../Editor/` | Chỉ còn `CutsceneMenu` (reset cutscene để test). Không còn script nào dựng scene. |

## Các file ODM (`Player/`)

| File | Vai trò |
| --- | --- |
| `ODMController` | Điều phối: trạng thái, input, gas, FOV; gọi các phần dưới |
| `ODMHooks` | Bắn/giữ/thu móc, điểm bám |
| `ODMGroundMovement` | Di chuyển trên mặt đất |
| `ODMAirMovement` | Di chuyển trên không, lực kéo dây, lướt |
| `ODMCollision` | Đẩy ra khỏi va chạm, trượt tường, tiếp xúc |
| `ODMAntiStuck` | Chống kẹt |
| `ODMRopeVisual` | Hiển thị dây |

## Ai tạo ai

- **Player gắn sẵn trong scene** (object `Player`, prefab `Assets/Prefabs/Player.prefab`, dựng từ Chapter1):
  `ODMController` + 6 phần ODM (`ODMHooks`, `ODMGroundMovement`, `ODMAirMovement`, `ODMCollision`, `ODMAntiStuck`, `ODMRopeVisual`),
  `FirstPersonSwordViewmodel`, `ThirdPersonObservationCamera`; con `ErenHumanVisual` có `ThirdPersonODMEquipment`.
  Chapter1 và SampleScene có thêm `PlayerStamina`, `PlayerCombat`, `Health`, `TimeFocus`, `TitanForm`.
  Chapter0 không có nhóm chiến đấu này (không có `GameManager`), nên Player ở Chapter0 không phải prefab instance.
  Code không còn tự `AddComponent` cho player; thiếu component là lỗi cấu hình scene.
- **Có sẵn trong scene khác**: `Crosshair`; `Chapter0Training` (Chapter0); `TrostMapAuthoring` (Chapter1); `MainMenuController` (MainMenu).
- **Tự tạo khi load scene** (`AfterSceneLoad`): `GameManager` (không tạo ở Chapter0); `GameManager` tạo `TrostMission`.
- **Singleton tạo sẵn**: `GameAudio`, `GameVfx`, `Titan` (đăng ký lúc `SubsystemRegistration`), `GameSettings`.
- **`Titan.Spawn`** tạo titan, rồi gắn `Health`, `Rigidbody` và các script animation tương ứng.
- **UI là scene/prefab, không sinh bằng code**: menu chính nằm trong scene `MainMenu` (Main Menu Canvas), HUD là prefab `Resources/UI/GameHud` đặt sẵn trong Chapter0, Chapter1, SampleScene (3 canvas: FX 10 < Markers 15 < HUD 20). Sửa trực tiếp trong Unity, không còn builder nào ghi đè. Ngoại lệ còn lại: `MangaMenu` (Pause, Settings, Briefing, Result trong trận) vẫn dựng bằng code.

## Scene

| Scene | Vai trò |
| --- | --- |
| `MainMenu` | Menu chính |
| `Chapter0_TrainingCamp` | Màn huấn luyện |
| `Chapter1_BattleOfTrost` | Trận Trost |
| `SampleScene` | Scene mẫu cũ, không còn builder nào dùng; có thể xóa cùng `New Terrain.asset`. |

## Nợ kỹ thuật (chưa làm)

- **[LÀM SAU] `MangaMenu`** (đang ổn, tạm giữ nguyên): chuyển Briefing, Pause, Settings, Result sang canvas dựng sẵn trong scene/prefab rồi bỏ code dựng UI (~1100 dòng).
  - Trang Title, Chapters, Credits của `MangaMenu` là code chết (menu chính đã dùng scene), xóa được ngay.
  - Trang Settings trùng với `SettingsMenu` của menu chính; khi chuyển nên dùng chung một prefab.
  - Crosshair vẫn là Image trong canvas riêng của từng scene; cân nhắc đưa vào prefab `GameHud`.
- `MangaMenu` (~1100 dòng) nên tách theo từng `Page`.
