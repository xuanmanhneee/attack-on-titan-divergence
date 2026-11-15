# Scripts – bản đồ code

Dự án **tự dựng gần hết bằng code lúc chạy**. Trong scene chỉ có vài component; phần còn lại do script tự tạo
(`[RuntimeInitializeOnLoadMethod]` + `AddComponent`). Muốn biết "cái này từ đâu ra" thì xem bảng bên dưới.

## Thư mục

| Thư mục | Nội dung |
| --- | --- |
| `Core/` | `GameManager` (trạng thái game, nối các hệ thống), `GameInput`, `Health`, `TimeFocus` |
| `Player/` | `ODMController` (di chuyển ODM, lớn nhất), `PlayerCombat`, `PlayerStamina`, camera/viewmodel/trang bị, `Crosshair` |
| `Titan/` | `Titan` (AI), `TitanForm` (Eren biến hình), các script animation: `TitanModels`, `TitanClipPlayer`, `TitanProceduralAnim`, `TitanPunchAnimator`, `TitanFootGrounder` |
| `Missions/` | `TrostMission`, `TrostCity`, `TrostMapAuthoring`, `Chapter0Training`, `Cutscene` |
| `Audio/` | `GameAudio` (singleton SFX/nhạc), `BackgroundMusicPlayer` |
| `Vfx/` | `GameVfx` (hạt), `TransformLightning` (sét biến hình) |
| `UI/` | `GameHud`, `MangaMenu` (menu trong game), `MainMenuController` (menu scene MainMenu), `MangaGraphics`, `GameSettings` |
| `../Editor/` | Script dựng scene/model, chỉ chạy trong Editor |

## Ai tạo ai

- **Có sẵn trong scene**: `ODMController`, `Crosshair`, `FirstPersonSwordViewmodel` (Chapter0, Chapter1, SampleScene);
  `Chapter0Training` (Chapter0); `TrostMapAuthoring` (Chapter1); `MainMenuController` (MainMenu).
- **Tự tạo khi load scene** (`AfterSceneLoad`): `GameManager`, `ThirdPersonObservationCamera`, `ThirdPersonODMEquipment`.
- **`GameManager` gắn thêm vào player** nếu thiếu: `PlayerCombat`, `TimeFocus`, `TitanForm`, `Health`; và tạo `TrostMission`.
- **Singleton tạo sẵn**: `GameAudio`, `GameVfx`, `Titan` (đăng ký lúc `SubsystemRegistration`), `GameSettings`.
- **`Titan.Spawn`** tạo titan, rồi gắn `Health`, `Rigidbody` và các script animation tương ứng.
- **Menu chính**: UI dựng sẵn trong scene MainMenu (do `Editor/MainMenuSceneBuilder` sinh, `MainMenuController` điều khiển). `MangaMenu` (code) chỉ dùng trong game: Pause, Settings, Briefing, Result.

## Scene

| Scene | Vai trò |
| --- | --- |
| `MainMenu` | Menu chính |
| `Chapter0_TrainingCamp` | Màn huấn luyện |
| `Chapter1_BattleOfTrost` | Trận Trost |
| `SampleScene` | **Scene gốc**: các `Editor/Chapter*SceneBuilder` mở nó rồi dựng chương mới. Không xóa. |

## Nợ kỹ thuật (chưa làm)

- Tách `HorizontalGradientGraphic`, `MenuButtonVisual`, `GameVolumeSettings` ra khỏi `MainMenuController.cs`.
- `ODMController` (~1500 dòng) nên tách: di chuyển / dây / gas / FOV.
- `MangaMenu` (~1100 dòng) nên tách theo từng `Page`.
- Gom `Editor/*SceneBuilder` vào thư mục con; bỏ `MissingScriptRepair` khi scene đã sạch.
