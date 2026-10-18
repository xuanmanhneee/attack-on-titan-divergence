# Attack on Titan: Divergence

A work-in-progress Unity fan game exploring fast, momentum-based ODM gear movement inspired by *Attack on Titan*.

## Current features

- First-person character controller
- Dual left/right grappling hooks
- Rope reeling and aerial movement
- Gas-powered acceleration and steering
- Smart aim, automatic slack take-up, and optional assisted release
- Dynamic crosshair feedback for valid hook targets
- Titan placeholder (dựng bằng code) với AI đuổi và đập, điểm yếu ở gáy
- Chém bằng kiếm: chém gáy khi đủ tốc độ để hạ gục, kiếm bị mòn và phải thay
- Đợt titan, máu người chơi, thắng/thua, tạm dừng
- HUD: máu, gas, tốc độ, độ bền kiếm
- Universal Render Pipeline (URP)

## Controls

| Input | Action |
| --- | --- |
| `Q` | Fire/hold the left hook |
| `E` | Fire/hold the right hook |
| `Space` | Reel the rope in strongly |
| `Shift` | Use gas while airborne or attached |
| `F` / chuột giữa | Chém |
| `R` | Thay kiếm |
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
