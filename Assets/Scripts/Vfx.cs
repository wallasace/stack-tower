using System.Collections.Generic;
using UnityEngine;

// Efeitos visuais do Toybox World (sistema de partículas próprio, leve, sem módulo extra):
// - lascas de madeira 3D (pedacinhos de bloco com a cor da tinta e madeira crua) que quicam no chão;
// - poeira de serragem que se espalha quando um bloco assenta;
// - estrelinhas e anel de brilho em HDR (o bloom do PostFX faz o "glow") no encaixe perfeito;
// - confete de papel colorido caindo na frente da câmera no novo recorde.
public class Vfx : MonoBehaviour
{
    class P
    {
        public Transform t;
        public Renderer r;
        public Vector3 vel, spin, baseScale;
        public float life, maxLife, drag, gravity, flutter;
        public bool bounce, fade, grow;
        public Color color;
        public MaterialPropertyBlock mpb;
        public SpriteRenderer sr;
    }

    static Vfx inst;
    readonly List<P> live = new List<P>();
    public static float FloorY = -6.25f;

    static Mesh chipMesh;
    static Sprite starSprite, puffSprite;
    static Material sparkleMat;

    static Vfx I
    {
        get
        {
            if (!inst) inst = new GameObject("Vfx").AddComponent<Vfx>();
            return inst;
        }
    }

    // ---------- Lascas de madeira ----------

