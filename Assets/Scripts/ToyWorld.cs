using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Cenário "Toybox World", montado como na referência:
// - céu em degradê (azul forte no alto -> ciano claro no horizonte) e sol no alto à esquerda;
// - nuvens fofas com volume nas laterais, centro livre;
// - brinquedos de madeira (castelos, árvores, cubos, cata-vento, telhados) à esquerda, à direita e ao fundo;
// - luz do dia vinda do alto à esquerda, com sombras de contato visíveis;
// - fundo levemente desfocado: o cenário distante é renderizado por uma 2ª câmera em baixa resolução
//   e ampliado com filtro bilinear (desfoque quase de graça no celular); a torre fica nítida.
// A composição é presa à tela (viewport): sol, nuvens e brinquedos se reposicionam conforme o formato,
// então ficam enquadrados igual em celular em pé, tablet e navegador deitado.
public class ToyWorld : MonoBehaviour
{
    // Paleta do projeto (color_palette)
    public static readonly Color SkyBlue = Hex("#48A5F5");
    public static readonly Color Cream = Hex("#FFF2D8");
    public static readonly Color Wood = Hex("#D9AC72");
    public static readonly Color Coral = Hex("#F05B49");
    public static readonly Color Yellow = Hex("#FFAD35");
    public static readonly Color Mint = Hex("#27B7AE");
    public static readonly Color Grass = Hex("#A8D84B");
    public static readonly Color Royal = Hex("#347BE8");
    public static readonly Color Purple = Hex("#7950C8");
    public static readonly Color Pink = Hex("#E64DA8");
    public static readonly Color Brown = Hex("#70452A");
    static readonly Color TreeGreen = Hex("#4CAF50");
    static readonly Color FloorWood = Hex("#B98352"); // vira ~#D9AC72 sob a luz do sol

    public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    // Camada só do fundo desfocado (índice livre do Unity, sem precisar de nome)
    public const int BackgroundLayer = 30;

    static Material wood;

    // Um único material para todos os objetos de madeira; cor e tamanho vão por MaterialPropertyBlock
    public static Material WoodMaterial
    {
        get
        {
            if (wood) return wood;
            var shader = Shader.Find("Toybox/PaintedWood");
            wood = new Material(shader ? shader : Shader.Find("Standard")) { name = "ToyPaintedWood", enableInstancing = true };
            wood.SetColor("_WoodColor", Wood);
            wood.SetFloat("_Glossiness", 0.36f); // tinta acetinada; as quinas são mais lisas (shader)
            wood.SetFloat("_GrainStrength", 0.16f); // veio visível por baixo da tinta, como na referência
            wood.SetFloat("_GrainScale", 6f);
            return wood;
        }
    }

    // Criado sob demanda: o Unity não deixa criar objetos nativos durante a construção do componente
    static MaterialPropertyBlock mpbCache;
    static MaterialPropertyBlock mpb => mpbCache ??= new MaterialPropertyBlock();

    // Aplica cor, tamanho (para o desgaste nas arestas) e semente do veio a um objeto
    public static void Paint(Renderer r, Color color, Vector3 halfSize, float seed)
    {
        r.GetPropertyBlock(mpb);
        mpb.SetColor("_Color", color);
        mpb.SetVector("_HalfSize", halfSize);
        mpb.SetFloat("_Seed", seed);
        r.SetPropertyBlock(mpb);
    }

    public static MeshRenderer Part(Transform parent, string name, Mesh mesh, Color color, Vector3 pos,
        Vector3 euler = default, Vector3? halfSize = null, bool shadows = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.layer = parent ? parent.gameObject.layer : 0;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = WoodMaterial;
        mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;
        // Peças que não são caixas não têm arestas retas: tamanho enorme = sem desgaste
        Paint(mr, color, halfSize ?? Vector3.one * 100f, Random.value * 10f);
        return mr;
    }

