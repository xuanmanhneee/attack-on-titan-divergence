# Thư mục asset

Bỏ file tải về vào đúng thư mục (Unity sẽ tự tạo file .meta):

| Thư mục | Dùng cho |
| --- | --- |
| `Models/Titans` | Model titan (ưu tiên FBX) |
| `Models/Player` | Nhân vật, tay, kiếm góc nhìn thứ nhất |
| `Models/Weapons` | Kiếm, ODM gear |
| `Models/Environment` | Nhà, tường thành, cây, đá |
| `Animations` | Animation từ Mixamo (đi, chạy, đập tay, ngã) |
| `Audio/SFX` | Âm thanh dây, gas, chém, titan |
| `Audio/Music` | Nhạc nền |
| `Textures` | Texture, skybox |
| `VFX` | Hiệu ứng hạt (khói gas, tia lửa) |

Ghi nguồn và giấy phép từng asset vào `Assets/CREDITS.md` (tên, link, giấy phép).

## Animation cho titan

- Model titan nằm trong `Resources/Titans/<Tên>/`. Unity tự đặt Rig = Humanoid cho chúng (xem `Editor/TitanImportSettings.cs`).
- Muốn có animation đi/đập tay thật: tải từ Mixamo (Format FBX, tick **In Place** cho Walk/Idle), bỏ vào
  `Resources/Titans/Animations/` và đặt tên đúng: `Idle.fbx`, `Walk.fbx` (hoặc `Run.fbx`), `Attack.fbx`.
- Không có file animation thì titan vẫn tự đi bằng animation sinh từ code.
