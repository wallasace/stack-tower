using System.Collections.Generic;
using UnityEngine;

// Stack Tower: toque para soltar o bloco. O que passar da borda é cortado.
// 3 acertos perfeitos seguidos fazem o bloco crescer de novo.
public class StackGame : MonoBehaviour, IGame
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<StackGame>() == null)
            new GameObject("StackGame").AddComponent<StackGame>();
    }

    const float BlockH = 0.5f;
    const float Range = 4f;
    const float StartWidth = 3f;
    const float PerfectTolerance = 0.12f;
    const float CamSize = 8f;
    const float MenuCamY = -4.5f;   // no menu a torre fica no meio da tela
    const float BaseTop = -3f;
    const int BaseBlocks = 6;
    const float BaseStep = 0.45f;   // quanto cada bloco da base é mais largo que o de cima


    static readonly Color Mint = new Color(0.37f, 0.9f, 0.71f);

    Camera cam;
    Sprite blockSprite;
    Transform prev, cur;
    SpriteRenderer trail;
    float curWidth, speed, dir;

    int score, combo, startFrame;
    readonly List<GameObject> spawned = new List<GameObject>();

    void Start()
    {
        Application.targetFrameRate = 60;
        cam = Util.SetupCamera(CamSize);
        blockSprite = Util.Block();

        BuildFloor();

        trail = Util.MakeSprite("Trail", Util.HGradient(), Color.clear, Vector3.zero, new Vector2(2.4f, BlockH * 0.8f / 0.25f), -1); // sprite tem 0,25 de altura

        Shell.Create(this, new ShellTheme
        {
            title = "Stack Tower",
            tagline = "One tap. Higher.",
            scoreLabel = "HEIGHT",
            resultLabel = "BLOCKS",
            menuHint = "Tap to stack",
            playHint = "Tap to drop",
            pausePhrase = "Breathe. Keep climbing.",
            english = true,
            author = "wallasace",
            xHandle = "wall_as_ace",
            gitHubUser = "wallasace",
            accent = Mint,
            brandBlocks = new[] { Palette[0], Palette[1], Palette[2], Palette[3], Palette[5], Palette[6] },
        });
        // Clima relaxado: Lá menor - Fá - Dó - Sol, 100 bpm
        Music.Play(new Music.Style
        {
            bpm = 100,
            chordRoots = new[] { 57, 53, 60, 55 },
            minor = new[] { true, false, false, false },
            seed = 7,
            busyHats = false,
            volume = 0.35f,
        });
    }

    public void ResetGame()
    {
        foreach (var g in spawned) if (g) Destroy(g);
        spawned.Clear();
        score = 0; combo = 0; speed = 3f; curWidth = StartWidth;

        // Base em degraus, mais larga embaixo; a cor sobe do roxo
        for (int i = 0; i <= BaseBlocks; i++)
        {
            int fromTop = BaseBlocks - i;
            var b = Block(0, BaseTop - fromTop * BlockH, StartWidth + fromTop * BaseStep, i);
            if (i == BaseBlocks) prev = b;
        }
        cur = null;
        trail.color = Color.clear;
        camMode = CamMode.Menu;
        cam.orthographicSize = FitSize;
        cam.transform.position = new Vector3(0, MenuCamY, -10);
    }

    public void Begin()
    {
        startFrame = Time.frameCount;
        camMode = CamMode.Play; // câmera sobe e deixa espaço livre para jogar
        Spawn();
    }

    // ---------- Câmera responsiva ----------
    // A câmera mostra pelo menos CamSize de altura e, em telas estreitas, se afasta
    // até caber o vaivém inteiro do bloco (Range + metade do bloco + margem).
    // Recalculada todo quadro: acompanha girar o celular e redimensionar o navegador.

    enum CamMode { Menu, Play, Over }
    CamMode camMode;
    float overTop, overBottom;

    const float SideMargin = 0.6f;
    const float FloorY = BaseTop - (BaseBlocks + 0.5f) * BlockH;

    float FitSize => Mathf.Max(CamSize, (Range + StartWidth / 2f + SideMargin) / Mathf.Max(cam.aspect, 0.01f));

    void TargetCamera(out float y, out float size)
    {
        size = FitSize;
        switch (camMode)
        {
            case CamMode.Menu:
                y = MenuCamY; // torre no meio da tela
                break;
            case CamMode.Play:
                // Bloco do topo a ~44% da altura; no começo, o chão a ~11% da altura
                y = Mathf.Max(FloorY + 0.78f * size, prev.position.y + 0.12f * size);
                break;
            default:
                // Fim de jogo: afasta para mostrar a torre inteira
                size = Mathf.Max(size, (overTop - overBottom) / 2f + 2f);
                y = (overTop + overBottom) / 2f;
                break;
        }
    }

    int Level => BaseBlocks + score + 1;

    // Paleta da referência, de baixo para cima; as cores intermediárias são misturadas
    static readonly Color[] Palette =
    {
        new Color(0.62f, 0.38f, 1.00f), // roxo
        new Color(1.00f, 0.38f, 0.74f), // rosa
        new Color(1.00f, 0.55f, 0.42f), // coral
        new Color(1.00f, 0.87f, 0.33f), // amarelo
        new Color(0.58f, 0.93f, 0.44f), // verde
        new Color(0.30f, 0.89f, 0.82f), // turquesa
        new Color(0.33f, 0.67f, 1.00f), // azul
    };
    const float PaletteStep = 0.9f; // quanto da paleta cada bloco avança

    static Color ColorFor(int level)
    {
        float p = Mathf.Repeat(level * PaletteStep, Palette.Length);
        int i = (int)p;
        return Color.Lerp(Palette[i], Palette[(i + 1) % Palette.Length], p - i);
    }

    // Chão: linha de luz, halo roxo e reflexo esmaecido dos dois blocos de baixo
    void BuildFloor()
    {
        float floorY = BaseTop - (BaseBlocks + 0.5f) * BlockH;
        float bottomW = StartWidth + BaseBlocks * BaseStep;

        Util.MakeSprite("FloorHalo", Util.Glow(), new Color(0.5f, 0.35f, 1f, 0.35f),
            new Vector3(0, floorY, 0), new Vector2(bottomW + 4f, 1.6f), -3);
        Util.MakeSprite("FloorLine", Util.Glow(), new Color(0.75f, 0.6f, 1f, 0.55f),
            new Vector3(0, floorY - 0.02f, 0), new Vector2(bottomW + 2.5f, 0.12f), -2);

        for (int i = 0; i < 2; i++)
        {
            float w = StartWidth + (BaseBlocks - i) * BaseStep;
            var c = ColorFor(i);
            c.a = i == 0 ? 0.32f : 0.12f;
            // VFade é opaco em cima: forte junto ao chão, some para baixo
            Util.MakeSprite("Reflection", Util.VFade(), c,
                new Vector3(0, floorY - (i + 0.5f) * BlockH, 0), new Vector2(w / 0.125f, BlockH * 0.95f), -2);
        }
    }

    Transform Block(float x, float y, float w, int level)
    {
        var color = ColorFor(level);
        var sr = Util.MakeSprite("Block", blockSprite, color, new Vector3(x, y, 0), Vector2.one);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(w, BlockH);

        // "Backlight": cópia menor e bem desfocada do bloco, um pouco abaixo dele,
        // como uma luz da cor do bloco atrás (equivale a scale + blur + top deslocado no CSS).
        // Valores em Tools > Ajustes Visuais.
        var glow = Util.MakeSprite("Backlight", null, color, sr.transform.position, Vector2.one, -1);
        glow.drawMode = SpriteDrawMode.Sliced;
        glow.transform.SetParent(sr.transform, true);
        ApplyBacklight(sr.transform);

        spawned.Add(sr.gameObject);
        return sr.transform;
    }

    static void ApplyBacklight(Transform block)
    {
        var s = VisualSettings.I;
        var glow = block.GetChild(0).GetComponent<SpriteRenderer>();
        float w = block.GetComponent<SpriteRenderer>().size.x;
        var c = block.GetComponent<SpriteRenderer>().color;
        glow.sprite = Util.Backlight(s.blockGlowBlur);
        glow.color = new Color(c.r, c.g, c.b, s.blockGlowAlpha);
        glow.size = new Vector2(w * s.blockGlowScale + s.blockGlowBlur * 2f,
                                BlockH * s.blockGlowScale + s.blockGlowBlur * 2f);
        glow.transform.localPosition = new Vector3(0, -s.blockGlowDrop, 0);
    }

    // Reaplica o brilho em todos os blocos quando os ajustes mudam no Inspector
    int settingsVersion = -1;

    void RefreshBacklightsIfChanged()
    {
        if (settingsVersion == VisualSettings.Version) return;
        settingsVersion = VisualSettings.Version;
        foreach (var g in spawned) if (g) ApplyBacklight(g.transform);
    }

    static void SetWidth(Transform t, float w)
    {
        t.GetComponent<SpriteRenderer>().size = new Vector2(w, BlockH);
        ApplyBacklight(t);
    }

    void Spawn()
    {
        dir = (score % 2 == 0) ? 1f : -1f;
        cur = Block(-dir * Range, prev.position.y + BlockH, curWidth, Level);
    }

    void Update()
    {
        RefreshBacklightsIfChanged();
        if (Shell.I.Playing && cur)
        {
            var p = cur.position;
            p.x += dir * speed * Time.deltaTime;
            if (Mathf.Abs(p.x) > Range)
            {
                p.x = Mathf.Sign(p.x) * Range;
                dir = -dir;
                // Variação leve de altura para não cansar; um pouco mais agudo conforme a velocidade sobe
                Sfx.Play("bounce", Random.Range(0.95f, 1.05f) + (speed - 3f) * 0.02f, 0.6f);
            }
            cur.position = p;
            UpdateTrail();
            // Ignora o mesmo toque que apertou o botão "Jogar"
            if (Time.frameCount != startFrame && Util.Tapped()) Drop();
        }

        TargetCamera(out float targetY, out float targetSize);
        var cp = cam.transform.position;
        cp.y = Mathf.Lerp(cp.y, targetY, 4f * Time.deltaTime);
        cam.transform.position = cp;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, 3f * Time.deltaTime);
    }

    // Rastro atrás do bloco em movimento, mais forte quanto mais rápido
    void UpdateTrail()
    {
        var c = cur.GetComponent<SpriteRenderer>().color;
        c.a = Mathf.Lerp(0.25f, 0.5f, (speed - 3f) / 6f);
        trail.color = c;
        trail.flipX = dir < 0f;
        trail.transform.position = cur.position - new Vector3(dir * (curWidth / 2f + 1.2f), 0, 0);
    }

    void Drop()
    {
        float dx = cur.position.x - prev.position.x;
        float overlap = curWidth - Mathf.Abs(dx);
        var color = cur.GetComponent<SpriteRenderer>().color;
        trail.color = Color.clear;

        if (overlap <= 0f)
        {
            MakeFalling(cur.gameObject);
            GameOver();
            return;
        }

        if (Mathf.Abs(dx) < PerfectTolerance)
        {
            combo++;
            cur.position = new Vector3(prev.position.x, cur.position.y, 0);
            if (combo >= 3) curWidth = Mathf.Min(curWidth + 0.25f, StartWidth);
            SetWidth(cur, curWidth);
            // Mensagem logo acima do bloco que acabou de encaixar
            float msgY = cam.WorldToViewportPoint(cur.position).y + 0.045f;
            Shell.I.ShowMessage(combo >= 3 ? "PERFECT x" + combo + "!" : "PERFECT!", 0.8f, msgY);
            Sfx.Play("perfect", 1f + Mathf.Min(combo, 10) * 0.06f);
            Juice.Burst(cur.position + Vector3.up * BlockH * 0.5f, Shell.Gold, 14, 6f, 0.15f);
        }
        else
        {
            combo = 0;
            float cutW = Mathf.Abs(dx);
            float newX = prev.position.x + dx / 2f;
            float cutX = newX + Mathf.Sign(dx) * (overlap / 2f + cutW / 2f);
            var piece = Block(cutX, cur.position.y, cutW, Level);
            MakeFalling(piece.gameObject);
            curWidth = overlap;
            cur.position = new Vector3(newX, cur.position.y, 0);
            SetWidth(cur, curWidth);
            Sfx.Play("place", 1f + Mathf.Min(score, 30) * 0.01f);
            Juice.Burst(new Vector3(cutX, cur.position.y, 0), color, 6, 4f, 0.15f);
        }

        Juice.Pop(cur, 0.1f, 0.15f);
        score++;
        Shell.I.SetScore(score);
        prev = cur;
        speed = Mathf.Min(3f + score * 0.12f, 9f);
        Spawn();
    }

    void MakeFalling(GameObject g)
    {
        var rb = g.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.angularVelocity = Random.Range(-90f, 90f);
        Destroy(g, 3f);
    }

    void GameOver()
    {
        cur = null;
        Sfx.Play("lose");
        Juice.Shake(0.35f, 0.4f);
        // Afasta a câmera para mostrar a torre inteira
        overBottom = FloorY;
        overTop = prev.position.y + 1f;
        camMode = CamMode.Over;
        Shell.I.GameOver(score);
    }
}