    // Caixa arredondada já posicionada com a base em "basePos"
    public static MeshRenderer Box(Transform parent, string name, Vector3 size, Color color, Vector3 basePos,
        float yaw = 0f, float bevel = 0.12f, bool shadows = true) =>
        Part(parent, name, ToyMeshes.RoundedBox(size, bevel), color, basePos + Vector3.up * size.y / 2f,
            new Vector3(0, yaw, 0), size / 2f, shadows);

    // Grupo de cenário preso a um ponto da tela: x acompanha a largura visível naquela profundidade
    class Anchor { public Transform t; public float side, edge, z, scale; }

    // Sprite preso à tela (sol, nuvens): posição e tamanho em frações do enquadramento
    class ScreenSprite { public Transform t; public Vector2 viewport; public float dist, height, sway, phase; }

    Camera cam;
    float floorY;
    readonly List<Anchor> anchors = new List<Anchor>();
    readonly List<ScreenSprite> screenSprites = new List<ScreenSprite>();
    Transform windmillBlades;

    public static ToyWorld Build(Camera cam, float floorY)
    {
        var w = new GameObject("ToyWorld").AddComponent<ToyWorld>();
        w.cam = cam;
        w.floorY = floorY;
        w.SetupLighting();
        w.SetupCameras();
        w.BuildGround();
        w.BuildDecorations();
        w.BuildSky();
        w.LateUpdate();
        return w;
    }

    // ---------- Luz (setup de cinema: luz principal + preenchimento + ambiente) ----------

