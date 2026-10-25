using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public interface IGame
{
    // Volta o mundo do jogo ao estado inicial (usado pelo menu e pelo "jogar de novo").
    void ResetGame();
    // Começa a partida.
    void Begin();
}

// Textos e cores que cada jogo personaliza nas telas comuns.
public struct ShellTheme
{
    public string title;        // "STACK TOWER"
    public string tagline;      // frase abaixo do título
    public string scoreLabel;   // rótulo acima do placar na partida ("ALTURA")
    public string resultLabel;  // rótulo abaixo da pontuação final ("BLOCOS")
    public string menuHint;     // dica no rodapé do menu
    public string playHint;     // dica no rodapé da partida
    public string pausePhrase;  // frase do painel de pausa
    public Color accent;        // cor dos botões principais e do recorde
    public Color[] brandBlocks; // marca de blocos acima do título (de baixo para cima); null = sem marca
    // Opcionais (valores padrão mantêm o visual do Stack Tower)
    public bool accentLastWord;                        // pinta a última palavra do título com a cor de destaque
    public Color highlight;                            // cor do placar e do recorde; transparente = padrão
    public bool controlsHint;                          // rodapé "ESQUERDA ‹ | › DIREITA" em vez de playHint
    public System.Action<RectTransform> buildBrand;    // desenha uma marca própria acima do título
    public string author;                              // nome no "Criado por" dos créditos
    public bool english;                               // textos das telas comuns em inglês
    public string xHandle;                             // usuário no X (sem @), botão nos créditos
    public string gitHubUser;                          // usuário no GitHub, botão nos créditos
}

// Telas e fluxo comuns aos jogos: menu, HUD, pausa, fim de jogo, som e recorde.
public class Shell : MonoBehaviour
{
    public enum State { Menu, Playing, Paused, Over }

    public static Shell I { get; private set; }
    // Chamado quando aparece "novo recorde" (o jogo pode soltar confete, por exemplo)
    public static System.Action OnNewRecord;
    public State Current { get; private set; }
    public bool Playing => Current == State.Playing;
    public int Best { get; private set; }

    // Paleta "Toybox World": creme sobre o céu, madeira pintada nos painéis, marrom nos ícones
    public static readonly Color Ivory = new Color(1f, 0.949f, 0.847f);          // #FFF2D8 creme
    public static readonly Color MutedBlue = new Color(0.97f, 0.85f, 0.66f);     // creme amadeirado (textos secundários)
    public static readonly Color Gold = new Color(1f, 0.79f, 0.3f);              // mensagens de acerto
    public static readonly Color Brown = new Color(0.439f, 0.271f, 0.165f);      // #70452A ícones e texto em madeira clara
    public static readonly Color NightTop = new Color(0.07f, 0.15f, 0.29f);
    public static readonly Color NightBottom = new Color(0.03f, 0.07f, 0.15f);
    static readonly Color ButtonText = new Color(1f, 0.949f, 0.847f);            // creme sobre o botão menta
    static readonly Color CardColor = new Color(0.73f, 0.5f, 0.29f, 1f);         // tábua de madeira média
    static readonly Color GhostColor = new Color(0.95f, 0.8f, 0.58f, 1f);        // madeira clara pintada (botões secundários)
    static readonly Color OutlineColor = new Color(0.44f, 0.27f, 0.16f, 0.35f);  // contorno marrom suave
    static readonly Color Dim = new Color(0.1f, 0.06f, 0.03f, 0.58f);            // escurece o jogo com tom quente

    IGame game;
    ShellTheme theme;
    GameObject menu, hud, pause, over;
    Text title, scoreText, msgText, overScore, newRecord, soundLabel;
    RecordPill menuBest, overBest;
    Image soundIcon;
    float msgTimer;
    bool newBest;
    int rankPosition;

