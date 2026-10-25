using System.Collections.Generic;
using UnityEngine;

// Efeitos sonoros sintetizados por código, no tema "brinquedos de madeira":
// marimba e xilofone (barras com parciais inarmônicos), blocos de madeira batendo (woodblock),
// batidas graves de madeira na madeira e chocalho de peças caindo. Nada de arquivo de áudio.
public static class Sfx
{
    const int Rate = 44100;
    enum Wave { Sine, Square, Triangle, Noise }

    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static AudioSource[] sources;
    static int next;

    public static bool Muted;

    public static void Init(GameObject host)
    {
        Muted = PlayerPrefs.GetInt("muted", 0) == 1;
        sources = new AudioSource[8];
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = host.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].spatialBlend = 0f;
            sources[i].ignoreListenerPause = true;
        }
        // Clips criados em runtime somem ao sair do Play: recria se estiverem inválidos
        if (clips.TryGetValue("click", out var existing) && existing != null) return;
        clips.Clear();
        var rng = new System.Random(3);

        // Toque de interface: woodblock agudo e curto
        clips["click"] = Clip("click", 0.12f, b => WoodBlock(b, 0f, 1500f, 0.32f));

        // Bloco encaixando: batida grave de madeira + "toc" + assentamento (duas batidinhas)
        clips["place"] = Clip("place", 0.45f, b =>
        {
            Thud(b, 0f, 150f, 0.75f, rng);
            WoodBlock(b, 0.004f, 560f, 0.4f);
            WoodBlock(b, 0.07f, 610f, 0.12f);
            WoodBlock(b, 0.115f, 640f, 0.06f);
        });

        // Encaixe perfeito: duas notas de marimba (terça maior) + brilho de xilofone
        clips["perfect"] = Clip("perfect", 1.2f, b =>
        {
            Thud(b, 0f, 160f, 0.45f, rng);
            Marimba(b, 0.0f, Note(84), 0.5f);   // Dó6
            Marimba(b, 0.07f, Note(88), 0.45f); // Mi6
            Xylophone(b, 0.14f, Note(96), 0.22f); // Dó7 (brilho)
        });

        // Pedaço cortado: estalo de madeira (ruído filtrado) + batida seca
        clips["cut"] = Clip("cut", 0.3f, b =>
        {
            Crack(b, 0f, 0.08f, 0.35f, rng);
            WoodBlock(b, 0.01f, 380f, 0.25f);
        });

        // Bloco batendo na lateral: woodblock suave
        clips["bounce"] = Clip("bounce", 0.15f, b => WoodBlock(b, 0f, 820f, 0.28f));

        // Derrota: marimba descendo + peças de madeira caindo e chacoalhando
        clips["lose"] = Clip("lose", 1.8f, b =>
        {
            Marimba(b, 0.0f, Note(79), 0.4f);  // Sol5
            Marimba(b, 0.16f, Note(76), 0.4f); // Mi5
            Marimba(b, 0.32f, Note(72), 0.45f); // Dó5
            Marimba(b, 0.48f, Note(67), 0.5f); // Sol4
            float t = 0.12f;
            for (int i = 0; i < 9; i++)
            {
                Thud(b, t, 110f + (float)rng.NextDouble() * 80f, 0.5f * (1f - i / 10f), rng);
                WoodBlock(b, t + 0.003f, 300f + (float)rng.NextDouble() * 500f, 0.18f * (1f - i / 10f));
                t += 0.05f + (float)rng.NextDouble() * 0.09f;
            }
        });

        // Novo recorde: arpejo de xilofone subindo + acorde final de marimba
        clips["record"] = Clip("record", 2f, b =>
        {
            int[] arp = { 72, 76, 79, 84, 88, 91, 96 };
            for (int i = 0; i < arp.Length; i++) Xylophone(b, i * 0.075f, Note(arp[i]), 0.26f);
            float end = arp.Length * 0.075f + 0.05f;
            Marimba(b, end, Note(72), 0.4f);
            Marimba(b, end, Note(76), 0.35f);
            Marimba(b, end, Note(79), 0.35f);
            Marimba(b, end, Note(84), 0.3f);
        });

        // Usados pelo Lane Dash (mantidos)
        clips["coin"] = Build("coin", (Wave.Square, 988f, 988f, 0.06f, 0.22f), (Wave.Square, 1319f, 1319f, 0.2f, 0.22f));
        clips["move"] = Build("move", (Wave.Sine, 420f, 640f, 0.07f, 0.35f));
    }

    public static void Play(string name, float pitch = 1f, float volume = 1f)
    {
        if (Muted || sources == null || !clips.TryGetValue(name, out var clip) || clip == null) return;
        var s = sources[next];
        next = (next + 1) % sources.Length;
        s.pitch = pitch;
        s.volume = volume;
        s.clip = clip;
        s.Play();
    }

    // ---------- Instrumentos ----------

    public static float Note(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    static AudioClip Clip(string name, float seconds, System.Action<float[]> write)
    {
        var buf = new float[(int)(seconds * Rate)];
        write(buf);
        // Limitador suave + fade no fim (sem estalo)
        for (int i = 0; i < buf.Length; i++)
        {
            float fade = Mathf.Clamp01((buf.Length - i) / (Rate * 0.02f));
            buf[i] = (float)System.Math.Tanh(buf[i] * 1.1f) * fade;
        }
        var clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
        clip.SetData(buf, 0);
        return clip;
    }

    // Parcial senoidal com ataque rápido e decaimento exponencial
    static void Partial(float[] b, float t0, float freq, float amp, float decay, float attack = 0.002f)
    {
        int s = (int)(t0 * Rate);
        int len = Mathf.Min(b.Length - s, (int)(Rate * Mathf.Min(6f / decay + 0.01f, 3f)));
        double w = 2.0 * System.Math.PI * freq / Rate;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / Rate;
            float env = Mathf.Min(1f, t / attack) * Mathf.Exp(-t * decay);
            b[s + i] += (float)System.Math.Sin(w * i) * env * amp;
        }
    }

    // Marimba: barra de madeira; parciais ~1, 3.93 e 9.2 (afinação típica), graves duram mais
    public static void Marimba(float[] b, float t0, float f, float vol)
    {
        Partial(b, t0, f, vol, 5.5f);
        Partial(b, t0, f * 3.93f, vol * 0.32f, 16f);
        Partial(b, t0, f * 9.2f, vol * 0.1f, 38f);
        Partial(b, t0, f * 0.5f, vol * 0.12f, 7f, 0.004f); // corpo do ressonador
    }

    // Xilofone: mais brilhante e curto que a marimba (parcial 3x forte)
    public static void Xylophone(float[] b, float t0, float f, float vol)
    {
        Partial(b, t0, f, vol, 9f, 0.001f);
        Partial(b, t0, f * 3f, vol * 0.45f, 18f, 0.001f);
        Partial(b, t0, f * 6.1f, vol * 0.15f, 40f, 0.001f);
    }

    // Woodblock: "toc" oco, nota curta com leve queda de afinação
    public static void WoodBlock(float[] b, float t0, float f, float vol)
    {
        int s = (int)(t0 * Rate);
        int len = Mathf.Min(b.Length - s, (int)(Rate * 0.12f));
        double phase = 0;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / Rate;
            float freq = f * (1f + 0.25f * Mathf.Exp(-t * 120f));
            phase += freq / Rate;
            float env = Mathf.Min(1f, t / 0.0008f) * Mathf.Exp(-t * 45f);
            float v = Mathf.Sin(2f * Mathf.PI * (float)phase) + 0.3f * Mathf.Sin(4f * Mathf.PI * 1.47f * (float)phase);
            b[s + i] += v * env * vol;
        }
    }

    // Batida grave de madeira na madeira: senoide com queda de afinação + ruído abafado
    public static void Thud(float[] b, float t0, float f, float vol, System.Random rng)
    {
        int s = (int)(t0 * Rate);
        int len = Mathf.Min(b.Length - s, (int)(Rate * 0.25f));
        double phase = 0;
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / Rate;
            phase += f * (1f + 0.6f * Mathf.Exp(-t * 60f)) / Rate;
            float body = Mathf.Sin(2f * Mathf.PI * (float)phase) * Mathf.Exp(-t * 22f);
            lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.08f; // ruído passa-baixa
            float knock = lp * Mathf.Exp(-t * 70f) * 2.2f;
            b[s + i] += (body + knock) * vol * Mathf.Min(1f, t / 0.001f);
        }
    }

    // Estalo de madeira lascando: rajada de ruído com filtro passa-banda
    static void Crack(float[] b, float t0, float dur, float vol, System.Random rng)
    {
        int s = (int)(t0 * Rate);
        int len = Mathf.Min(b.Length - s, (int)(Rate * dur));
        float lp = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / Rate;
            float n = (float)(rng.NextDouble() * 2 - 1);
            lp += (n - lp) * 0.35f;
            hp = 0.9f * (hp + lp - prev);
            prev = lp;
            b[s + i] += hp * Mathf.Exp(-t * 55f) * vol * (1f + 0.6f * Mathf.Sin(t * 900f));
        }
    }

    // Formato antigo (sequência de notas simples), usado pelos sons do Lane Dash
    static AudioClip Build(string name, params (Wave wave, float f0, float f1, float dur, float vol)[] notes)
    {
        var data = new List<float>();
        foreach (var n in notes)
        {
            int len = (int)(n.dur * Rate);
            double phase = 0;
            for (int i = 0; i < len; i++)
            {
                float k = (float)i / len;
                float freq = Mathf.Lerp(n.f0, n.f1, k);
                phase += freq / Rate;
                float p = (float)(phase % 1.0);
                float v;
                switch (n.wave)
                {
                    case Wave.Square: v = p < 0.5f ? 1f : -1f; break;
                    case Wave.Triangle: v = 4f * Mathf.Abs(p - 0.5f) - 1f; break;
                    case Wave.Noise: v = Random.Range(-1f, 1f); break;
                    default: v = Mathf.Sin(2f * Mathf.PI * p); break;
                }
                float env = Mathf.Min(1f, i / (Rate * 0.004f)) * Mathf.Pow(1f - k, 1.5f);
                data.Add(v * env * n.vol);
            }
        }
        var clip = AudioClip.Create(name, data.Count, 1, Rate, false);
        clip.SetData(data.ToArray(), 0);
        return clip;
    }
}
