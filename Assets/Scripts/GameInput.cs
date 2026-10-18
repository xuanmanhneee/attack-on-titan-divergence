using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Lớp bọc input: cùng một API chạy được ở cả Input Manager cũ lẫn Input System mới.
// Nhờ vậy đổi "Active Input Handling" trong Player Settings không làm hỏng game.
// Chỉ hỗ trợ các phím game đang dùng; cần phím mới thì thêm vào KeyToNew().
public static class GameInput
{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    const float MouseScale = 0.05f; // quy đổi delta pixel về gần giống "Mouse X/Y" của Input cũ

    static Key KeyToNew(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Q: return Key.Q;
            case KeyCode.E: return Key.E;
            case KeyCode.W: return Key.W;
            case KeyCode.A: return Key.A;
            case KeyCode.S: return Key.S;
            case KeyCode.D: return Key.D;
            case KeyCode.Space: return Key.Space;
            case KeyCode.LeftShift: return Key.LeftShift;
            case KeyCode.Escape: return Key.Escape;
            case KeyCode.R: return Key.R;
            case KeyCode.F: return Key.F;
            default: return Key.None;
        }
    }

    public static bool GetKey(KeyCode k)
    {
        var kb = Keyboard.current; var key = KeyToNew(k);
        return kb != null && key != Key.None && kb[key].isPressed;
    }
    public static bool GetKeyDown(KeyCode k)
    {
        var kb = Keyboard.current; var key = KeyToNew(k);
        return kb != null && key != Key.None && kb[key].wasPressedThisFrame;
    }
    public static bool GetKeyUp(KeyCode k)
    {
        var kb = Keyboard.current; var key = KeyToNew(k);
        return kb != null && key != Key.None && kb[key].wasReleasedThisFrame;
    }

    static UnityEngine.InputSystem.Controls.ButtonControl Btn(int b)
    {
        var m = Mouse.current; if (m == null) return null;
        return b == 0 ? m.leftButton : b == 1 ? m.rightButton : m.middleButton;
    }
    public static bool GetMouseButton(int b) { var c = Btn(b); return c != null && c.isPressed; }
    public static bool GetMouseButtonDown(int b) { var c = Btn(b); return c != null && c.wasPressedThisFrame; }
    public static bool GetMouseButtonUp(int b) { var c = Btn(b); return c != null && c.wasReleasedThisFrame; }

    public static float GetAxis(string axis)
    {
        var m = Mouse.current; var kb = Keyboard.current;
        switch (axis)
        {
            case "Mouse X": return m != null ? m.delta.ReadValue().x * MouseScale : 0f;
            case "Mouse Y": return m != null ? m.delta.ReadValue().y * MouseScale : 0f;
            case "Horizontal": return kb == null ? 0f : (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            case "Vertical":   return kb == null ? 0f : (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            default: return 0f;
        }
    }
#else
    public static bool GetKey(KeyCode k) => Input.GetKey(k);
    public static bool GetKeyDown(KeyCode k) => Input.GetKeyDown(k);
    public static bool GetKeyUp(KeyCode k) => Input.GetKeyUp(k);
    public static bool GetMouseButton(int b) => Input.GetMouseButton(b);
    public static bool GetMouseButtonDown(int b) => Input.GetMouseButtonDown(b);
    public static bool GetMouseButtonUp(int b) => Input.GetMouseButtonUp(b);
    public static float GetAxis(string axis) => Input.GetAxis(axis);
#endif
}
