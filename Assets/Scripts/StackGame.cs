using System.Collections.Generic;
using UnityEngine;

// Stack Tower — reskin "Toybox World": tábuas de madeira pintada empilhadas num mundo de brinquedo.
// Toque para soltar o bloco. O que passar da borda é cortado.
// 3 acertos perfeitos seguidos fazem o bloco crescer de novo. Jogabilidade idêntica à original.
public class StackGame : MonoBehaviour, IGame
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<StackGame>() == null)
            new GameObject("StackGame").AddComponent<StackGame>();
    }

    const float BlockH = 0.5f;
    const float BlockDepth = 0.9f;  // profundidade das tábuas
    const float Bevel = 0.09f;      // chanfro suave das arestas
    const float Range = 4f;
    const float StartWidth = 3f;
    const float PerfectTolerance = 0.12f;
    const float BaseTop = -3f;
    const int BaseBlocks = 6;
    const float BaseStep = 0.45f;   // quanto cada bloco da base é mais largo que o de cima

    // Câmera em perspectiva frontal
    const float FieldOfView = 35f;

    Camera cam;
    Transform prev, cur;
    float curWidth, speed, dir;
    

    int score, combo, startFrame;
    readonly List<GameObject> spawned = new List<GameObject>();

    void Start()
    {
        Application.targetFrameRate = 60;
        cam = Util.SetupCamera(8f);
        cam.orthographic = false;
        cam.fieldOfView = FieldOfView;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 300f;

        ToyWorld.Build(cam, FloorY);
        Vfx.FloorY = FloorY;
        Shell.OnNewRecord = () => Vfx.Confetti(cam);

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
            accent = ToyWorld.Mint,
            brandBlocks = new[] { Palette[0], Palette[1], Palette[2], Palette[3], Palette[5], Palette[6] },
        });
        Music.Play(new Music.Style
        {
            // Trilha de brinquedo: Dó - Sol - Lá menor - Fá - Dó - Sol - Fá - Sol, 104 bpm, 16 compassos
            toy = true,
            bpm = 104,
            chordRoots = new[] { 60, 55, 57, 53, 60, 55, 53, 55 },
            minor = new[] { false, false, true, false, false, false, false, false },
            seed = 12,
            volume = 0.42f,
        });
    }

    public void ResetGame()
    {
        foreach (var g in spawned) if (g) Destroy(g);
        spawned.Clear();
        blockColors.Clear();
        score = 0; combo = 0; speed = 3f; curWidth = StartWidth;

        // Base em degraus, mais larga embaixo; a cor sobe do roxo ao azul
        for (int i = 0; i <= BaseBlocks; i++)
        {
            int fromTop = BaseBlocks - i;
            var b = Block(0, BaseTop - fromTop * BlockH, StartWidth + fromTop * BaseStep, i);
            if (i == BaseBlocks) prev = b;
        }
        cur = null;
        camMode = CamMode.Menu;
        TargetCamera(out camEye, out camDist, out camPitch);
        PlaceCamera();
    }

    public void Begin()
    {
        startFrame = Time.frameCount;
        camMode = CamMode.Play; // câmera sobe e deixa espaço livre para jogar
        Spawn();
    }

    // ---------- Câmera responsiva (enquadramento da referência) ----------
    // Na partida a câmera fica rente à mesa, olhando um pouco para cima: torre grande no centro,
    // céu ocupando a maior parte da tela e o chão só como uma faixa embaixo.
    // Em telas estreitas ela se afasta até caber o vaivém inteiro do bloco.
    // Recalculada todo quadro: acompanha girar o celular e redimensionar o navegador.

    enum CamMode { Menu, Play, Over }
    CamMode camMode;
    float overTop, overBottom;
    float camEye, camDist, camPitch;

    const float SideMargin = 0.6f;
    const float FloorY = BaseTop - (BaseBlocks + 0.5f) * BlockH;
    const float BaseDistance = 15f;     // distância da torre em telas largas (como na referência)
    const float PlayPitchUp = 12f;      // olha para cima: horizonte bem baixo, muito céu
    const float EyeAboveFloor = 0.7f;   // olho quase na altura da mesa
    const float EyeBelowTop = 2.8f;     // conforme a torre sobe, a câmera acompanha o topo

    float TanHalfFov => Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);

    // Distância mínima para o vaivém do bloco caber na largura da tela
    float FitDistance =>
        Mathf.Max(BaseDistance, (Range + StartWidth / 2f + SideMargin) / (TanHalfFov * Mathf.Max(cam.aspect, 0.01f)));

    void TargetCamera(out float eye, out float dist, out float pitch)
    {
        dist = FitDistance;
        switch (camMode)
        {
            case CamMode.Menu:
                // Menu: olhar reto para o meio da torre (título acima, botões abaixo)
                // Torre um pouco menor e levemente abaixo do centro: cabe entre o selo BEST e o botão PLAY
                dist *= 1.55f;
                eye = BaseTop - BaseBlocks * BlockH / 2f + 0.1f * dist * TanHalfFov;
                pitch = 0f;
                break;
            case CamMode.Play:
                eye = Mathf.Max(FloorY + EyeAboveFloor, prev.position.y - EyeBelowTop);
                pitch = -PlayPitchUp;
                break;
            default:
                // Fim de jogo: afasta e olha reto para mostrar a torre inteira
                eye = (overTop + overBottom) / 2f;
                pitch = 0f;
                dist = Mathf.Max(dist, ((overTop - overBottom) / 2f + 2f) / TanHalfFov);
                break;
        }
    }

    void PlaceCamera()
    {
        cam.transform.rotation = Quaternion.Euler(camPitch, 0f, 0f);
        cam.transform.position = new Vector3(0f, camEye, -camDist);
    }

    int Level => BaseBlocks + score + 1;

    // Cores da referência, de baixo para cima (colors_bottom_to_top)
    static readonly Color[] Palette =
    {
        ToyWorld.Purple, ToyWorld.Pink, ToyWorld.Coral, ToyWorld.Yellow,
        ToyWorld.Grass, ToyWorld.Mint, ToyWorld.Royal,
    };

    // Um tom por bloco, como na torre da referência; depois do azul, volta suave ao roxo
    static Color ColorFor(int level)
    {
        float p = Mathf.Repeat(level, Palette.Length);
        int i = (int)p;
        return Color.Lerp(Palette[i], Palette[(i + 1) % Palette.Length], p - i);
    }

    Transform Block(float x, float y, float w, int level)
    {
        var go = new GameObject("Block");
        go.transform.position = new Vector3(x, y, 0f);
        go.AddComponent<MeshFilter>();
        go.AddComponent<OwnedMesh>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ToyWorld.WoodMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;
        blockColors[go] = ColorFor(level);
        SetWidth(go.transform, w);
        spawned.Add(go);
        return go.transform;
    }

    readonly Dictionary<GameObject, Color> blockColors = new Dictionary<GameObject, Color>();

    Color ColorOf(Transform t) => blockColors.TryGetValue(t.gameObject, out var c) ? c : Color.white;

    // Troca a malha pela caixa arredondada da nova largura (o chanfro não se deforma com escala)
    void SetWidth(Transform t, float w)
    {
        var size = new Vector3(w, BlockH, BlockDepth);
        t.GetComponent<OwnedMesh>().Set(ToyMeshes.RoundedBox(size, Bevel, 5, cached: false));
        ToyWorld.Paint(t.GetComponent<MeshRenderer>(), ColorOf(t), size / 2f, t.GetInstanceID() * 0.37f % 10f);
    }

    void Spawn()
    {
        dir = (score % 2 == 0) ? 1f : -1f;
        cur = Block(-dir * Range, prev.position.y + BlockH, curWidth, Level);
    }

    void Update()
    {
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
            // Ignora o mesmo toque que apertou o botão "Jogar"
            if (Time.frameCount != startFrame && Util.Tapped()) Drop();
        }

        TargetCamera(out float targetEye, out float targetDist, out float targetPitch);
        float k = 1f - Mathf.Exp(-4f * Time.deltaTime);
        camEye = Mathf.Lerp(camEye, targetEye, k);
        camDist = Mathf.Lerp(camDist, targetDist, k);
        camPitch = Mathf.Lerp(camPitch, targetPitch, k);
        PlaceCamera();
    }

    // Ponto na face da frente do bloco (efeitos aparecem na frente da madeira)
    static Vector3 Front(Vector3 p) => new Vector3(p.x, p.y, -BlockDepth / 2f - 0.05f);

    void Drop()
    {
        float dx = cur.position.x - prev.position.x;
        float overlap = curWidth - Mathf.Abs(dx);
        var color = ColorOf(cur);

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
            Vfx.Sparkle(cur.position + Vector3.up * BlockH * 0.5f, curWidth, 8 + Mathf.Min(combo, 6) * 2);
            Vfx.Dust(cur.position - Vector3.up * BlockH * 0.5f, curWidth, 8);
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
            Sfx.Play("cut", Random.Range(0.92f, 1.08f), 0.8f);
            // Lascas de madeira na cor do bloco
            // Lascas saem da aresta cortada; serragem na base do bloco que assentou
            Vfx.WoodChips(new Vector3(newX + Mathf.Sign(dx) * overlap / 2f, cur.position.y, 0), color, Mathf.Sign(dx));
            Vfx.Dust(cur.position - Vector3.up * BlockH * 0.5f, curWidth);
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
        // Física 2D no plano da torre: o pedaço cai e gira, como no jogo original
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
