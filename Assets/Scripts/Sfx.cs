using System.Collections.Generic;
using UnityEngine;

// Efeitos sonoros sintetizados por código (estilo retrô/arcade).
// Para trocar por arquivos .wav reais, basta substituir o clip no dicionário.
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
        sources = new AudioSource[6];
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

        clips["click"] = Build("click", (Wave.Square, 900f, 650f, 0.05f, 0.25f));
        clips["place"] = Build("place", (Wave.Triangle, 260f, 180f, 0.12f, 0.6f), (Wave.Noise, 0f, 0f, 0.04f, 0.15f));
        clips["perfect"] = Build("perfect", (Wave.Sine, 880f, 880f, 0.07f, 0.5f), (Wave.Sine, 1320f, 1320f, 0.18f, 0.5f));
        clips["coin"] = Build("coin", (Wave.Square, 988f, 988f, 0.06f, 0.22f), (Wave.Square, 1319f, 1319f, 0.2f, 0.22f));
        // Batida do bloco na lateral: "toc" curto e grave, com um pouco de ruído de impacto
        clips["bounce"] = Build("bounce", (Wave.Triangle, 340f, 220f, 0.07f, 0.55f), (Wave.Noise, 0f, 0f, 0.015f, 0.08f));
        clips["move"] = Build("move", (Wave.Sine, 420f, 640f, 0.07f, 0.35f));
        clips["lose"] = Build("lose", (Wave.Square, 420f, 70f, 0.55f, 0.3f));
        clips["record"] = Build("record",
            (Wave.Square, 523f, 523f, 0.09f, 0.2f), (Wave.Square, 659f, 659f, 0.09f, 0.2f),
            (Wave.Square, 784f, 784f, 0.09f, 0.2f), (Wave.Square, 1047f, 1047f, 0.3f, 0.2f));
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

    // Cada nota: (onda, freq inicial, freq final, duração, volume). As notas tocam em sequência.
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
                // Ataque curto + decaimento, evita estalos
                float env = Mathf.Min(1f, i / (Rate * 0.004f)) * Mathf.Pow(1f - k, 1.5f);
                data.Add(v * env * n.vol);
            }
        }
        var clip = AudioClip.Create(name, data.Count, 1, Rate, false);
        clip.SetData(data.ToArray(), 0);
        return clip;
    }
}
