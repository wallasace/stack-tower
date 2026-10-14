using System.Collections.Generic;
using UnityEngine;

// "Game feel": tremor de câmera, partículas e animação de pop.
// Roda antes dos outros scripts para desfazer o tremor antes de a câmera ser movida.
[DefaultExecutionOrder(-100)]
public class Juice : MonoBehaviour
{
    class Particle { public SpriteRenderer sr; public Vector2 vel; public float life, maxLife; public Vector3 baseScale; }
    class PopAnim { public Transform t; public Vector3 baseScale; public float time, dur, amount; }

    static Juice I;

    Camera cam;
    Sprite square;
    Vector3 applied;
    float shakeTime, shakeDur, shakeAmp;
    readonly List<Particle> particles = new List<Particle>();
    readonly List<PopAnim> pops = new List<PopAnim>();

    void Awake()
    {
        I = this;
        square = Util.Square();
    }

    void Update()
    {
        if (!cam) cam = Camera.main;
        if (cam) cam.transform.position -= applied;
        applied = Vector3.zero;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.life -= dt;
            if (p.life <= 0f) { Destroy(p.sr.gameObject); particles.RemoveAt(i); continue; }
            p.vel.y -= 14f * dt;
            p.sr.transform.position += (Vector3)(p.vel * dt);
            p.sr.transform.localScale = p.baseScale * (p.life / p.maxLife);
        }

        float udt = Time.unscaledDeltaTime;
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            var a = pops[i];
            if (!a.t) { pops.RemoveAt(i); continue; }
            a.time += udt;
            float k = Mathf.Clamp01(a.time / a.dur);
            a.t.localScale = a.baseScale * (1f + a.amount * Mathf.Sin(Mathf.PI * k));
            if (k >= 1f) { a.t.localScale = a.baseScale; pops.RemoveAt(i); }
        }

        if (shakeTime > 0f && cam)
        {
            shakeTime -= dt;
            applied = (Vector3)(Random.insideUnitCircle * shakeAmp * Mathf.Clamp01(shakeTime / shakeDur));
            cam.transform.position += applied;
        }
    }

    public static void Shake(float amplitude, float duration)
    {
        if (!I) return;
        I.shakeAmp = amplitude;
        I.shakeDur = I.shakeTime = duration;
    }

    public static void Burst(Vector3 pos, Color color, int count, float speed = 6f, float size = 0.25f)
    {
        if (!I) return;
        for (int i = 0; i < count; i++)
        {
            var sr = Util.MakeSprite("Particle", I.square, color, pos, Vector2.one * size * Random.Range(0.6f, 1.2f), 5);
            float life = Random.Range(0.35f, 0.7f);
            I.particles.Add(new Particle
            {
                sr = sr,
                vel = Random.insideUnitCircle.normalized * speed * Random.Range(0.4f, 1f) + Vector2.up * 2f,
                life = life,
                maxLife = life,
                baseScale = sr.transform.localScale,
            });
        }
    }

    // Aumenta e volta a escala original. Chame depois de definir a escala final do objeto.
    public static void Pop(Transform t, float amount = 0.2f, float duration = 0.2f)
    {
        if (!I || !t) return;
        var existing = I.pops.Find(a => a.t == t);
        if (existing != null)
        {
            existing.time = 0f;
            return;
        }
        I.pops.Add(new PopAnim { t = t, baseScale = t.localScale, dur = duration, amount = amount });
    }
}
