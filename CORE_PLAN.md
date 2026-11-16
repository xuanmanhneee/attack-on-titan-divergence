# Plan chốt core (dọn code)

Ngày lập: 2026-11-16. Nhánh hiện tại: `claude/remove-editor-scripts`.
Mỗi task = 1 commit. Sau mỗi task chạy thử trong Unity rồi tick `[x]`.
Mỗi giai đoạn nên làm trong 1 phiên riêng.

## Đã xong trước plan này
- ODM đã tách: `ODMController` (524 dòng) + `ODMHooks`, `ODMGroundMovement`, `ODMAirMovement`,
  `ODMCollision`, `ODMAntiStuck`, `ODMRopeVisual`. Phần người chơi coi như chốt.
- Editor chỉ còn `CutsceneMenu`. UI chính là scene/prefab (`GameHud` prefab, `MainMenu` scene).

## Điều đã chốt
- UI đặt trong scene/prefab, không sinh bằng code, không dùng `OnGUI`.
- Refactor không đổi hành vi gameplay.
- Không thêm dòng Claude vào commit/PR; nhánh mới đặt `claude/<mô-tả>`.

---

## Giai đoạn 0: Dọn nhẹ
- [x] **T1. Cập nhật `Assets/Scripts/README.md`**
  - Bỏ dòng "ODMController ~1500 dòng nên tách" và `TitanImportSettings` trong mục Editor.
  - Thêm bảng các file ODM, mỗi file 1 dòng mô tả.
  - Test: không cần.
- [x] **T2. Gom log spawn titan sau cờ debug**
  - Thêm `static bool DebugLogs` trong `Titan`.
  - Bọc log ở: `Titan.cs:341`, `Titan.cs:565`, `TitanClipPlayer.cs:66`, `TitanClipPlayer.cs:83`, `TitanProceduralAnim.cs:36`.
  - Test: vào Chapter1, Console không spam khi titan spawn.

## Giai đoạn 1: Tách `Titan/Titan.cs` (889 dòng)
- [x] **T3. Tạo `Titan/TitanBuilder.cs` (static)**
  - Chuyển: `BuildBody`, `BuildPureTitan`, `CalcBounds`, `ExactBounds`, `IsWeaponRenderer`,
    `ApplyMaterials`, `SetLayerRecursively` (khoảng dòng 132–530).
  - `Titan.Create` gọi `TitanBuilder.Build(titan, def)`. Mục tiêu: `Titan.cs` ~450 dòng, chỉ còn AI.
  - Test: titan spawn đúng chiều cao, vật liệu, layer; chém gáy vẫn được.
- [x] **T4. Chia nhỏ `Titan.Update` (~100 dòng) và `MoveToward` (~95 dòng)**
  - `Update` → `UpdateTargeting`, `UpdateAttack`, `UpdateMovement`.
  - `MoveToward` → tách phần né vật cản và phần tính bước đi.
  - Chỉ đổi cấu trúc, không đổi hành vi.
  - Test: titan đuổi, đánh, không xuyên nhà như trước.

## Giai đoạn 2: Tách `Titan/TitanForm.cs` (795 dòng)
- [x] **T5. Tạo `Titan/TitanFormVisual.cs`**
  - Chuyển: `BuildVisual`, `DestroyVisual`, `FindClip`, `AnimateFists`, `RotateBone`, phần xoay xương trong `LateUpdate`.
  - Test: biến hình hiện đúng model, đủ animation chạy và đấm.
- [x] **T6. Tạo `Titan/TitanFormCombat.cs`**
  - Chuyển: `TitanPunch`, `StartPunch`, `ClipPunch`, `ResolvePunch`.
  - Test: đấm nhẹ và đấm mạnh đều gây sát thương, giết được titan.
- [x] **T7. Tách camera shake**
  - Chuyển `AddShake`, `UpdateShake`, `ResetShake`, `SetCameraLocalY` sang 1 class nhỏ (dùng chung được).
  - Mục tiêu: `TitanForm` ~300 dòng, chỉ lo trạng thái (Human/Transforming/Titan), thời gian, di chuyển.

## Giai đoạn 3: Bỏ hết `OnGUI`, gom về `GameHud`
- [x] **T8. Mở rộng prefab `Resources/UI/GameHud`**
  - Thanh Titan (fill + text + màu) trên canvas HUD.
  - Image flash toàn màn hình trên canvas FX.
  - API: `GameHud.SetTitanBar(fill, text, color)`, `HideTitanBar()`, `Flash(color, alpha)`.
- [x] **T9. Xóa `OnGUI` ở `TitanForm.cs` (~dòng 746) và `TransformLightning.cs` (~dòng 243)** (cần T8)
  - Chuyển sang gọi API của T8.
  - Test: lúc biến hình có lóe sáng; thanh hồi chiêu và thời gian titan đúng; ẩn khi pause.
- [x] **T10. Chuyển panel `Chapter0Training` (`OnGUI` ~dòng 153) sang uGUI**
  - Prefab con trong `GameHud` hoặc đặt sẵn trong scene Chapter0.
  - Test: chạy hết màn huấn luyện, panel cập nhật đúng từng bước.

## Giai đoạn 4: Chốt
- [ ] **T11. Xóa code chết trong `UI/MangaMenu.cs`**
  - Xóa trang Title, Chapters, Credits.
  - Trước khi xóa: kiểm tra mọi chỗ gọi `ShowTitle()` và `returnPage = Page.Title`;
    nếu còn dùng thì đổi sang load scene `MainMenu`.
  - Test: pause → thoát về menu, Settings → quay lại, màn kết quả.
- [ ] **T12. Smoke test và merge**
  - Chạy MainMenu → Chapter0 → Chapter1; Console không lỗi, không missing script.
  - Cập nhật README lần cuối, merge vào `main`, xóa file plan này.

## Thứ tự
T1 → T2 → T3 → T4 → T5 → T6 → T7 → T8 → T9 → T10 → T11 → T12.
T1, T2, T11 độc lập, làm lúc nào cũng được. T9 cần T8.

## Để sau (ngoài phạm vi chốt core)
- Chuyển các trang Briefing, Pause, Settings, Result của `MangaMenu` sang prefab, dùng chung Settings với `SettingsMenu`.
- Đưa Crosshair vào prefab `GameHud`.
- Xóa `SampleScene` và `New Terrain.asset` nếu không dùng.
