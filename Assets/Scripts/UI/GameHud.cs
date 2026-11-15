using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD trong trận (uGUI). Giao diện là prefab Assets/Resources/UI/GameHud đặt sẵn trong scene,
/// gồm 3 canvas tách lớp: FX (viền đỏ khi trúng đòn) &lt; Markers (dấu gáy, máu titan, điểm nhiệm vụ) &lt; HUD (máu/gas/thể lực, tốc độ, mục tiêu).
/// Script này chỉ đọc trạng thái game rồi cập nhật các thành phần có sẵn trong prefab.
/// </summary>
public class GameHud : MonoBehaviour
{
    const float RefHeight = 1080f;

    static readonly Color Ivory = new Color(0.91f, 0.878f, 0.796f, 1f);
    static readonly Color Brass = new Color(0.753f, 0.541f, 0.243f, 1f);
    static readonly Color Muted = new Color(0.659f, 0.667f, 0.624f, 1f);
    static readonly Color Danger = new Color(0.86f, 0.27f, 0.2f, 1f);
    static readonly Color Good = new Color(0.36f, 0.72f, 0.58f, 1f);

    [Header("Layers (bật khi đang chơi)")]
    [SerializeField] GameObject[] layers = null;

    [Header("Vitals")]
    [SerializeField] RectTransform hpFill = null;
    [SerializeField] Text hpValue = null;
    [SerializeField] RectTransform gasFill = null;
    [SerializeField] Image gasFillImage = null;
    [SerializeField] Text gasValue = null;
    [SerializeField] RectTransform staminaFill = null;
    [SerializeField] Image staminaFillImage = null;
    [SerializeField] Text staminaValue = null;
    [SerializeField] Text statusText = null;
    [SerializeField] Text lowGasText = null;

    [Header("Speed / Objective / Messages")]
    [SerializeField] Text speedValue = null;
    [SerializeField] Text objectiveText = null;
    [SerializeField] Text subText = null;
    [SerializeField] CanvasGroup toastGroup = null;
    [SerializeField] Text toastText = null;
    [SerializeField] Text hintText = null;

    [Header("FX")]
    [SerializeField] CanvasGroup hurtGroup = null;

    [Header("Markers (template ẩn, nhân bản khi cần)")]
    [SerializeField] RectTransform napeTemplate = null;
    [SerializeField] RectTransform titanBarTemplate = null;
    [SerializeField] RectTransform missionTemplate = null;

    GameManager gm;
    ODMController odm;
    PlayerCombat combat;
    Health health;
    Camera cam;
    TimeFocus focus;
    PlayerStamina stamina;

    string toast = "";
    float toastTimer;
    float shownGas = 1f, shownHp = 1f, shownStamina = 1f;
    float hurtFlash;
    float lastHp = -1f;
    bool visible;

    class Item
    {
        public RectTransform rt;
        public Image[] images;
        public RectTransform fill;
        public Text text;
        public GameObject icon;
    }

    class Pool
    {
        public RectTransform template;
        public readonly List<Item> items = new List<Item>();
        int used;

        public void Begin() { used = 0; }

        public Item Next()
        {
            if (used >= items.Count)
            {
                var rt = Object.Instantiate(template, template.parent);
                var it = new Item
                {
                    rt = rt,
                    images = rt.GetComponentsInChildren<Image>(true),
                    fill = rt.Find("Fill") as RectTransform,
                    text = rt.GetComponentInChildren<Text>(true),
                    icon = rt.Find("Icon") != null ? rt.Find("Icon").gameObject : null
                };
                items.Add(it);
            }
            var item = items[used++];
            item.rt.gameObject.SetActive(true);
            return item;
        }

        public void End()
        {
            for (int i = used; i < items.Count; i++)
                if (items[i].rt.gameObject.activeSelf) items[i].rt.gameObject.SetActive(false);
        }
    }

    Pool napes, titanBars, missionMarks;

