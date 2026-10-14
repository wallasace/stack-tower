using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Gera sprites e UI por código, sem precisar de assets externos.
public static class Util
{
    public static Sprite Square()
    {
        var t = new Texture2D(4, 4) { filterMode = FilterMode.Point };
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    public static Sprite Circle(int size = 128)
    {
        var t = new Texture2D(size, size) { filterMode = FilterMode.Bilinear };
        float r = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    public static SpriteRenderer MakeSprite(string name, Sprite s, Color c, Vector3 pos, Vector2 scale, int order = 0)
    {
        var g = new GameObject(name);
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.color = c; sr.sortingOrder = order;
        g.transform.position = pos;
        g.transform.localScale = new Vector3(scale.x, scale.y, 1);
        return sr;
    }

    public static Camera SetupCamera(float size)
    {
        var cam = Camera.main;
        if (!cam)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }
        if (!cam.GetComponent<AudioListener>() && !Object.FindAnyObjectByType<AudioListener>())
            cam.gameObject.AddComponent<AudioListener>(); // sem isso nenhum som é ouvido
        cam.orthographic = true;
        cam.orthographicSize = size;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0, 0, -10);
        return cam;
    }

    public static Transform MakeCanvas()
    {
        var go = new GameObject("Canvas");
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<GraphicRaycaster>(); // necessário para os botões receberem cliques
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080, 1920);
        // Expand: a área 1080x1920 sempre cabe inteira. Em telas mais estreitas sobra altura,
        // em telas mais largas (tablet, navegador deitado) sobra largura. Nada é cortado.
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        return go.transform;
    }

    // Filho do canvas que fica dentro da área segura (fora do notch e da barra de gestos).
    // Use para HUD e menus que ficam colados nas bordas.
    public static Transform MakeSafeArea(Transform canvas)
    {
        var go = new GameObject("SafeArea", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        go.AddComponent<SafeArea>();
        return go.transform;
    }

    public enum Weight { Medium, SemiBold, Bold }

    static readonly Font[] fonts = new Font[3];

    // Fonte Barlow (Assets/Resources/Fonts). Se faltar o arquivo, cai na fonte padrão do Unity.
    public static Font FontFor(Weight w)
    {
        int i = (int)w;
        if (!fonts[i])
            fonts[i] = Resources.Load<Font>("Fonts/Barlow-" + w)
                       ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return fonts[i];
    }

    public static void SetType(Text t, Weight weight, float spacing)
    {
        t.font = FontFor(weight);
        t.fontStyle = FontStyle.Normal;
        t.GetComponent<LetterSpacing>().Spacing = spacing;
    }

    public static Text MakeText(Transform parent, float anchorY, float offsetY, int size,
        Weight weight = Weight.Bold, float spacing = 0f)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        go.AddComponent<LetterSpacing>().Spacing = spacing; // antes do Shadow
        t.font = FontFor(weight);
        t.fontSize = size;
        t.fontStyle = FontStyle.Normal;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false; // texto não deve "engolir" toques do jogo
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0.05f, 0.35f);
        sh.effectDistance = new Vector2(0, -4);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY);
        rt.anchoredPosition = new Vector2(0, offsetY);
        rt.sizeDelta = new Vector2(1000, 400);
        return t;
    }

    static Sprite rounded;

    // Retângulo com cantos arredondados, fatiado (9-slice) para botões de qualquer tamanho.
    public static Sprite Rounded()
    {
        if (rounded) return rounded;
        const int size = 64, r = 24;
        var t = new Texture2D(size, size) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
            }
        t.Apply();
        rounded = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return rounded;
    }

    // Painel que cobre a tela toda. Só bloqueia toques se tiver cor visível.
    public static RectTransform MakePanel(Transform parent, Color color)
    {
        var go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = color.a > 0f;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static Text MakeButton(Transform parent, string label, Vector2 anchor, Vector2 offset, Vector2 size,
        Color color, UnityAction onClick, int fontSize = 64)
    {
        var go = new GameObject("Button");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = Rounded();
        img.type = Image.Type.Sliced;
        img.color = color;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var text = MakeText(go.transform, 0.5f, 0, fontSize);
        text.text = label;
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        return text;
    }

    static Sprite roundedOutline, block, hGradient;

    // Contorno de retângulo arredondado (borda de botões secundários e painéis).
    public static Sprite RoundedOutline()
    {
        if (roundedOutline) return roundedOutline;
        const int size = 64, r = 24;
        const float thickness = 3f;
        var t = new Texture2D(size, size) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                float d = r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(d) * Mathf.Clamp01(thickness - d + 0.5f);
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        t.Apply();
        roundedOutline = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return roundedOutline;
    }

    public static Image AddOutline(Transform target, Color color, float pixelsPerUnitMultiplier = 1f)
    {
        var go = new GameObject("Outline");
        go.transform.SetParent(target, false);
        var img = go.AddComponent<Image>();
        img.sprite = RoundedOutline();
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        img.color = color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return img;
    }

    // Bloco 2D com relevo: brilho no topo, gradiente vertical e borda inferior mais escura.
    // A textura é em tons de cinza; a cor do SpriteRenderer pinta o bloco. Use drawMode Sliced.
    public static Sprite Block()
    {
        if (block) return block;
        const int w = 64, h = 32, r = 5;
        var t = new Texture2D(w, h) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float lum;
                if (y >= h - 3) lum = 1f;                                        // faixa clara no topo
                else if (y == h - 4) lum = 0.95f;
                else if (y <= 2) lum = Mathf.Lerp(0.55f, 0.66f, y / 2f);         // borda inferior em sombra
                else lum = Mathf.Lerp(0.74f, 0.9f, (y - 3f) / (h - 7f));         // corpo em degradê
                // laterais um pouco mais escuras: sugere volume sem perspectiva
                float edge = Mathf.Min(x, w - 1 - x);
                if (edge < 3) lum *= Mathf.Lerp(0.82f, 1f, edge / 3f);

                float cx = Mathf.Clamp(x + 0.5f, r, w - r);
                float cy = Mathf.Clamp(y + 0.5f, r, h - r);
                float a = Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)));
                t.SetPixel(x, y, new Color(lum, lum, lum, a));
            }
        t.Apply();
        block = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64, 0,
            SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
        return block;
    }

    static Sprite softRect, vFade;

    // Retângulo de bordas desfocadas (bloom/brilho atrás de blocos e botões). Use Sliced.
    public static Sprite SoftRect()
    {
        if (softRect) return softRect;
        const int size = 96, blur = 36;
        var t = new Texture2D(size, size) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(blur - (x + 0.5f), (x + 0.5f) - (size - blur)));
                float dy = Mathf.Max(0f, Mathf.Max(blur - (y + 0.5f), (y + 0.5f) - (size - blur)));
                float k = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / blur);
                t.SetPixel(x, y, new Color(1, 1, 1, k * k * (3f - 2f * k)));
            }
        t.Apply();
        softRect = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(blur, blur, blur, blur));
        return softRect;
    }

    static Sprite backlight;
    static float backlightBlur;

    // Mesmo desfoque do SoftRect, com borda de "blur" unidades: espalha além da forma,
    // como filter: blur() no CSS. Use Sliced. Recria o sprite só quando o blur muda.
    public static Sprite Backlight(float blur)
    {
        if (backlight && Mathf.Approximately(backlightBlur, blur)) return backlight;
        var soft = SoftRect();
        backlightBlur = blur;
        backlight = Sprite.Create(soft.texture, soft.rect, new Vector2(0.5f, 0.5f), soft.border.x / blur, 0,
            SpriteMeshType.FullRect, soft.border);
        return backlight;
    }

    // Largura de borda (em px) do SoftRect numa Image com pixelsPerUnitMultiplier = 1.
    public static float SoftRectBorderPx => SoftRect().border.x;

    static Sprite bloomRect;

    // Brilho curto e contido em volta de um retângulo (blocos). Some rápido a partir da borda.
    // Borda de 0,2 unidade: o brilho passa só um pouco da borda do bloco. Use Sliced.
    public static Sprite BloomRect()
    {
        if (bloomRect) return bloomRect;
        const int size = 64, blur = 24;
        var t = new Texture2D(size, size) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(blur - (x + 0.5f), (x + 0.5f) - (size - blur)));
                float dy = Mathf.Max(0f, Mathf.Max(blur - (y + 0.5f), (y + 0.5f) - (size - blur)));
                float k = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / blur);
                t.SetPixel(x, y, new Color(1, 1, 1, k * k * k)); // queda rápida = brilho, não borrão
            }
        t.Apply();
        bloomRect = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 120, 0,
            SpriteMeshType.FullRect, new Vector4(blur, blur, blur, blur));
        return bloomRect;
    }

    // Degradê vertical opaco em cima -> transparente embaixo (reflexo no chão). 1x1 unidade.
    public static Sprite VFade()
    {
        if (vFade) return vFade;
        const int w = 8, h = 64;
        var t = new Texture2D(w, h) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
        {
            float k = (y + 0.5f) / h;
            for (int x = 0; x < w; x++) t.SetPixel(x, y, new Color(1, 1, 1, k * k));
        }
        t.Apply();
        vFade = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
        return vFade;
    }

    // Degradê horizontal transparente -> opaco (rastro de movimento). 1x1 unidade.
    public static Sprite HGradient()
    {
        if (hGradient) return hGradient;
        const int w = 32, h = 8;
        var t = new Texture2D(w, h) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float k = (x + 0.5f) / w;
                t.SetPixel(x, y, new Color(1, 1, 1, k * k));
            }
        t.Apply();
        hGradient = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        return hGradient;
    }

    // Fundo: degradê radial vindo de baixo (como "ellipse at bottom" no CSS) nas cores do jogo,
    // com céu de estrelas em parallax por cima (StarField). Fica atrás de tudo.
    public static void MakeBackground(Camera cam, Color top, Color bottom)
    {
        cam.backgroundColor = bottom;
        const int w = 270, h = 480;
        var px = new Color32[w * h];
        // Elipse centrada na base da tela, com raios até o canto mais distante
        float rx = w / 2f * 1.4142f, ry = h * 1.4142f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w / 2f) / rx, dy = (y + 0.5f) / ry;
                float k = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                var c = Color.Lerp(top, bottom, k);
                c.a = 1f;
                px[y * w + x] = c;
            }
        var tex = new Texture2D(w, h) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();

        var go = new GameObject("Background");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 50f;
        canvas.sortingOrder = -100;
        // Mesma escala da UI, para as estrelas terem o mesmo tamanho em qualquer tela
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var imgGo = new GameObject("Gradient", typeof(RectTransform));
        imgGo.transform.SetParent(go.transform, false);
        var img = imgGo.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        StarField.Create(go.transform);
    }

    static Sprite glow;

    // Brilho radial suave (some nas bordas). 1x1 unidade.
    public static Sprite Glow()
    {
        if (glow) return glow;
        const int size = 64;
        var t = new Texture2D(size, size) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float r = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
        t.Apply();
        glow = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return glow;
    }

    static Sprite speakerOn, speakerOff;

    // Ícone de alto-falante desenhado por código: com ondas (som ligado) ou com X (mudo).
    public static Sprite SpeakerIcon(bool on)
    {
        if (on && speakerOn) return speakerOn;
        if (!on && speakerOff) return speakerOff;
        const int size = 64, ss = 4; // 4x4 amostras por pixel = bordas suaves
        var t = new Texture2D(size, size) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                        if (SpeakerShape(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss, on)) hits++;
                t.SetPixel(x, y, new Color(1, 1, 1, hits / (float)(ss * ss)));
            }
        t.Apply();
        var s = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        if (on) speakerOn = s; else speakerOff = s;
        return s;
    }

    static bool SpeakerShape(float x, float y, bool on)
    {
        const float cy = 32f;
        if (x >= 8f && x <= 20f && Mathf.Abs(y - cy) <= 8f) return true;          // corpo
        if (x >= 20f && x <= 34f)                                                 // cone
        {
            float hh = Mathf.Lerp(8f, 20f, (x - 20f) / 14f);
            if (Mathf.Abs(y - cy) <= hh) return true;
        }
        if (on)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(34f, cy));
            float ang = Mathf.Abs(Mathf.Atan2(y - cy, x - 34f)) * Mathf.Rad2Deg;
            if (ang < 50f && (Mathf.Abs(d - 11f) < 2f || Mathf.Abs(d - 20f) < 2f)) return true; // ondas
        }
        else
        {
            float dx = x - 48f, dy = y - cy;
            if (Mathf.Abs(dx) <= 9f && Mathf.Abs(dy) <= 9f &&
                (Mathf.Abs(dx - dy) < 2.6f || Mathf.Abs(dx + dy) < 2.6f)) return true;  // X
        }
        return false;
    }

    static Sprite xIcon, gitHubIcon;

    // Logos oficiais (Simple Icons, CC0) em Assets/Resources/Icons.
    // Se o arquivo faltar, usa a versão simplificada desenhada por código.
    public static Sprite XIcon() => xIcon ? xIcon : (xIcon = LoadIcon("Icons/x") ?? DrawIcon(XShape));
    public static Sprite GitHubIcon() => gitHubIcon ? gitHubIcon : (gitHubIcon = LoadIcon("Icons/github") ?? DrawIcon(GitHubShape));

    static Sprite LoadIcon(string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (!tex) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
    }

    static Sprite DrawIcon(System.Func<float, float, bool> shape)
    {
        const int size = 64, ss = 4;
        var t = new Texture2D(size, size) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                        if (shape(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss)) hits++;
                t.SetPixel(x, y, new Color(1, 1, 1, hits / (float)(ss * ss)));
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * k);
    }

    // Logo do X: traço grosso de cima-esquerda para baixo-direita, traço fino no sentido oposto
    static bool XShape(float x, float y)
    {
        var p = new Vector2(x, y);
        return SegmentDistance(p, new Vector2(15, 52), new Vector2(49, 12)) < 5f
            || SegmentDistance(p, new Vector2(15, 12), new Vector2(49, 52)) < 2f;
    }

    // Marca do GitHub simplificada: disco com a silhueta do gato vazada
    static bool GitHubShape(float x, float y)
    {
        var p = new Vector2(x, y);
        if (Vector2.Distance(p, new Vector2(32, 32)) > 30f) return false;
        bool head = ((x - 32f) * (x - 32f)) / (15f * 15f) + ((y - 37f) * (y - 37f)) / (12.5f * 12.5f) <= 1f;
        bool ears = InTriangle(p, new Vector2(18, 40), new Vector2(20, 54), new Vector2(29, 47))
                 || InTriangle(p, new Vector2(46, 40), new Vector2(44, 54), new Vector2(35, 47));
        bool body = x >= 26.5f && x <= 37.5f && y <= 27f;
        bool tail = SegmentDistance(p, new Vector2(26.5f, 17f), new Vector2(17f, 22f)) < 2.2f;
        return !(head || ears || body || tail);
    }

    static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        // Sem navegação: assim Espaço não "clica" no último botão selecionado
        go.AddComponent<EventSystem>().sendNavigationEvents = false;
        go.AddComponent<StandaloneInputModule>();
    }

    static bool OverUI()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
                if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
            return false;
        }
        return es.IsPointerOverGameObject();
    }

    // Toque/clique na área de jogo (ignora toques em botões).
    public static bool PointerDown() => Input.GetMouseButtonDown(0) && !OverUI();

    public static bool Tapped() => PointerDown() || Input.GetKeyDown(KeyCode.Space);
}