    public static Shell Create(IGame game, ShellTheme theme)
    {
        var go = new GameObject("Shell");
        go.AddComponent<Juice>();
        Sfx.Init(go);
        var shell = go.AddComponent<Shell>();
        shell.Build(game, theme);
        return shell;
    }

    void Build(IGame g, ShellTheme t)
    {
        I = this;
        game = g;
        theme = t;
        English = t.english;
        Best = PlayerPrefs.GetInt("best", 0);
        _ = Ranking.Entries; // carrega já (e migra o recorde antigo) antes de qualquer partida
        Util.EnsureEventSystem();
        // O céu, o sol e o cenário vêm do mundo 3D (ToyWorld), sem fundo de UI
        var canvas = Util.MakeCanvas();
        // HUD e menu ficam colados nas bordas: dentro da área segura (notch, barra de gestos).
        // Os painéis escuros cobrem a tela toda; os cartões ficam centralizados.
        var safe = Util.MakeSafeArea(canvas);

        // Partida: placar no topo, pausa no canto, dica no rodapé. O meio fica livre para jogar.
        hud = Util.MakePanel(safe, Color.clear).gameObject;
        Label(hud.transform, t.scoreLabel, 0.935f, 44, Ivory, Util.Weight.Bold, 0.12f);
        scoreText = Label(hud.transform, "0", 0.88f, 190, ScoreColor, Util.Weight.Bold, 0.02f);
        scoreText.GetComponent<Shadow>().effectDistance = new Vector2(0, -9); // número "grosso" como na referência
        msgText = Label(hud.transform, "", 0.62f, 44, Gold, Util.Weight.Bold, 0.08f);
        if (t.controlsHint) BuildControlsHint(hud.transform);
        else Outlined(Label(hud.transform, t.playHint, 0.035f, 34, Ivory, Util.Weight.SemiBold, 0.12f));
        // Pausa: botão de madeira clara com ícone marrom
        var pauseBtn = Secondary(hud.transform, "", new Vector2(1f, 1f), new Vector2(-100, -105), new Vector2(112, 112), Pause, 40);
        var pauseIcon = new GameObject("Icon").AddComponent<Image>();
        pauseIcon.transform.SetParent(pauseBtn.transform.parent, false);
        pauseIcon.sprite = Util.PauseIcon();
        pauseIcon.color = Brown;
        pauseIcon.raycastTarget = false;
        pauseIcon.rectTransform.anchoredPosition = new Vector2(0, 3); // compensa a "borda" de baixo do botão
        pauseIcon.rectTransform.sizeDelta = new Vector2(52, 52);

        // Som: canto superior esquerdo, simétrico à pausa; ícone muda entre ondas e X
        var soundBtn = Secondary(hud.transform, "", new Vector2(0f, 1f), new Vector2(100, -105), new Vector2(112, 112), ToggleSound, 40);
        soundIcon = new GameObject("Icon").AddComponent<Image>();
        soundIcon.transform.SetParent(soundBtn.transform.parent, false);
        soundIcon.color = Brown;
        soundIcon.raycastTarget = false;
        soundIcon.rectTransform.anchoredPosition = new Vector2(0, 3);
        soundIcon.rectTransform.sizeDelta = new Vector2(64, 64);

        // Menu: marca, título, frase, recorde; botão JOGAR embaixo, o jogo aparece no meio
        menu = Util.MakePanel(safe, Color.clear).gameObject;
        if (t.brandBlocks != null) BuildBrand(menu.transform, 0.86f, t.brandBlocks);
        if (t.buildBrand != null)
        {
            var brand = new GameObject("Brand").AddComponent<RectTransform>();
            brand.SetParent(menu.transform, false);
            brand.anchorMin = brand.anchorMax = new Vector2(0.5f, 0.87f);
            t.buildBrand(brand);
        }
        title = Label(menu.transform, TitleText(t), 0.785f, 120, Ivory, Util.Weight.Bold, 0.045f);
        Label(menu.transform, t.tagline, 0.738f, 42, Ivory, Util.Weight.Medium, 0.12f);
        menuBest = Pill(menu.transform, 0.685f);
        Primary(menu.transform, L("JOGAR", "PLAY"), 0.235f, new Vector2(640, 140), Play, 60);
        // Som, Ranking e Créditos lado a lado, abaixo do botão principal
        soundLabel = Secondary(menu.transform, "", new Vector2(0.5f, 0.157f), new Vector2(-320, 0), new Vector2(300, 84), ToggleSound, 26);
        Util.SetType(soundLabel, Util.Weight.SemiBold, 0.1f);
        var rankingBtn = Secondary(menu.transform, "RANKING", new Vector2(0.5f, 0.157f), Vector2.zero, new Vector2(300, 84), ShowRanking, 26);
        Util.SetType(rankingBtn, Util.Weight.SemiBold, 0.1f);
        var creditsBtn = Secondary(menu.transform, L("CRÉDITOS", "CREDITS"), new Vector2(0.5f, 0.157f), new Vector2(320, 0), new Vector2(300, 84), ShowCredits, 26);
        Util.SetType(creditsBtn, Util.Weight.SemiBold, 0.1f);
        Outlined(Label(menu.transform, t.menuHint, 0.088f, 34, Ivory, Util.Weight.SemiBold, 0.12f));

        // Pausa: jogo escurecido + painel compacto
        pause = Util.MakePanel(canvas, Dim).gameObject;
        var pcard = Card(pause.transform, new Vector2(700, 680));
        Label(pcard, L("PAUSADO", "PAUSED"), 0.8f, 82, Ivory, Util.Weight.Bold, 0.08f);
        Label(pcard, t.pausePhrase, 0.66f, 36, Ivory, Util.Weight.Medium, 0.06f);
        Primary(pcard, L("CONTINUAR", "RESUME"), 0.45f, new Vector2(580, 128), Resume, 48);
        Secondary(pcard, "MENU", new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(580, 108), GoToMenu, 40);

        // Resultado
        over = Util.MakePanel(canvas, Dim).gameObject;
        var ocard = Card(over.transform, new Vector2(740, 1000));
        Label(ocard, L("FIM DE JOGO", "GAME OVER"), 0.9f, 64, Ivory, Util.Weight.Bold, 0.08f);
        overScore = Label(ocard, "", 0.745f, 220, ScoreColor, Util.Weight.Bold, 0.02f);
        Label(ocard, t.resultLabel, 0.615f, 42, Ivory, Util.Weight.Bold, 0.16f);
        overBest = Pill(ocard, 0.515f);
        newRecord = Label(ocard, L("NOVO RECORDE!", "NEW BEST!"), 0.44f, 38, Gold, Util.Weight.Bold, 0.08f);
        Primary(ocard, L("JOGAR DE NOVO", "PLAY AGAIN"), 0.315f, new Vector2(600, 124), Play, 46);
        Secondary(ocard, "MENU", new Vector2(0.5f, 0.16f), Vector2.zero, new Vector2(600, 104), GoToMenu, 38);

        // Créditos
        credits = Util.MakePanel(canvas, Dim).gameObject;
        var ccard = Card(credits.transform, new Vector2(800, 1180));
        Label(ccard, L("CRÉDITOS", "CREDITS"), 0.93f, 64, Ivory, Util.Weight.Bold, 0.08f);
        float y = 0.82f;
        void Entry(string role, string who)
        {
            Label(ccard, role, y, 28, MutedBlue, Util.Weight.SemiBold, 0.16f);
            Label(ccard, who, y - 0.04f, 40, Ivory, Util.Weight.Medium, 0.02f);
            y -= 0.105f;
        }
        Entry(L("CRIADO POR", "CREATED BY"), string.IsNullOrEmpty(t.author) ? L("Seu nome", "Your name") : t.author);
        // Redes do autor, logo abaixo do nome; cada botão abre o link no navegador
        bool hasX = !string.IsNullOrEmpty(t.xHandle), hasGitHub = !string.IsNullOrEmpty(t.gitHubUser);
        if (hasX || hasGitHub)
        {
            float rowY = y - 0.005f; // respiro abaixo do nome
            float offset = hasX && hasGitHub ? 62f : 0f;
            if (hasX)
                SocialButton(ccard, Util.XIcon(), rowY, -offset, "https://x.com/" + t.xHandle);
            if (hasGitHub)
                SocialButton(ccard, Util.GitHubIcon(), rowY, offset, "https://github.com/" + t.gitHubUser);
            y -= 0.115f;
        }
        Entry(L("MÚSICA E EFEITOS SONOROS", "MUSIC AND SOUND EFFECTS"), string.IsNullOrEmpty(t.author) ? L("Seu nome", "Your name") : t.author);
        Entry(L("FONTE", "TYPEFACE"), L("Fredoka, de Milena Brandão", "Fredoka by Milena Brandão"));
        Entry(L("FEITO COM", "MADE WITH"), "Unity");
        var license = Label(ccard,
            L("Fonte Fredoka © 2016 The Fredoka Project Authors\n(https://github.com/hafontia/Fredoka-One), licenciada sob a\nSIL Open Font License 1.1 (scripts.sil.org/OFL).",
              "Fredoka font © 2016 The Fredoka Project Authors\n(https://github.com/hafontia/Fredoka-One), licensed under the\nSIL Open Font License 1.1 (scripts.sil.org/OFL)."),
            0.25f, 24, MutedBlue, Util.Weight.Medium);
        license.lineSpacing = 1.15f;
        Secondary(ccard, L("VOLTAR", "BACK"), new Vector2(0.5f, 0.09f), Vector2.zero, new Vector2(600, 104), CloseCredits, 38);

        BuildRanking(canvas);
        UpdateSoundLabel();
        ShowMenu();
    }

