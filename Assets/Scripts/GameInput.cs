using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Lớp bọc input: cùng một API chạy được ở cả Input Manager cũ lẫn Input System mới.
// Nhờ vậy đổi "Active Input Handling" trong Player Settings không làm hỏng game.
//
// GetKey / GetKeyDown / GetKeyUp: đi qua bảng gán phím của người chơi (GameSettings.Remap),
//   code game cứ hỏi theo phím MẶC ĐỊNH (Q, E, Space...) và nhận về trạng thái của phím người chơi đã gán.
// GetKeyRaw / GetKeyDownRaw / GetKeyUpRaw: phím thật, không đổi (dùng cho menu).
public static class GameInput
{
    // Các phím cho phép người chơi gán trong Cài đặt
    public static readonly KeyCode[] Rebindable = BuildRebindable();

    static KeyCode[] BuildRebindable()
    {
        var list = new List<KeyCode>();
        for (int i = (int)KeyCode.A; i <= (int)KeyCode.Z; i++) list.Add((KeyCode)i);
        list.Add(KeyCode.Space);
        list.Add(KeyCode.LeftShift);
        list.Add(KeyCode.RightShift);
        list.Add(KeyCode.LeftControl);
        list.Add(KeyCode.RightControl);
        list.Add(KeyCode.LeftAlt);
        list.Add(KeyCode.Tab);
        list.Add(KeyCode.UpArrow);
        list.Add(KeyCode.DownArrow);
        list.Add(KeyCode.LeftArrow);
        list.Add(KeyCode.RightArrow);
        return list.ToArray();
    }

    // ---- API dùng trong game (có gán phím) ----
    public static bool GetKey(KeyCode k) { return GetKeyRaw(GameSettings.Remap(k)); }
    public static bool GetKeyDown(KeyCode k) { return GetKeyDownRaw(GameSettings.Remap(k)); }
    public static bool GetKeyUp(KeyCode k) { return GetKeyUpRaw(GameSettings.Remap(k)); }

    // Có phím nào (trong danh sách gán được) vừa được nhấn không
    public static bool AnyKeyDown(out KeyCode key)
    {
        for (int i = 0; i < Rebindable.Length; i++)
        {
            if (GetKeyDownRaw(Rebindable[i])) { key = Rebindable[i]; return true; }
        }
        key = KeyCode.None;
        return false;
    }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    const float MouseScale = 0.05f; // quy đổi delta pixel về gần giống "Mouse X/Y" của Input cũ

    static Key KeyToNew(KeyCode k)
    {
        if (k >= KeyCode.A && k <= KeyCode.Z) return (Key)((int)Key.A + ((int)k - (int)KeyCode.A));

        switch (k)
        {
            case KeyCode.Space: return Key.Space;
            case KeyCode.LeftShift: return Key.LeftShift;
            case KeyCode.RightShift: return Key.RightShift;
            case KeyCode.LeftControl: return Key.LeftCtrl;
            case KeyCode.RightControl: return Key.RightCtrl;
            case KeyCode.LeftAlt: return Key.LeftAlt;
            case KeyCode.Tab: return Key.Tab;
            case KeyCode.UpArrow: return Key.UpArrow;
            case KeyCode.DownArrow: return Key.DownArrow;
            case KeyCode.LeftArrow: return Key.LeftArrow;
            case KeyCode.RightArrow: return Key.RightArrow;
            case KeyCode.Escape: return Key.Escape;
            case KeyCode.Return: return Key.Enter;
            default: return Key.None;
        }
    }

    public static bool GetKeyRaw(KeyCode k)
    {
        var kb = Keyboard.current; var key = KeyToNew(k);
        return kb != null && key != Key.None && kb[key].isPressed;
    }
    public static bool GetKeyDownRaw(KeyCode k)
    {
        var kb = Keyboard.current; var key = KeyToNew(k);
        return kb != null && key != Key.None && kb[key].wasPressedThisFrame;
    }
    public static bool GetKeyUpRaw(KeyCode k)
    {
        var kb = Keyboard.current; var key = KeyToNew(k);
        return kb != null && key != Key.None && kb[key].wasReleasedThisFrame;
    }

    public static Vector2 MousePosition
    {
        get { var m = Mouse.current; return m != null ? m.position.ReadValue() : Vector2.zero; }
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
    public static bool GetKeyRaw(KeyCode k) { return Input.GetKey(k); }
    public static bool GetKeyDownRaw(KeyCode k) { return Input.GetKeyDown(k); }
    public static bool GetKeyUpRaw(KeyCode k) { return Input.GetKeyUp(k); }
    public static Vector2 MousePosition { get { return Input.mousePosition; } }
    public static bool GetMouseButton(int b) { return Input.GetMouseButton(b); }
    public static bool GetMouseButtonDown(int b) { return Input.GetMouseButtonDown(b); }
    public static bool GetMouseButtonUp(int b) { return Input.GetMouseButtonUp(b); }
    public static float GetAxis(string axis) { return Input.GetAxis(axis); }
#endif
}