    public static void WoodChips(Vector3 pos, Color paint, float side, int count = 12)
    {
        if (!chipMesh) chipMesh = ToyMeshes.RoundedBox(new Vector3(0.12f, 0.08f, 0.1f), 0.02f, 1);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Chip");
            go.transform.position = pos + Random.insideUnitSphere * 0.15f;
            go.transform.rotation = Random.rotation;
            float s = Random.Range(0.6f, 1.5f);
            go.transform.localScale = Vector3.one * s;
            go.AddComponent<MeshFilter>().sharedMesh = chipMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ToyWorld.WoodMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            // Metade das lascas mostra a tinta, a outra metade a madeira crua por dentro
            var c = Random.value < 0.55f ? paint : Color.Lerp(ToyWorld.Wood, Color.white, Random.value * 0.2f);
            ToyWorld.Paint(mr, c, Vector3.one * 100f, Random.value * 10f);
            I.live.Add(new P
            {
                t = go.transform, r = mr, baseScale = go.transform.localScale,
                vel = new Vector3(side * Random.Range(1.5f, 4.5f), Random.Range(1.5f, 5f), Random.Range(-2.5f, 1f)),
                spin = Random.insideUnitSphere * 720f,
                life = Random.Range(1.2f, 1.8f), gravity = 14f, drag = 0.4f, bounce = true,
            });
        }
    }

    // ---------- Poeira de serragem ----------

    public static void Dust(Vector3 pos, float width, int count = 14)
    {
        if (!puffSprite) puffSprite = Util.Glow();
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(-0.5f, 0.5f) * width;
            var sr = Util.MakeSprite("Dust", puffSprite, new Color(0.96f, 0.86f, 0.7f, 0.5f),
                pos + new Vector3(x, Random.Range(-0.05f, 0.1f), -0.5f), Vector2.one * Random.Range(0.35f, 0.6f), 3);
            var dir = new Vector3(Mathf.Sign(x) * Random.Range(0.4f, 1.6f), Random.Range(0.1f, 0.6f), Random.Range(-0.6f, 0.1f));
            I.live.Add(new P
            {
                t = sr.transform, sr = sr, baseScale = sr.transform.localScale, color = sr.color,
                vel = dir, life = Random.Range(0.5f, 0.9f), drag = 3.5f, gravity = -0.4f, fade = true, grow = true,
            });
        }
    }

    // ---------- Brilho do encaixe perfeito ----------

    public static void Sparkle(Vector3 pos, float width, int count = 10)
    {
        if (!starSprite) starSprite = StarSprite();
        if (!sparkleMat)
        {
            sparkleMat = new Material(Shader.Find("Sprites/Default")) { name = "SparkleHDR" };
            sparkleMat.SetColor("_Color", new Color(3.5f, 3.1f, 2.2f, 1f)); // acima de 1: o bloom acende
        }
        for (int i = 0; i < count; i++)
        {
            var sr = Util.MakeSprite("Sparkle", starSprite, new Color(1f, 0.92f, 0.6f, 1f),
                pos + new Vector3(Random.Range(-0.5f, 0.5f) * width, Random.Range(0f, 0.3f), -0.6f),
                Vector2.one * Random.Range(0.25f, 0.55f), 6);
            sr.sharedMaterial = sparkleMat;
            I.live.Add(new P
            {
                t = sr.transform, sr = sr, baseScale = sr.transform.localScale, color = sr.color,
                vel = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(1.2f, 3.2f), 0),
                spin = new Vector3(0, 0, Random.Range(-360f, 360f)),
                life = Random.Range(0.45f, 0.8f), drag = 2f, gravity = 2f, fade = true,
            });
        }
        // Anel de luz que se abre a partir do bloco
        var ring = Util.MakeSprite("Ring", Util.Glow(), new Color(1f, 0.95f, 0.75f, 0.6f), pos + Vector3.back * 0.6f,
            new Vector2(width * 1.1f, 0.6f), 5);
        ring.sharedMaterial = sparkleMat;
        I.live.Add(new P
        {
            t = ring.transform, sr = ring, baseScale = ring.transform.localScale, color = ring.color,
            life = 0.35f, fade = true, grow = true,
        });
    }

    // ---------- Confete do novo recorde ----------

    public static void Confetti(Camera cam, int count = 90)
    {
        if (!cam) return;
        Color[] colors = { ToyWorld.Coral, ToyWorld.Yellow, ToyWorld.Mint, ToyWorld.Royal, ToyWorld.Pink, ToyWorld.Grass, ToyWorld.Purple };
        // Papel de verdade (malha opaca fininha): escreve profundidade, então a profundidade de campo não o desfoca
        var paper = ToyMeshes.RoundedBox(new Vector3(0.1f, 0.17f, 0.008f), 0.003f, 1);
        for (int i = 0; i < count; i++)
        {
            var vp = new Vector3(Random.Range(0.05f, 0.95f), Random.Range(1.02f, 1.4f), 7f);
            var go = new GameObject("Confetti");
            go.transform.position = cam.ViewportToWorldPoint(vp);
            go.transform.rotation = Random.rotation;
            go.AddComponent<MeshFilter>().sharedMesh = paper;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ToyWorld.WoodMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ToyWorld.Paint(mr, colors[i % colors.Length], Vector3.one * 100f, Random.value * 10f);
            I.live.Add(new P
            {
                t = go.transform, r = mr, baseScale = go.transform.localScale,
                vel = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-1.5f, -0.5f), 0),
                spin = new Vector3(Random.Range(-250f, 250f), Random.Range(-350f, 350f), Random.Range(-250f, 250f)),
                life = Random.Range(2.5f, 3.6f), gravity = 1.6f, drag = 1.2f, flutter = Random.Range(1.5f, 3f),
            });
        }
    }

    // Estrela de 4 pontas com núcleo brilhante
    static Sprite StarSprite()
    {
        const int size = 64;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - size / 2f) / (size / 2f), dy = (y + 0.5f - size / 2f) / (size / 2f);
                float cross = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(dx) * 9f) * Mathf.Clamp01(1f - Mathf.Abs(dy)),
                                        Mathf.Clamp01(1f - Mathf.Abs(dy) * 9f) * Mathf.Clamp01(1f - Mathf.Abs(dx)));
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                float a = Mathf.Clamp01(cross * cross + core * core);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var p = live[i];
            p.life -= dt;
            if (p.life <= 0f || !p.t) { if (p.t) Destroy(p.t.gameObject); live.RemoveAt(i); continue; }
            float k = Mathf.Clamp01(p.life / Mathf.Max(p.maxLife, 0.0001f));
            if (p.maxLife <= 0f) { p.maxLife = p.life + dt; k = 1f; }

            p.vel += Vector3.down * p.gravity * dt;
            p.vel *= 1f / (1f + p.drag * dt);
            var pos = p.t.position + p.vel * dt;
            if (p.flutter > 0f) pos.x += Mathf.Sin(Time.time * p.flutter * 3f + i) * 0.6f * dt;
            if (p.bounce && pos.y < FloorY + 0.04f)
            {
                pos.y = FloorY + 0.04f;
                p.vel = new Vector3(p.vel.x * 0.5f, -p.vel.y * 0.35f, p.vel.z * 0.5f);
                p.spin *= 0.5f;
            }
            p.t.position = pos;
            if (p.spin != Vector3.zero) p.t.Rotate(p.spin * dt, Space.Self);

            float scale = p.grow ? Mathf.Lerp(2.2f, 1f, k) : (k < 0.25f ? k / 0.25f : 1f);
            p.t.localScale = p.baseScale * scale;
            if (p.fade && p.sr)
            {
                var c = p.color;
                c.a *= Mathf.Clamp01(k * 1.6f);
                p.sr.color = c;
            }
        }
    }
}