    // HUD là prefab GameHud đặt sẵn trong scene; GameManager chỉ nối dữ liệu vào, không tạo UI.
    public static GameHud Bind(GameManager manager, ODMController player, PlayerCombat pc, Health hp, Camera camera)
    {
        var h = FindAnyObjectByType<GameHud>(FindObjectsInactive.Include);
        if (h == null)
        {
            Debug.LogError("[GameHud] Scene chưa có prefab GameHud. Kéo Assets/Resources/UI/GameHud.prefab vào scene.");
            return null;
        }
        h.gm = manager; h.odm = player; h.combat = pc; h.health = hp; h.cam = camera;
        h.focus = player.GetComponent<TimeFocus>();
        return h;
    }

    public void Toast(string m)
    {
        toast = m;
        toastTimer = 3f;
    }

    void Awake()
    {
        napes = new Pool { template = napeTemplate };
        titanBars = new Pool { template = titanBarTemplate };
        missionMarks = new Pool { template = missionTemplate };
        SetLayers(false);
    }

    void SetLayers(bool on)
    {
        visible = on;
        foreach (var l in layers) if (l != null && l.activeSelf != on) l.SetActive(on);
    }

    void LateUpdate()
    {
        bool playing = odm != null && gm != null && gm.Current == GameManager.State.Playing;
        if (playing != visible) SetLayers(playing);
        if (!playing) return;

        float dt = Time.unscaledDeltaTime;
        toastTimer -= dt;
        if (odm != null) shownGas = Mathf.MoveTowards(shownGas, odm.GasNormalized, 2.5f * dt);
        if (health != null)
        {
            shownHp = Mathf.MoveTowards(shownHp, health.Normalized, 2f * dt);
            if (lastHp >= 0f && health.Current < lastHp - 0.01f) hurtFlash = 1f;
            lastHp = health.Current;
        }
        if (stamina == null) stamina = odm.GetComponent<PlayerStamina>();
        if (stamina != null) shownStamina = Mathf.MoveTowards(shownStamina, stamina.Normalized, 2f * dt);
        hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, 2.2f * dt);

        UpdateVitals();
        UpdateSpeed();
        UpdateObjective();
        UpdateMessages();
        hurtGroup.alpha = hurtFlash;