    GameObject credits;

    // Botão quadrado só com o ícone, que abre um link externo
    static void SocialButton(Transform parent, Sprite icon, float anchorY, float offsetX, string url)
    {
        var text = Secondary(parent, "", new Vector2(0.5f, anchorY), new Vector2(offsetX, 0), new Vector2(96, 96),
            () => Sfx.Play("click"), 30);
        // Abre no "soltar" do toque/clique, o que os navegadores aceitam sem bloquear como pop-up
        text.transform.parent.gameObject.AddComponent<ExternalLink>().url = url;

        var img = new GameObject("Icon").AddComponent<Image>();
        img.transform.SetParent(text.transform.parent, false);
        img.sprite = icon;
        img.color = Brown;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(44, 44);
    }

    // ---------- Ranking local ----------

    GameObject ranking;
    Text rankingEmpty;
    readonly Text[,] rankRows = new Text[Ranking.Size, 3]; // posição, pontos, data
    Ranking.Entry lastAdded; // destacada em dourado

    void BuildRanking(Transform canvas)
    {
        ranking = Util.MakePanel(canvas, Dim).gameObject;
        var card = Card(ranking.transform, new Vector2(800, 1180));
        Label(card, "RANKING", 0.93f, 64, Ivory, Util.Weight.Bold, 0.08f);

        // Cabeçalho das colunas
        Column(Label(card, "#", 0.855f, 26, MutedBlue, Util.Weight.SemiBold, 0.16f), -280f);
        Column(Label(card, theme.resultLabel, 0.855f, 26, MutedBlue, Util.Weight.SemiBold, 0.16f), -40f);
        Column(Label(card, L("DATA", "DATE"), 0.855f, 26, MutedBlue, Util.Weight.SemiBold, 0.16f), 230f);

        for (int i = 0; i < Ranking.Size; i++)
        {
            float y = 0.795f - i * 0.058f;
            rankRows[i, 0] = Column(Label(card, "", y, 36, MutedBlue, Util.Weight.Bold, 0.04f), -280f);
            rankRows[i, 1] = Column(Label(card, "", y, 44, Ivory, Util.Weight.Bold, 0.02f), -40f);
            rankRows[i, 2] = Column(Label(card, "", y, 30, MutedBlue, Util.Weight.Medium, 0.06f), 230f);
        }
        rankingEmpty = Label(card, L("Nenhuma partida ainda.\nJogue para entrar no ranking!", "No games yet.\nPlay to get on the board!"),
            0.55f, 36, MutedBlue, Util.Weight.Medium, 0.04f);
        rankingEmpty.lineSpacing = 1.2f;

        Secondary(card, L("VOLTAR", "BACK"), new Vector2(0.5f, 0.09f), Vector2.zero, new Vector2(600, 104), CloseRanking, 38);
        ranking.SetActive(false);
    }

