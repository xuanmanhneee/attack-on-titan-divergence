using System.Collections;
using UnityEngine;

// Xác titan người chơi để lại sau khi hoá lại thành người: bốc hơi, nằm một lúc rồi chìm xuống và biến mất.
public class TitanCorpse : MonoBehaviour
{
    public float size = 10f;
    public float lifetime = 8f;

    IEnumerator Start()
    {
        Vector3 mid = transform.position + Vector3.up * size * 0.1f;
        GameVfx.Steam(mid, size * 0.22f, lifetime);
        GameAudio.Play(Sfx.Thud, transform.position, Mathf.Clamp(size / 12f, 0.6f, 1.3f));
        yield return new WaitForSeconds(lifetime);

        // Bốc khói dày lần cuối rồi biến mất hẳn.
        GameVfx.Steam(mid, size * 0.35f, 3f);
        Destroy(gameObject);
    }
}
