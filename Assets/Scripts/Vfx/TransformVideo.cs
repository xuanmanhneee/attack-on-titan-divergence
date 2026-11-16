using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Video biến hình (Resources/Vfx/transform_vfx.mp4) phát toàn màn hình, thay cho sét thủ tục ở lần biến hình
// đầu tiên của một trận. Chạy theo thời gian thật (timeScale đang = 0). Không có tiếng: tiếng do TitanForm phát.
// Sau đó loá sáng theo đúng màu của TransformLightning (trắng vàng) tới kín màn hình, giữ rồi mờ dần.
public class TransformVideo : MonoBehaviour
{
    public const string ResourcePath = "Vfx/transform_vfx";
    static readonly Color FlashColor = TransformLightning.WhiteColor;

    // Tạo sớm để video kịp Prepare; gọi Begin() khi tới đoạn sét. Trả về null nếu không có video.
    // reuse = true: phát xong không huỷ mà nạp lại sẵn để lần biến hình sau dùng tiếp (không phải nạp lại từ đầu).
    public static TransformVideo Create(bool reuse = false)
    {
        var clip = Resources.Load<VideoClip>(ResourcePath);
        if (clip == null) return null;
        var go = new GameObject("TransformVideo");
        var t = go.AddComponent<TransformVideo>();
        t.reuse = reuse;
        t.Setup(clip);
        return t;
    }

    VideoPlayer player;
    RenderTexture rt;
    RawImage image;
    Image flashImage;
    float total;
    float fullAt;
    float time = -1f; // < 0: chưa bắt đầu
    bool reuse;
    bool discarded;
    GameObject canvasObj;

    void Setup(VideoClip clip)
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvasObj = canvasGo;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        rt = new RenderTexture((int)Mathf.Max(16, clip.width), (int)Mathf.Max(16, clip.height), 0);
        var imgGo = new GameObject("Video", typeof(RectTransform));
        imgGo.transform.SetParent(canvasGo.transform, false);
        image = imgGo.AddComponent<RawImage>();
        image.texture = rt;
        image.raycastTarget = false;
        image.color = Color.clear; // ẩn tới khi có khung hình đầu
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var fit = imgGo.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // phủ kín màn hình
        fit.aspectRatio = (float)clip.width / Mathf.Max(1, (float)clip.height);

        var flashGo = new GameObject("Flash", typeof(RectTransform));
        flashGo.transform.SetParent(canvasGo.transform, false);
        flashImage = flashGo.AddComponent<Image>();
        flashImage.raycastTarget = false;
        flashImage.color = Color.clear;
        var fr = flashImage.rectTransform;
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = fr.offsetMax = Vector2.zero;

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.clip = clip;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = rt;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.isLooping = false;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.skipOnDrop = true;
        player.Prepare();
    }

    // totalTime = thời gian tới lúc trắng xoá hoàn toàn (giống TransformLightning.Play).
    // fullAt = giây (tính từ lúc bắt đầu) màn hình phải trắng kín; sau mốc đó video bị cắt hẳn.
    public void Begin(float totalTime, float fullAtTime)
    {
        total = Mathf.Max(0.8f, totalTime);
        fullAt = Mathf.Clamp(fullAtTime, 0.4f, total);
        time = 0f;
        player.Play();
    }

    // Huỷ an toàn: dừng và rút VideoPlayer khỏi RenderTexture TRƯỚC, rồi mới huỷ sau một lúc. Huỷ thẳng lúc video còn đang
    // Prepare / giải mã (hoặc Release RenderTexture đang được ghi) có thể làm sập game.
    public void Discard()
    {
        if (discarded) return;
        discarded = true;
        time = -1f;
        if (canvasObj != null) canvasObj.SetActive(false);
        if (player != null)
        {
            player.Stop();
            player.targetTexture = null;
            player.enabled = false;
        }
        Destroy(gameObject, 1f);
    }

    void Update()
    {
        if (discarded || time < 0f) return;
        time += Time.unscaledDeltaTime;

        // Chỉ hiện khi đã có khung hình; video hết thì giữ khung cuối.
        if (image.enabled && image.color.a < 1f && player.isPlaying && player.frame > 0) image.color = Color.white;

        // Loá sáng dần tới trắng kín ở fullAt, lúc đó cắt video (không mờ dần): kết thúc bằng chớp sáng,
        // rồi trắng mờ đi để lộ thế giới.
        float rampStart = Mathf.Max(0f, fullAt - 0.4f * total);
        float white = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rampStart, fullAt, time));
        if (time >= fullAt) image.enabled = false;
        if (time > total) white = Mathf.Clamp01(1.3f - (time - total) * 0.9f);
        flashImage.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, white);

        if (time > total + 1.5f)
        {
            if (reuse) Rearm(); else Discard();
        }
    }

    // Đưa về trạng thái chờ ban đầu: dừng, ẩn hình, Prepare lại để lần sau Begin() là phát được ngay.
    void Rearm()
    {
        time = -1f;
        player.Stop();
        image.enabled = true;
        image.color = Color.clear;
        flashImage.color = Color.clear;
        player.Prepare();
    }

    void OnDestroy()
    {
        if (rt != null) { rt.Release(); Destroy(rt); }
    }
}