    static Text Column(Text t, float x)
    {
        t.rectTransform.anchoredPosition = new Vector2(x, 0);
        t.rectTransform.sizeDelta = new Vector2(220, 80);
        return t;
    }

    void RefreshRanking()
    {
        var entries = Ranking.Entries;
        rankingEmpty.gameObject.SetActive(entries.Count == 0);
        string dateFormat = English ? "MMM d, yyyy" : "dd/MM/yyyy";
        var culture = English ? System.Globalization.CultureInfo.InvariantCulture : PtBr;
        for (int i = 0; i < Ranking.Size; i++)
        {
            bool has = i < entries.Count;
            bool mine = has && entries[i] == lastAdded;
            // 1º lugar e a partida mais recente em dourado
            var scoreColor = mine || i == 0 ? Gold : Ivory;
            rankRows[i, 0].text = has ? (i + 1).ToString() : "";
            rankRows[i, 1].text = has ? Format(entries[i].score) : "";
            rankRows[i, 1].color = scoreColor;
            rankRows[i, 0].color = mine ? Gold : MutedBlue;
            rankRows[i, 2].text = has ? entries[i].Date.ToString(dateFormat, culture) : "";
        }
    }

    void ShowRanking()
    {
        Sfx.Play("click");
        RefreshRanking();
        ranking.SetActive(true);
        menu.SetActive(false); // nada do menu aparecendo atrás do painel
    }

