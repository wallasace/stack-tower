using UnityEngine;

// Trilha sonora chiptune gerada por código: bateria, baixo, arpejo e melodia em loop.
// Cada jogo escolhe tempo, acordes e "semente" da melodia em Music.Style.
public static class Music
{
    public struct Style
    {
        public int bpm;
        public int[] chordRoots;   // notas MIDI (57 = Lá3). Cada acorde dura 2 compassos.
        public bool[] minor;       // true = acorde menor
        public int seed;           // muda a melodia gerada
        public bool busyHats;      // chimbal em semicolcheias (mais energia)
        public float volume;
    }

    const int Rate = 44100;
    const float DuckedFactor = 0.35f;

    static AudioSource src;
    static float baseVolume;

    public static void Play(Style style)
    {
        if (!src)
        {
            var go = new GameObject("Music");
            Object.DontDestroyOnLoad(go);
            src = go.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.ignoreListenerPause = true;
        }
        baseVolume = style.volume;
        src.volume = baseVolume;
        src.clip = Generate(style);
        if (!Sfx.Muted) src.Play();
    }

    public static void SetMuted(bool muted)
    {
        if (!src) return;
        if (muted) src.Stop();
        else if (!src.isPlaying) src.Play();
    }

    // Abaixa a música (pausa / fim de jogo) sem pará-la.
    public static void SetDucked(bool ducked)
    {
        if (src) src.volume = ducked ? baseVolume * DuckedFactor : baseVolume;
    }

    static float Freq(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

    static AudioClip Generate(Style s)
    {
        var rng = new System.Random(s.seed);
        int stepLen = (int)(Rate * 60f / s.bpm / 4f); // semicolcheia
        const int StepsPerChord = 32;                  // 2 compassos 4/4
        int totalSteps = s.chordRoots.Length * StepsPerChord;
        var buf = new float[totalSteps * stepLen];

        // Motivo da melodia: 32 passos com graus do acorde; repete transposto em cada acorde
        int[] motif = new int[StepsPerChord];
        int[] motifLen = new int[StepsPerChord];
        for (int i = 0; i < StepsPerChord; i += 2)
        {
            motif[i] = -1;
            if (rng.NextDouble() < (i % 8 == 0 ? 0.9 : 0.55))
            {
                motif[i] = rng.Next(0, 5);
                motifLen[i] = rng.NextDouble() < 0.3 ? 4 : 2;
            }
        }
        for (int i = 1; i < StepsPerChord; i += 2) motif[i] = -1;

        for (int step = 0; step < totalSteps; step++)
        {
            int ci = step / StepsPerChord;
            int local = step % StepsPerChord;
            int root = s.chordRoots[ci];
            int third = s.minor[ci] ? 3 : 4;
            int[] tones = { 0, third, 7, 12, 12 + third };
            int start = step * stepLen;

            // Bateria
            if (step % 8 == 0) Kick(buf, start);
            if (step % 8 == 4) Snare(buf, start);
            if (s.busyHats || step % 2 == 0) Hat(buf, start, step % 4 == 2 ? 0.07f : 0.04f);

            // Baixo: colcheias alternando tônica e oitava
            if (step % 2 == 0)
            {
                float bassNote = root - 24 + (step % 4 == 2 ? 12 : 0);
                Tone(buf, start, stepLen * 2, Freq(bassNote), 1, 0.30f, 6f);
            }

            // Arpejo
            int arp = tones[(local % 4 == 3) ? 2 : local % 4];
            Tone(buf, start, stepLen, Freq(root + arp), 2, 0.05f, 10f);

            // Melodia (último acorde varia uma oitava acima para fechar o loop)
            if (motif[local] >= 0)
            {
                float note = root + 12 + tones[motif[local]] + (ci == s.chordRoots.Length - 1 && local >= 16 ? 12 : 0);
                Tone(buf, start, stepLen * motifLen[local], Freq(note), 0, 0.13f, 3f);
                Tone(buf, start, stepLen * motifLen[local], Freq(note) * 1.003f, 2, 0.035f, 3f);
            }
        }

        // Compressão suave para não estourar
        for (int i = 0; i < buf.Length; i++) buf[i] = (float)System.Math.Tanh(buf[i] * 1.3f) * 0.85f;

        var clip = AudioClip.Create("Music", buf.Length, 1, Rate, false);
        clip.SetData(buf, 0);
        return clip;
    }

    // wave: 0 = seno, 1 = triângulo, 2 = pulso 25%. Notas longas "dão a volta" no fim do loop.
    static void Tone(float[] buf, int start, int len, float freq, int wave, float vol, float decay)
    {
        double phase = 0;
        for (int i = 0; i < len; i++)
        {
            phase += freq / Rate;
            float p = (float)(phase % 1.0);
            float v = wave == 0 ? Mathf.Sin(2f * Mathf.PI * p)
                    : wave == 1 ? 4f * Mathf.Abs(p - 0.5f) - 1f
                    : (p < 0.25f ? 1f : -1f);
            float t = (float)i / Rate;
            float env = Mathf.Min(1f, i / (Rate * 0.005f)) * Mathf.Exp(-t * decay)
                        * Mathf.Min(1f, (len - i) / (Rate * 0.005f));
            buf[(start + i) % buf.Length] += v * env * vol;
        }
    }

    static void Kick(float[] buf, int start)
    {
        int len = (int)(Rate * 0.18f);
        double phase = 0;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / Rate;
            phase += Mathf.Lerp(140f, 45f, Mathf.Min(1f, t / 0.12f)) / Rate;
            buf[(start + i) % buf.Length] += Mathf.Sin(2f * Mathf.PI * (float)(phase % 1.0)) * Mathf.Exp(-t * 18f) * 0.6f;
        }
    }

    static readonly System.Random noise = new System.Random(1);
    static float Noise() => (float)(noise.NextDouble() * 2.0 - 1.0);

    static void Snare(float[] buf, int start)
    {
        int len = (int)(Rate * 0.16f);
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / Rate;
            float body = Mathf.Sin(2f * Mathf.PI * 190f * t) * Mathf.Exp(-t * 30f) * 0.15f;
            buf[(start + i) % buf.Length] += (Noise() * 0.22f) * Mathf.Exp(-t * 22f) + body;
        }
    }

    static void Hat(float[] buf, int start, float vol)
    {
        int len = (int)(Rate * 0.035f);
        float prev = 0;
        for (int i = 0; i < len; i++)
        {
            float n = Noise();
            float hp = n - prev; // passa-alta simples: deixa o ruído mais "agudo"
            prev = n;
            buf[(start + i) % buf.Length] += hp * vol * Mathf.Exp(-(float)i / Rate * 90f);
        }
    }
}
