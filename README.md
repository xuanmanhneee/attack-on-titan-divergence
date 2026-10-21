# Attack on Titan: Divergence

A work-in-progress Unity fan game exploring fast, momentum-based ODM gear movement inspired by *Attack on Titan*.

## Current features

- First-person character controller
- Dual left/right grappling hooks
- Rope reeling and aerial movement
- Gas-powered acceleration and steering
- Manual dual-hook aiming with momentum-preserving rope constraints
- Dynamic crosshair feedback for valid hook targets
- Titan placeholder (dựng bằng code) với AI đuổi và đập, điểm yếu ở gáy
- Chém bằng kiếm: áp sát và ngắm đúng gáy để hạ gục, kiếm bị mòn và phải thay
- Đợt titan, máu người chơi, thắng/thua, tạm dừng
- HUD: máu, gas, tốc độ, độ bền kiếm
- Universal Render Pipeline (URP)

## Controls

| Input | Action |
| --- | --- |
| `WASD` | Move on the ground (`Shift` raises it to a gas-powered sprint) |
| Giữ chuột trái | Fire and hold the left hook; release to detach |
| Giữ chuột phải | Fire and hold the right hook; release to detach |
| `Space` | Manually shorten active ropes; the spool only removes harmless slack automatically |
| `Shift` | Use gas in the exact camera direction; with one hook, turn the camera to carve a smooth arc |
| Hai dây + `Space` + `Shift` | Cú phóng kép: lực kéo/gas mạnh hơn và trần tốc độ cao hơn |
| Bay sát tường | Khi đang có dây, hướng camera dọc theo tường để giữ đà và lướt qua góc |
| Đáp xuống mái | `WASD` để chạy dù vẫn giữ móc; giữ `Space` để dây kéo rời mái trở lại |
| Tiếp đất | Vận tốc được ghì một nhịp theo lực đáp để tránh bị quán tính hất khỏi mái |
| Giữ `S` khi tiếp đất | Ghì/cuộn người mạnh hơn; trong `0.2 giây` đầu phím này chưa làm nhân vật chạy lùi |
| `F` / chuột giữa | Chém; gáy trong tầm và góc nhìn sẽ hạ Titan, không cần tốc độ tối thiểu |
| `R` | Thay kiếm |
| `T` | Bật/tắt hỗ trợ thời gian: chậm khi đang tìm neo, chậm sâu với một móc, nhanh lại khi đủ hai móc |
| Điểm tiếp tế xanh | Nạp lại gas và kiếm; gas không tự hồi ở nơi khác |
| `Esc` | Tạm dừng |
| `Shift` (khi đang bám tường) | Bật ra khỏi tường theo hướng nhìn |

Controls and movement values are still being tuned during development.

## Requirements

- Unity `6000.5.6f1`
- A platform supported by the Unity project and URP

## Getting started

1. Clone the repository.
2. Open Unity Hub and select **Add > Add project from disk**.
3. Select the cloned repository folder.
4. Open the project with Unity `6000.5.6f1`.
5. Open `Assets/Scenes/SampleScene.unity` and enter Play Mode.

Unity will recreate ignored generated folders such as `Library`, `Temp`, and `Logs` the first time the project opens.

## Project status

This project is in early development. APIs, controls, scenes, and assets may change frequently.

## Disclaimer

This is an unofficial, non-commercial fan project. *Attack on Titan* and related names and properties belong to their respective owners. This repository is not affiliated with or endorsed by the rights holders.