    void SetupLighting()
    {
        // Qualidade máxima de sombra: 4 cascatas, resolução muito alta, bordas suaves
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowDistance = 70f;
        QualitySettings.antiAliasing = 8;        // MSAA 8x: sem serrilhado nas arestas
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.pixelLightCount = 4;

        // Luz principal: o sol está atrás, no alto à esquerda (como na referência),
        // então a luz vem de trás e as sombras caem para a frente e para a direita
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.62f, -0.52f, -0.58f).normalized);
        sun.color = new Color(1f, 0.95f, 0.87f);     // sol quente de fim de manhã
        sun.intensity = 1.15f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.88f;
        sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
        sun.shadowBias = 0.02f;
        sun.shadowNormalBias = 0.12f;

        // Preenchimento pela frente (céu rebatendo): sem sombra, frio e fraco, só para abrir as faces da frente
        var fill = new GameObject("Fill").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.transform.rotation = Quaternion.LookRotation(new Vector3(-0.3f, -0.35f, 0.89f).normalized);
        fill.color = new Color(0.78f, 0.87f, 1f);
        fill.intensity = 0.35f;
        fill.shadows = LightShadows.None;
        fill.renderMode = LightRenderMode.ForcePixel;
        sun.renderMode = LightRenderMode.ForcePixel;

        // Ambiente: céu azul em cima, horizonte quente, rebatimento de madeira embaixo
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.33f, 0.46f, 0.7f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.44f, 0.41f);
        RenderSettings.ambientGroundColor = new Color(0.26f, 0.19f, 0.12f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.sun = sun;
        RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = null;
        RenderSettings.reflectionIntensity = 0f;

        // Perspectiva aérea: névoa azulada bem leve só no fundo distante
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.62f, 0.8f, 0.98f);
    }

    // ---------- Câmera: céu + profundidade de campo ----------

    PostFX postFX;

    void SetupCameras()
    {
        // Uma câmera só: HDR + MSAA; o desfoque do fundo é profundidade de campo pelo depth buffer (PostFX)
        var skyShader = Shader.Find("Toybox/Sky");
        if (skyShader)
        {
            var sky = new Material(skyShader) { name = "ToySky" };
            sky.SetColor("_ZenithColor", Hex("#1A62D6"));
            sky.SetColor("_SkyColor", Hex("#3C96EE"));
            sky.SetColor("_HorizonColor", Hex("#A9DCFF"));
            sky.SetFloat("_SunGlowStrength", 0f); // o sol é desenhado à parte, sempre na tela
            var skybox = cam.GetComponent<Skybox>();
            if (!skybox) skybox = cam.gameObject.AddComponent<Skybox>();
            skybox.material = sky;
            cam.clearFlags = CameraClearFlags.Skybox;
        }
        else
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyBlue;
        }
        cam.allowHDR = true;
        cam.allowMSAA = true;
        cam.depthTextureMode |= DepthTextureMode.Depth;
        postFX = cam.GetComponent<PostFX>();
        if (!postFX) postFX = cam.gameObject.AddComponent<PostFX>();
    }

    // ---------- Chão ----------

    // Transição entre o chão nítido (perto da torre) e o desfocado (fundo)
    const float FocusSplitZ = 3.5f;

    void BuildGround()
    {
        var groundMat = new Material(WoodMaterial) { name = "ToyGround" };
        groundMat.SetFloat("_GrainScale", 0.9f);
        groundMat.SetFloat("_GrainStrength", 0.09f);
        groundMat.SetFloat("_PlankWidth", 2.6f);
        groundMat.SetFloat("_Glossiness", 0.3f);
        groundMat.SetFloat("_EdgeGlow", 0f);
        groundMat.SetFloat("_BaseAO", 0f);
        groundMat.SetFloat("_WearAmount", 0f);

        // Perto: nítido (camada padrão)
        float nearDepth = FocusSplitZ + 60f;
        var near = Part(transform, "GroundNear", ToyMeshes.RoundedBox(new Vector3(160f, 1f, nearDepth), 0.001f, 1), FloorWood,
            new Vector3(0, floorY - 0.5f, FocusSplitZ - nearDepth / 2f), default, Vector3.one * 1000f, false);
        near.sharedMaterial = groundMat;

        // Longe: desfocado (camada do fundo)
        var bgRoot = BackgroundRoot("Background");
        var far = Part(bgRoot, "GroundFar", ToyMeshes.RoundedBox(new Vector3(260f, 1f, 160f), 0.001f, 1), FloorWood,
            new Vector3(0, floorY - 0.5f, FocusSplitZ + 80f), default, Vector3.one * 1000f, false);
        far.sharedMaterial = groundMat;

        // Sombra de contato macia sob a torre (oclusão ambiente falsa)
        var blob = Util.MakeSprite("ContactShadow", Util.Glow(), new Color(0.25f, 0.14f, 0.06f, 0.5f),
            new Vector3(0.9f, floorY + 0.01f, -0.5f), new Vector2(8.5f, 2.6f)); // sombra vai para a frente/direita
        blob.transform.rotation = Quaternion.Euler(90f, 0, 0);
    }

    Transform BackgroundRoot(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        t.gameObject.layer = BackgroundLayer;
        return t;
    }

    // ---------- Cenário ----------

    // Cria um grupo preso à lateral da tela: side -1 = esquerda, +1 = direita;
    // edge = fração da meia largura visível (0 = centro, 1 = borda); z = profundidade
    Transform AnchorGroup(string name, float side, float edge, float z, bool background, float scale = 1f)
    {
        var t = background ? BackgroundRoot(name) : new GameObject(name).transform;
        if (!background) t.SetParent(transform, false);
        t.localScale = Vector3.one * scale;
        anchors.Add(new Anchor { t = t, side = side, edge = edge, z = z, scale = scale });
        return t;
    }

    void BuildDecorations()
    {
        var f = new Vector3(0, floorY, 0);

        // --- Fundo à esquerda (desfocado): castelo de madeira com telhado coral e bandeira, árvores, cubos
        var leftBack = AnchorGroup("LeftBack", -1f, 0.86f, 14f, true, 1.15f);
        BuildCastle(leftBack, f + new Vector3(-1.5f, 0, 2f), true);
        BuildTree(leftBack, f + new Vector3(2.6f, 0, -1.5f), 1.25f);
        BuildTree(leftBack, f + new Vector3(-4.5f, 0, 4f), 1.5f);
        Box(leftBack, "CubeBlue", new Vector3(1.7f, 1.7f, 1.7f), Royal, f + new Vector3(4.2f, 0, -3.5f), 18f);
        Box(leftBack, "Roof", new Vector3(1.2f, 1.2f, 1.2f), Yellow, f + new Vector3(1.2f, 0, -4.5f), -10f);
        Part(leftBack, "Prism", ToyMeshes.Prism(1.6f, 1.2f, 1f), Coral, f + new Vector3(3.6f, 0, 1.5f), new Vector3(0, 20f, 0));

        // --- Fundo à direita (desfocado): castelo com telhado vermelho, cata-vento, árvores, arco amarelo
        var rightBack = AnchorGroup("RightBack", 1f, 0.86f, 14f, true, 1.15f);
        BuildCastle(rightBack, f + new Vector3(1.8f, 0, 2.5f), false);
        BuildWindmill(rightBack, f + new Vector3(-2.4f, 0, 6f));
        BuildTree(rightBack, f + new Vector3(-1.2f, 0, -1f), 1.2f);
        BuildTree(rightBack, f + new Vector3(4.8f, 0, 5f), 1.6f);
        Box(rightBack, "CubeBlue", new Vector3(1.8f, 1.8f, 1.8f), Royal, f + new Vector3(-0.6f, 0, -4f), -14f);

        // --- Bem ao fundo, centro (detalhe moderado, desfocado e com névoa)
        var farBack = AnchorGroup("FarBack", 0f, 0f, 28f, true);
        Box(farBack, "FarHouseL", new Vector3(2.6f, 2f, 2f), Wood, f + new Vector3(-9f, 0, 0), 8f, 0.12f, false);
        Part(farBack, "FarRoofL", ToyMeshes.Pyramid(2.9f, 1.4f), Yellow, f + new Vector3(-9f, 2f, 0), new Vector3(0, 8f, 0), null, false);
        Box(farBack, "FarHouseR", new Vector3(3f, 2.2f, 2.2f), Wood, f + new Vector3(10f, 0, 2f), -10f, 0.12f, false);
        Part(farBack, "FarRoofR", ToyMeshes.Prism(3.3f, 1.4f, 2.3f), Royal, f + new Vector3(10f, 2.2f, 2f), new Vector3(0, -10f, 0), null, false);
        BuildTree(farBack, f + new Vector3(-4f, 0, 3f), 1.4f);
        BuildTree(farBack, f + new Vector3(5f, 0, 4f), 1.3f);
        Box(farBack, "FarCubeR", new Vector3(1.6f, 1.6f, 1.6f), Coral, f + new Vector3(1.5f, 0, 6f), -20f, 0.12f, false);

        // --- Frente, cantos de baixo (nítidos): cubos coloridos cortados pela borda, sem cobrir a jogada
        var leftFront = AnchorGroup("LeftFront", -1f, 1f, 1.5f, false, 0.78f);
        Box(leftFront, "CubeRed", new Vector3(1.9f, 1.9f, 1.9f), Coral, f + new Vector3(0.6f, 0, 0), 12f);
        Box(leftFront, "CubeBlue", new Vector3(2.2f, 1.6f, 1.8f), Royal, f + new Vector3(-1.8f, 0, -2.2f), -8f);
        Box(leftFront, "CubeBlueBack", new Vector3(1.6f, 1.5f, 1.6f), Royal, f + new Vector3(2.4f, 0, 3.2f), 6f);

        var rightFront = AnchorGroup("RightFront", 1f, 0.98f, 1.5f, false, 0.78f);
        BuildArch(rightFront, f + new Vector3(0.2f, 0, 0.5f));
        Part(rightFront, "PrismRed", ToyMeshes.Prism(2.3f, 1.4f, 1.3f), Coral, f + new Vector3(0.6f, 0, -1.6f), new Vector3(0, -14f, 0));
        Box(rightFront, "CubeBlue", new Vector3(1.6f, 1.6f, 1.6f), Royal, f + new Vector3(-2.2f, 0, 3.5f), -18f);
    }

    void BuildCastle(Transform root, Vector3 b, bool tall)
    {
        var c = new GameObject(tall ? "CastleLeft" : "CastleRight").transform;
        c.SetParent(root, false);
        c.gameObject.layer = root.gameObject.layer;
        c.localPosition = b;
        if (tall)
        {
            Box(c, "Base", new Vector3(4.2f, 2.4f, 3f), Wood, new Vector3(0.6f, 0, 0));
            Box(c, "Door", new Vector3(1f, 1.4f, 0.2f), Hex("#B9824C"), new Vector3(0.9f, 0, -1.55f), 0, 0.08f);
            Box(c, "Tower", new Vector3(1.9f, 3.2f, 1.9f), Wood, new Vector3(-0.8f, 2.4f, 0));
            Box(c, "Window", new Vector3(0.7f, 0.9f, 0.2f), Royal, new Vector3(-0.8f, 3.6f, -1f), 0, 0.08f);
            Part(c, "Roof", ToyMeshes.Cylinder(1.4f, 0f, 2f, 10), Coral, new Vector3(-0.8f, 5.6f, 0));
            Part(c, "FlagPole", ToyMeshes.Cylinder(0.06f, 0.06f, 1.3f, 6), Brown, new Vector3(-0.8f, 7.4f, 0));
            Part(c, "Flag", ToyMeshes.Prism(0.7f, 0.9f, 0.06f), Coral, new Vector3(-0.5f, 8.35f, 0), new Vector3(0, 0, -90f));
            Box(c, "Block", new Vector3(1.4f, 1.4f, 1.4f), Yellow, new Vector3(2.4f, 2.4f, 0.2f), 10f);
        }
        else
        {
            Box(c, "Base", new Vector3(3.6f, 2.2f, 2.8f), Wood, Vector3.zero);
            Box(c, "Tower", new Vector3(1.7f, 2.4f, 1.7f), Wood, new Vector3(-0.4f, 2.2f, 0));
            Part(c, "Roof", ToyMeshes.Pyramid(2.3f, 1.7f), Coral, new Vector3(-0.4f, 4.6f, 0));
            Box(c, "Window", new Vector3(0.7f, 1f, 0.2f), Royal, new Vector3(0.9f, 0.5f, -1.45f), 0, 0.08f);
        }
    }

    // Arco amarelo (dois pés + viga), como o da frente à direita na referência
    void BuildArch(Transform root, Vector3 b)
    {
        Box(root, "ArchLeft", new Vector3(0.9f, 1.2f, 1.4f), Yellow, b + new Vector3(-1.4f, 0, 0));
        Box(root, "ArchRight", new Vector3(0.9f, 1.2f, 1.4f), Yellow, b + new Vector3(1.4f, 0, 0));
        Box(root, "ArchTop", new Vector3(3.8f, 0.8f, 1.4f), Yellow, b + new Vector3(0, 1.2f, 0));
    }

    void BuildTree(Transform root, Vector3 b, float s)
    {
        var t = new GameObject("Tree").transform;
        t.SetParent(root, false);
        t.gameObject.layer = root.gameObject.layer;
        t.localPosition = b;
        t.localScale = Vector3.one * s;
        Part(t, "Trunk", ToyMeshes.Cylinder(0.2f, 0.16f, 0.85f, 8), Hex("#C48A52"), Vector3.zero);
        Part(t, "LeavesLow", ToyMeshes.Cylinder(1f, 0f, 1.5f, 10), TreeGreen, new Vector3(0, 0.75f, 0));
        Part(t, "LeavesHigh", ToyMeshes.Cylinder(0.75f, 0f, 1.25f, 10), Grass, new Vector3(0, 1.6f, 0));
    }

    void BuildWindmill(Transform root, Vector3 b)
    {
        var m = new GameObject("Windmill").transform;
        m.SetParent(root, false);
        m.gameObject.layer = root.gameObject.layer;
        m.localPosition = b;
        Box(m, "Post", new Vector3(0.6f, 3.6f, 0.6f), Wood, Vector3.zero, 0, 0.1f);
        windmillBlades = new GameObject("Blades").transform;
        windmillBlades.SetParent(m, false);
        windmillBlades.gameObject.layer = m.gameObject.layer;
        windmillBlades.localPosition = new Vector3(0, 3.6f, -0.45f);
        for (int i = 0; i < 4; i++)
        {
            var blade = Box(windmillBlades, "Blade", new Vector3(0.6f, 1.9f, 0.12f), Wood, Vector3.zero, 0, 0.06f);
            blade.transform.localRotation = Quaternion.Euler(0, 0, i * 90f);
            blade.transform.localPosition = blade.transform.localRotation * new Vector3(0, 1.05f, 0);
        }
        Part(windmillBlades, "Hub", ToyMeshes.Cylinder(0.22f, 0.22f, 0.25f, 10), Brown, new Vector3(0, 0, -0.1f),
            new Vector3(90f, 0, 0));
    }

    // ---------- Céu: sol e nuvens presos à tela ----------

    void BuildSky()
    {
        var root = BackgroundRoot("Sky");

        // Sol no alto à esquerda: halo quente grande + núcleo claro
        var sunSprite = Util.MakeSprite("SunGlow", SunSprite(), Color.white, Vector3.zero, Vector2.one, -60);
        // Cor HDR (acima de 1) no material: o núcleo passa do limiar e o bloom/glare fazem o brilho do sol
        var sunMat = new Material(sunSprite.sharedMaterial) { name = "SunHDR" };
        sunMat.SetColor("_Color", new Color(2.2f, 2.05f, 1.8f, 1f));
        sunSprite.sharedMaterial = sunMat;
        AddScreenSprite(root, sunSprite, new Vector2(0.04f, 0.8f), 0.62f, 0f);

        // Nuvens grandes nas laterais, centro livre (alturas em fração da tela)
        var a = Sprite.Create(CloudTexture(768, 384, 11), new Rect(0, 0, 768, 384), new Vector2(0.5f, 0.3f), 150f);
        var b = Sprite.Create(CloudTexture(768, 384, 29), new Rect(0, 0, 768, 384), new Vector2(0.5f, 0.3f), 150f);
        var c = Sprite.Create(CloudTexture(768, 384, 47), new Rect(0, 0, 768, 384), new Vector2(0.5f, 0.3f), 150f);
        AddCloud(root, a, new Vector2(0.1f, 0.58f), 0.36f);
        AddCloud(root, c, new Vector2(0.27f, 0.56f), 0.26f);
        AddCloud(root, b, new Vector2(0.9f, 0.6f), 0.36f);
        AddCloud(root, a, new Vector2(0.8f, 0.8f), 0.16f);
        AddCloud(root, c, new Vector2(0.24f, 0.93f), 0.07f);
        AddCloud(root, b, new Vector2(0.72f, 0.57f), 0.2f);
    }

    void AddCloud(Transform root, Sprite s, Vector2 viewport, float height)
    {
        var sr = Util.MakeSprite("Cloud", s, Color.white, Vector3.zero, Vector2.one, -50);
        AddScreenSprite(root, sr, viewport, height, 0.012f);
    }

    void AddScreenSprite(Transform root, SpriteRenderer sr, Vector2 viewport, float height, float sway)
    {
        sr.transform.SetParent(root, true);
        sr.gameObject.layer = BackgroundLayer;
        screenSprites.Add(new ScreenSprite
        {
            t = sr.transform, viewport = viewport, dist = 110f, height = height, sway = sway,
            phase = Random.value * 10f,
        });
    }

    // Sol: núcleo quase branco e halo largo e macio
    static Sprite SunSprite()
    {
        const int size = 256;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float core = Mathf.Clamp01((0.17f - d) / 0.05f);
                float halo = Mathf.Pow(Mathf.Clamp01(1f - d), 3.2f) * 0.8f;
                float a = Mathf.Clamp01(core + halo * 0.85f);
                var c = Color.Lerp(new Color(1f, 0.9f, 0.62f), new Color(1f, 1f, 0.95f), core);
                px[y * size + x] = new Color(c.r, c.g, c.b, a);
            }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Nuvem fofa com volume: cada "puff" é uma meia esfera; a normal do relevo recebe luz do sol
    // (alto à esquerda): topo/esquerda branco com toque creme, base/direita cinza-azulado.
    static Texture2D CloudTexture(int w, int h, int seed)
    {
        var rng = new System.Random(seed);
        var puffs = new List<Vector3>();
        // Linha de base da nuvem (sem corte: a base também é feita de bolotas, irregular)
        float baseY = h * 0.3f;
        int n = 12;
        // Corpo: bolotas grandes formando o "morro" da nuvem, mais altas no meio
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            float r = (0.06f + 0.12f * Mathf.Sin(t * Mathf.PI) + (float)rng.NextDouble() * 0.03f) * w;
            float x = Mathf.Lerp(0.16f, 0.84f, t) * w + (float)(rng.NextDouble() - 0.5) * 0.04f * w;
            float y = baseY + r * (0.25f + (float)rng.NextDouble() * 0.3f);
            puffs.Add(new Vector3(x, y, r));
        }
        // Base orgânica: bolotas menores e achatadas por baixo, em alturas variadas
        for (int i = 0; i < 14; i++)
        {
            float t = i / 13f;
            float r = (0.045f + (float)rng.NextDouble() * 0.035f) * w * Mathf.Lerp(0.6f, 1f, Mathf.Sin(t * Mathf.PI));
            float x = Mathf.Lerp(0.2f, 0.8f, t) * w + (float)(rng.NextDouble() - 0.5) * 0.03f * w;
            float y = baseY - r * (0.05f + (float)rng.NextDouble() * 0.35f);
            if (y - r < 3f) y = r + 3f;
            puffs.Add(new Vector3(x, y, r));
        }
        // "Couve-flor": bolotinhas menores espalhadas no contorno de cima
        int extra = puffs.Count;
        for (int i = 0; i < 22; i++)
        {
            var parent = puffs[rng.Next(extra)];
            float ang = Mathf.Lerp(0.15f, Mathf.PI - 0.15f, (float)rng.NextDouble());
            float rr = parent.z * (0.35f + (float)rng.NextDouble() * 0.3f);
            float x = parent.x + Mathf.Cos(ang) * parent.z * 0.8f;
            float y = parent.y + Mathf.Sin(ang) * parent.z * 0.8f;
            if (y + rr > h - 2 || x - rr < 1 || x + rr > w - 2) continue;
            puffs.Add(new Vector3(x, y, rr));
        }

        // Relevo: união suave das meias-esferas (sem vincos entre bolotas)
        float softK = 7f * w / 512f;
        var height = new float[w * h];
        var mask = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float sum = 0f, best = -1f;
                foreach (var p in puffs)
                {
                    float dx = x - p.x, dy = y - p.y;
                    float d2 = p.z * p.z - dx * dx - dy * dy;
                    if (d2 <= 0f) continue;
                    float hv = Mathf.Sqrt(d2);
                    best = Mathf.Max(best, hv);
                    sum += Mathf.Exp(hv / softK);
                }
                height[y * w + x] = best > 0f ? softK * Mathf.Log(sum) : 0f;
                mask[y * w + x] = best > 0f ? Mathf.Clamp01(best / (6f * w / 512f)) : 0f; // borda macia
            }
        var smooth = BoxBlur(height, w, h, Mathf.RoundToInt(5f * w / 512f));

        var lightDir = new Vector3(-0.62f, 0.62f, 0.32f).normalized; // sol no alto à esquerda, um pouco atrás
        var lit = new Color(1f, 0.99f, 0.96f);        // branco com highlight creme
        var shadow = new Color(0.58f, 0.67f, 0.84f);  // sombra própria azulada (lado oposto ao sol)
        var px = new Color32[w * h];
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                float a = mask[y * w + x];
                if (a <= 0f) { px[y * w + x] = new Color32(255, 255, 255, 0); continue; }
                float hl = smooth[y * w + x - 1], hr = smooth[y * w + x + 1];
                float hd = smooth[(y - 1) * w + x], hu = smooth[(y + 1) * w + x];
                var n3 = new Vector3(hl - hr, hd - hu, 1.6f).normalized;
                float diffuse = Mathf.Clamp01(Vector3.Dot(n3, lightDir));
                float k = Mathf.SmoothStep(0f, 1f, 0.3f + diffuse * 0.8f);
                // Base mais sombreada (volume embaixo) e frestas entre bolotas levemente escuras
                k *= Mathf.Lerp(0.74f, 1f, Mathf.Clamp01((y - h * 0.18f) / (h * 0.4f)));
                float crease = Mathf.Clamp01((smooth[y * w + x] - height[y * w + x]) / 6f);
                k *= 1f - crease * 0.25f;
                var c = Color.Lerp(shadow, lit, k);
                // Borda prateada: contorno virado para o sol fica mais claro e quente (luz atravessando)
                float thin = 1f - Mathf.Clamp01(smooth[y * w + x] / (0.06f * w));
                c = Color.Lerp(c, new Color(1f, 0.97f, 0.88f), thin * diffuse * 0.9f);
                px[y * w + x] = new Color(c.r, c.g, c.b, a);
            }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // Desfoque de caixa separável (horizontal + vertical), usado para suavizar o relevo das nuvens
    static float[] BoxBlur(float[] src, int w, int h, int r)
    {
        var tmp = new float[src.Length];
        var dst = new float[src.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = 0f; int c = 0;
                for (int k = -r; k <= r; k++) { int xx = x + k; if (xx < 0 || xx >= w) continue; s += src[y * w + xx]; c++; }
                tmp[y * w + x] = s / c;
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float s = 0f; int c = 0;
                for (int k = -r; k <= r; k++) { int yy = y + k; if (yy < 0 || yy >= h) continue; s += tmp[yy * w + x]; c++; }
                dst[y * w + x] = s / c;
            }
        return dst;
    }

    // ---------- Atualização ----------

    void LateUpdate()
    {
        if (!cam) return;

        float dist = Mathf.Abs(cam.transform.position.z);
        RenderSettings.fogStartDistance = dist + 30f;   // névoa só bem ao longe (antes esmaecia o cenário)
        RenderSettings.fogEndDistance = dist + 320f;

        // Profundidade de campo: foco na torre; o cenário começa a desfocar logo atrás dela
        if (postFX)
        {
            postFX.focusStart = dist + FocusSplitZ + 4f;
            postFX.focusRange = 30f;
        }

        // Grupos de cenário presos às laterais: x = fração da meia largura visível naquela profundidade
        float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        foreach (var a in anchors)
        {
            float depth = dist + a.z;
            float halfW = depth * tanV * cam.aspect;
            // As peças são posicionadas com y = chão; com escala, o grupo compensa para não afundar
            a.t.position = new Vector3(a.side * halfW * a.edge, floorY * (1f - a.scale), a.z);
        }

        // Sol e nuvens: presos a pontos da tela, tamanho proporcional à altura do enquadramento
        float time = Time.unscaledTime;
        var rot = cam.transform.rotation;
        foreach (var s in screenSprites)
        {
            var vp = s.viewport + new Vector2(Mathf.Sin(time * 0.05f + s.phase) * s.sway, 0f);
            s.t.position = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, s.dist));
            s.t.rotation = rot;
            // Tamanho pela menor dimensão da tela: nuvens não ficam gigantes no celular em pé
            float worldH = 2f * s.dist * tanV * Mathf.Min(1f, cam.aspect * 9f / 16f);
            float spriteH = s.t.GetComponent<SpriteRenderer>().sprite.bounds.size.y;
            float scale = worldH * s.height / Mathf.Max(spriteH, 0.001f);
            s.t.localScale = Vector3.one * scale;
        }

        if (windmillBlades) windmillBlades.Rotate(0, 0, -25f * Time.deltaTime, Space.Self);
    }

}