        UpdateNapeMarks();
        UpdateTitanBars();
        UpdateMissionMarks();
    }

    // ---------- Thanh và chỉ số ----------
    static void SetFill(RectTransform fill, float v)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(v), 1f);
    }

    void UpdateVitals()
    {
        SetFill(hpFill, shownHp);
        hpValue.text = health != null ? Mathf.CeilToInt(health.Current).ToString() : "";

        float g = odm.GasNormalized;
        SetFill(gasFill, shownGas);
        gasFillImage.color = shownGas >= 0.5f ? Good : (shownGas >= 0.2f ? Brass : Danger);
        gasValue.text = Mathf.RoundToInt(g * 100f) + "%";

        SetFill(staminaFill, shownStamina);
        bool exhausted = stamina != null && stamina.Exhausted;
        staminaFillImage.color = exhausted ? Danger : Ivory;
        staminaValue.text = stamina != null ? stamina.Percent + "%" : "";

        string msg = null; Color col = Muted;
        if (stamina != null)
        {
            if (exhausted) { msg = "KIỆT SỨC - tìm chỗ an toàn để nghỉ"; col = Danger; }
            else if (stamina.Resting) msg = "Đang nghỉ - hồi sức";
        }
        statusText.text = msg ?? "";
        statusText.color = col;

        lowGasText.enabled = g < 0.2f && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.4f;
    }

    void UpdateSpeed()
    {
        speedValue.text = odm.Speed.ToString("F0");
    }

    void UpdateObjective()
    {
        objectiveText.text = gm.Mission != null ? gm.Mission.ObjectiveText
                                                : "Đợt " + gm.Wave + "/" + gm.TotalWaves + "  -  hạ gục hết titan";
        string sub = "TITAN CÒN  <color=#C08A3E>" + Titan.All.Count + "</color>";
        if (focus != null && focus.Active) sub += "      <color=#C08A3E>TẬP TRUNG · THỜI GIAN CHẬM</color>";
        subText.text = sub;
    }

    void UpdateMessages()
    {
        bool showToast = toastTimer > 0f && !string.IsNullOrEmpty(toast);
        toastGroup.alpha = showToast ? Mathf.Clamp01(toastTimer) : 0f;
        if (showToast) toastText.text = toast;

        string h = gm.Hint;
        hintText.text = string.IsNullOrEmpty(h) ? "" : h;
    }

    // ---------- Dấu trên thế giới (canvas Markers) ----------
    // Canvas dùng Scale With Screen Size, match chiều cao, nên 1 đơn vị canvas = Screen.height / 1080 pixel.
    bool ToCanvas(Vector3 world, out Vector2 pos, out float depth)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        float k = RefHeight / Screen.height;
        pos = new Vector2((sp.x - Screen.width * 0.5f) * k, (sp.y - Screen.height * 0.5f) * k);
        depth = sp.z;
        return sp.z > 0f;
    }

    static void Tint(Item it, Color c)
    {
        foreach (var img in it.images) img.color = c;
    }

    void UpdateNapeMarks()
    {
        napes.Begin();
        if (cam != null)
        {
            foreach (Titan t in Titan.All)
            {
                if (t == null || t.IsDead || t.Nape == null) continue;
                Vector2 p; float z;
                if (!ToCanvas(t.Nape.position, out p, out z) || z > 140f) continue;
                bool inRange = Vector3.Distance(cam.transform.position, t.Nape.position) <= combat.napeReach;
                var it = napes.Next();
                float size = inRange ? 56f : 30f;
                it.rt.sizeDelta = new Vector2(size, size);
                it.rt.anchoredPosition = p;
                Tint(it, inRange ? new Color(1f, 0.86f, 0.25f, 1f) : new Color(1f, 0.3f, 0.2f, 0.75f));
            }
        }
        napes.End();
    }

    void UpdateTitanBars()
    {
        titanBars.Begin();
        if (cam != null)
        {
            foreach (Titan t in Titan.All)
            {
                if (t == null || t.IsDead) continue;
                Vector2 p; float z;
                if (!ToCanvas(t.transform.position + Vector3.up * (t.Height + 1.5f), out p, out z) || z > 100f) continue;
                var it = titanBars.Next();
                it.rt.anchoredPosition = p;
                it.rt.localScale = Vector3.one * Mathf.Clamp(60f / z, 0.6f, 1.4f);
                SetFill(it.fill, t.HealthNormalized);
            }
        }
        titanBars.End();
    }

    void UpdateMissionMarks()
    {
        missionMarks.Begin();
        if (cam != null && gm.Mission != null)
        {
            foreach (var mk in gm.Mission.Markers)
            {
                var it = missionMarks.Next();
                float dist = Vector3.Distance(cam.transform.position, mk.pos);
                string txt = mk.label + "  " + Mathf.RoundToInt(dist) + " m";
                Vector2 p; float z;
                bool front = ToCanvas(mk.pos + Vector3.up * 8f, out p, out z);
                if (front)
                {
                    p.x = Mathf.Clamp(p.x, -960f + 190f, 960f - 190f);
                    p.y = Mathf.Clamp(p.y, -260f, 410f);
                }
                else
                {
                    bool right = cam.transform.InverseTransformPoint(mk.pos).x >= 0f;
                    txt = right ? txt + "  ›››" : "‹‹‹  " + txt;
                    p = new Vector2(right ? 960f - 190f : -960f + 190f, 0f);
                }
                it.rt.anchoredPosition = p;
                it.icon.SetActive(front);
                it.text.text = txt;
                it.text.color = mk.color;
                it.images[it.images.Length - 1].color = mk.color; // Icon là Image cuối cùng
            }
        }
        missionMarks.End();
    }
}