    void CloseRanking()
    {
        Sfx.Play("click");
        ranking.SetActive(false);
        menu.SetActive(true);
    }

    void ShowCredits()
    {
        Sfx.Play("click");
        credits.SetActive(true);
        menu.SetActive(false); // nada do menu aparecendo atrás do painel
    }

    void CloseCredits()
    {
        Sfx.Play("click");
        credits.SetActive(false);
        menu.SetActive(true);
    }

    // ---------- Peças visuais ----------

    static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

    // Contorno marrom em volta do texto: legível sobre o céu claro e sobre o chão de madeira
    static Text Outlined(Text t)
    {
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.36f, 0.21f, 0.12f, 0.9f);
        o.effectDistance = new Vector2(2.5f, -2.5f);
        return t;
    }

    static Text Label(Transform parent, string text, float anchorY, int size, Color color,
        Util.Weight weight = Util.Weight.Bold, float spacing = 0f)
    {
        var t = Util.MakeText(parent, anchorY, 0, size, weight, spacing);
        t.text = text;
        t.color = color;
        return t;
    }

    // Botão principal: madeira pintada na cor de destaque (menta), texto creme com sombra marrom
    Text Primary(Transform parent, string label, float anchorY, Vector2 size, UnityEngine.Events.UnityAction action, int font)
    {
        var text = Util.MakeButton(parent, label, new Vector2(0.5f, anchorY), Vector2.zero, size, theme.accent, action, font);
        text.color = ButtonText;
        text.GetComponent<Shadow>().effectColor = new Color(0.05f, 0.3f, 0.28f, 0.9f); // sombra menta escura
        text.rectTransform.offsetMin = new Vector2(0, 6); // centraliza acima da "borda" de baixo
        Util.SetType(text, Util.Weight.Bold, 0.1f);
        return text;
    }

    readonly System.Collections.Generic.List<(Image glow, Vector2 size)> buttonGlows =
        new System.Collections.Generic.List<(Image, Vector2)>();
    int settingsVersion = -1;

    // Brilho dos botões a partir de Tools > Ajustes Visuais. O degradê começa por dentro do botão
    // e some "spread" px fora dele, sem faixa de cor chapada (que formava uma "moldura").
    void ApplyButtonGlow(Image glow, Vector2 size)
    {
        var s = VisualSettings.I;
        glow.color = Fade(theme.accent, s.buttonGlowAlpha);
        glow.pixelsPerUnitMultiplier = Util.SoftRectBorderPx / s.buttonGlowBlur;
        glow.rectTransform.anchoredPosition = new Vector2(0, -s.buttonGlowDrop);
        // O núcleo nunca fica menor que zero: no mínimo, 2x o desfoque
        var total = size + Vector2.one * (s.buttonGlowSpread * 2f);
        float min = s.buttonGlowBlur * 2f;
        glow.rectTransform.sizeDelta = new Vector2(Mathf.Max(total.x, min), Mathf.Max(total.y, min));
    }

    void RefreshButtonGlowsIfChanged()
    {
        if (settingsVersion == VisualSettings.Version) return;
        settingsVersion = VisualSettings.Version;
        foreach (var (glow, size) in buttonGlows) if (glow) ApplyButtonGlow(glow, size);
    }

    static Text Secondary(Transform parent, string label, Vector2 anchor, Vector2 offset, Vector2 size,
        UnityEngine.Events.UnityAction action, int font)
    {
        var text = Util.MakeButton(parent, label, anchor, offset, size, GhostColor, action, font);
        text.color = Brown;
        text.GetComponent<Shadow>().enabled = false;
        Util.SetType(text, Util.Weight.SemiBold, 0.12f);
        text.rectTransform.offsetMin = new Vector2(0, 6); // centraliza acima da "borda" de baixo
        return text;
    }

    static Transform Card(Transform parent, Vector2 size)
    {
        var go = new GameObject("Card");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = Util.PaintedPanel(); // tábua com topo claro e borda inferior escura
        img.type = Image.Type.Sliced;
        img.color = CardColor;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        Util.AddDropShadow(go.transform);
        return go.transform;
    }

    // Selo "RECORDE 28" com contorno
    // Selo de recorde: rótulo e número em textos separados, centralizados juntos na face da peça
    // (acima da "espessura" de baixo) e alinhados pelo meio, independente do tamanho do número
    class RecordPill { public Text label, value; public RectTransform face; }

    static RecordPill Pill(Transform parent, float anchorY)
    {
        var go = new GameObject("Pill");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = Util.PaintedPanel();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.6f; // cantos bem redondos
        img.color = GhostColor;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY);
        rt.sizeDelta = new Vector2(300, 84);
        Util.AddDropShadow(go.transform);

        var pill = new RecordPill { face = rt };
        pill.label = Util.MakeText(go.transform, 0.5f, 0, 30, Util.Weight.Bold, 0.14f);
        pill.label.color = Brown;
        pill.value = Util.MakeText(go.transform, 0.5f, 0, 46, Util.Weight.Bold, 0.02f);
        foreach (var t in new[] { pill.label, pill.value })
        {
            t.GetComponent<Shadow>().enabled = false;
            t.rectTransform.sizeDelta = new Vector2(260, 80);
        }
        return pill;
    }

    static void BuildBrand(Transform parent, float anchorY, Color[] colors)
    {
        var root = new GameObject("Brand").AddComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, anchorY);
        const float h = 24f, gap = 3f;
        for (int i = 0; i < colors.Length; i++)
        {
            var img = new GameObject("Bar").AddComponent<Image>();
            img.transform.SetParent(root, false);
            img.sprite = Util.Rounded();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 4f; // cantos pequenos para barras baixas
            img.color = colors[i];
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(190f - i * 22f, h);
            img.rectTransform.anchoredPosition = new Vector2(0, (i - (colors.Length - 1) / 2f) * (h + gap));
        }
    }

    Color ScoreColor => theme.highlight.a > 0f ? theme.highlight : Ivory;
    Color RecordColor => theme.highlight.a > 0f ? theme.highlight : theme.accent;

    static readonly System.Globalization.CultureInfo PtBr = new System.Globalization.CultureInfo("pt-BR");

    // 1280 -> "1.280"
    public static string Format(int n) => n.ToString("N0", English ? System.Globalization.CultureInfo.InvariantCulture : PtBr);

    // Idioma das telas comuns (ShellTheme.english)
    public static bool English { get; private set; }
    public static string L(string pt, string en) => English ? en : pt;

    static string TitleText(ShellTheme t)
    {
        string s = t.title.ToUpper();
        int split = s.LastIndexOf(' ');
        if (!t.accentLastWord || split < 0) return s;
        return s.Substring(0, split + 1) + $"<color={Util.Hex(t.accent)}>{s.Substring(split + 1)}</color>";
    }

    // Rodapé com as duas metades da tela: "ESQUERDA ‹" | "DIREITA ›"
    static void BuildControlsHint(Transform parent)
    {
        foreach (var (x, label) in new[] { (0.25f, L("ESQUERDA", "LEFT") + "\n‹"), (0.75f, L("DIREITA", "RIGHT") + "\n›") })
        {
            var t = Label(parent, label, 0.055f, 34, MutedBlue);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(x, 0.055f);
            t.lineSpacing = 1.1f;
        }
        var line = new GameObject("Divider").AddComponent<Image>();
        line.transform.SetParent(parent, false);
        line.color = new Color(MutedBlue.r, MutedBlue.g, MutedBlue.b, 0.3f);
        line.raycastTarget = false;
        line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 0.055f);
        line.rectTransform.sizeDelta = new Vector2(3, 120);
    }

    void SetRecordText(RecordPill p)
    {
        p.label.text = L("RECORDE", "BEST");
        p.value.text = Format(Best);
        p.value.color = RecordColor;
        // Mede os dois textos e centraliza o conjunto; sobe 5 para compensar a borda inferior da peça
        const float gap = 14f, lift = 5f;
        float lw = p.label.preferredWidth, vw = p.value.preferredWidth;
        float total = lw + gap + vw;
        p.label.rectTransform.anchoredPosition = new Vector2(-total / 2f + lw / 2f, lift);
        p.value.rectTransform.anchoredPosition = new Vector2(total / 2f - vw / 2f, lift + 1f);
        p.face.sizeDelta = new Vector2(Mathf.Max(240f, total + 90f), p.face.sizeDelta.y);
    }

    // ---------- Fluxo ----------

    void Update()
    {
        RefreshButtonGlowsIfChanged();
        if (msgTimer > 0f && (msgTimer -= Time.unscaledDeltaTime) <= 0f) msgText.text = "";
        if (menu.activeSelf)
            title.transform.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(Time.unscaledTime * 2.5f));
    }

    void Show(GameObject screen)
    {
        menu.SetActive(screen == menu);
        pause.SetActive(screen == pause);
        over.SetActive(screen == over);
        credits.SetActive(false);
        ranking.SetActive(false);
        hud.SetActive(screen == hud || screen == pause);
    }

    void ShowMenu()
    {
        StopAllCoroutines();
        Time.timeScale = 1f;
        game.ResetGame();
        Current = State.Menu;
        Music.SetDucked(false);
        SetRecordText(menuBest);
        Show(menu);
    }

    void GoToMenu()
    {
        Sfx.Play("click");
        ShowMenu();
    }

    void Play()
    {
        Sfx.Play("click");
        StopAllCoroutines();
        Time.timeScale = 1f;
        game.ResetGame();
        SetScore(0, false);
        msgText.text = "";
        Current = State.Playing;
        Music.SetDucked(false);
        Show(hud);
        game.Begin();
    }

    void Pause()
    {
        if (!Playing) return;
        Sfx.Play("click");
        Time.timeScale = 0f;
        Current = State.Paused;
        Music.SetDucked(true);
        Show(pause);
    }

    void Resume()
    {
        Sfx.Play("click");
        Time.timeScale = 1f;
        Current = State.Playing;
        Music.SetDucked(false);
        Show(hud);
    }

    void ToggleSound()
    {
        Sfx.Muted = !Sfx.Muted;
        Music.SetMuted(Sfx.Muted);
        PlayerPrefs.SetInt("muted", Sfx.Muted ? 1 : 0);
        PlayerPrefs.Save();
        Sfx.Play("click");
        UpdateSoundLabel();
    }

    void UpdateSoundLabel()
    {
        soundLabel.text = Sfx.Muted ? L("SOM: DESLIGADO", "SOUND: OFF") : L("SOM: LIGADO", "SOUND: ON");
        soundIcon.sprite = Util.SpeakerIcon(!Sfx.Muted);
    }

    // Celular: pausa sozinho quando o app vai para segundo plano.
    void OnApplicationPause(bool paused)
    {
        if (paused && Playing) Pause();
    }

    // Navegador e PC: pausa ao trocar de aba ou de janela.
    // No editor fica desligado, para não pausar ao clicar no Inspector.
    void OnApplicationFocus(bool focused)
    {
#if !UNITY_EDITOR
        if (!focused && Playing) Pause();
#endif
    }

    public void SetScore(int score, bool pop = true)
    {
        string s = Format(score);
        if (scoreText.text == s) return;
        scoreText.text = s;
        if (pop) Juice.Pop(scoreText.transform, 0.2f, 0.15f);
    }

    // anchorY: altura na tela (0 = base, 1 = topo). Padrão = acima do meio.
    public void ShowMessage(string text, float duration = 0.8f, float anchorY = 0.62f)
    {
        msgText.rectTransform.anchorMin = msgText.rectTransform.anchorMax = new Vector2(0.5f, anchorY);
        msgText.text = text;
        msgTimer = duration;
        Juice.Pop(msgText.transform, 0.3f, 0.25f);
    }

    public void GameOver(int score)
    {
        if (!Playing) return;
        Current = State.Over;
        Music.SetDucked(true);
        newBest = score > Best;
        if (newBest)
        {
            Best = score;
            PlayerPrefs.SetInt("best", Best);
            PlayerPrefs.Save();
        }
        rankPosition = Ranking.Submit(score, out lastAdded);
        StartCoroutine(ShowGameOver(score));
    }

    IEnumerator ShowGameOver(int score)
    {
        // Pequena espera para o jogador ver o que aconteceu
        yield return new WaitForSecondsRealtime(0.9f);
        overScore.text = Format(score);
        SetRecordText(overBest);
        // Recorde novo tem prioridade; senão, mostra a posição se entrou no top 10
        newRecord.text = newBest ? L("NOVO RECORDE!", "NEW BEST!")
                       : L($"{rankPosition}º NO RANKING", $"#{rankPosition} ON THE BOARD");
        newRecord.gameObject.SetActive(newBest || rankPosition > 0);
        Show(over);
        Juice.Pop(overScore.transform, 0.3f, 0.3f);
        if (newBest) { Sfx.Play("record"); OnNewRecord?.Invoke(); }
    }
}
