using UnityEngine;

// Rung màn hình bằng nhiễu Perlin (dùng thời gian thật của khung hình). Dùng chung cho mọi thứ cần rung camera.
// Người dùng đọc Y / Pitch / Roll rồi tự cộng vào camera.
public class CameraShake
{
    public float Decay = 2.6f;      // giảm mỗi giây
    public float Y, Pitch, Roll;
    public bool WasGrounded = true; // chỗ nhớ cho logic rung tiếp đất của người dùng

    float amount;

    public void Add(float a)
    {
        if (!GameSettings.ScreenShake) return; // người chơi đã tắt trong Cài đặt > Đồ họa
        amount = Mathf.Max(amount, Mathf.Clamp01(a));
    }

    public void Update(float dt)
    {
        amount = Mathf.Max(0f, amount - Decay * dt);
        float a = amount * amount * 0.5f + amount * 0.5f; // đường cong nhẹ: rung nhỏ vẫn thấy được, rung lớn gắt
        float t = Time.time * 28f;
        Y = (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * a * 0.5f;      // tối đa ~0.5m
        Pitch = (Mathf.PerlinNoise(t, 1.7f) - 0.5f) * 2f * a * 2.5f;  // độ
        Roll = (Mathf.PerlinNoise(t, 5.1f) - 0.5f) * 2f * a * 3f;     // độ
    }

    public void Reset()
    {
        amount = Y = Pitch = Roll = 0f;
        WasGrounded = true;
    }

    public static void SetLocalY(Transform cam, float y)
    {
        Vector3 p = cam.localPosition;
        p.y = y;
        cam.localPosition = p;
    }
}
