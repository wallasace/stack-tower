using UnityEngine;

// Pós-processamento da câmera principal (pipeline padrão): bloom em cadeia (estilo Call of Duty /
// Unreal: downsample + upsample com filtro tenda), glare anamórfico sutil, exposição, contraste,
// saturação, tonemapping ACES e vinheta. Precisa de HDR na câmera e espaço de cor linear.
[RequireComponent(typeof(Camera))]
public class PostFX : MonoBehaviour
{
    [Header("Bloom")]
    public float threshold = 1.15f;
    [Range(0, 1)] public float softKnee = 0.6f;
    public float bloomIntensity = 0.32f;
    [Header("Glare (rastro horizontal nas luzes fortes)")]
    public float glareIntensity = 0.14f;
    public Color glareTint = new Color(1f, 0.93f, 0.8f);
    [Header("Cor")]
    public float exposure = 1f;
    public float contrast = 1.08f;
    public float saturation = 1.04f;
    public Color lift = new Color(0.004f, 0.006f, 0.012f);
    public Color gain = new Color(1.02f, 1f, 0.97f);
    [Range(0, 1)] public float vignette = 0.32f;
    [Header("Profundidade de campo (fundo desfocado)")]
    public float focusStart = 30f;   // a partir desta distância (m) começa a desfocar
    public float focusRange = 18f;   // distância até o desfoque máximo
    [Range(0, 1)] public float dofStrength = 0.55f;
    [Range(1, 6)] public int dofIterations = 2;

    const int MaxIterations = 7;
    static readonly int FilterId = Shader.PropertyToID("_Filter");

    Material mat;
    readonly RenderTexture[] chain = new RenderTexture[MaxIterations];

    public static Material CreateMaterial()
    {
        var s = Shader.Find("Hidden/Toybox/PostFX");
        return s ? new Material(s) { hideFlags = HideFlags.HideAndDontSave } : null;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (!mat) mat = CreateMaterial();
        if (!mat) { Graphics.Blit(src, dst); return; }

        float knee = threshold * softKnee;
        mat.SetVector(FilterId, new Vector4(threshold, threshold - knee, 2f * knee, 0.25f / (knee + 1e-5f)));
        var fmt = RenderTextureFormat.DefaultHDR;

        // Bloom: meia resolução e descendo
        int w = src.width / 2, h = src.height / 2;
        var cur = chain[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
        Graphics.Blit(src, cur, mat, 0);
        int n = 1;
        for (; n < MaxIterations; n++)
        {
            w /= 2; h /= 2;
            if (w < 4 || h < 4) break;
            chain[n] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(cur, chain[n], mat, 1);
            cur = chain[n];
        }

        // Glare: rastro horizontal a partir de 1/8 da resolução, em duas passadas de alcance crescente
        var glareSrc = chain[Mathf.Min(2, n - 1)];
        var streakA = RenderTexture.GetTemporary(glareSrc.width, glareSrc.height, 0, fmt);
        var streakB = RenderTexture.GetTemporary(glareSrc.width, glareSrc.height, 0, fmt);
        mat.SetVector("_Dir", new Vector2(1.5f, 0));
        Graphics.Blit(glareSrc, streakA, mat, 3);
        mat.SetVector("_Dir", new Vector2(5f, 0));
        Graphics.Blit(streakA, streakB, mat, 3);

        // Sobe somando cada nível (bloom macio e largo)
        for (int i = n - 2; i >= 0; i--)
        {
            Graphics.Blit(cur, chain[i], mat, 2);
            cur = chain[i];
        }

        // Profundidade de campo: cópia desfocada (Kawase, meia resolução) misturada pela profundidade
        var blurA = RenderTexture.GetTemporary(src.width / 2, src.height / 2, 0, fmt);
        var blurB = RenderTexture.GetTemporary(src.width / 2, src.height / 2, 0, fmt);
        Graphics.Blit(src, blurA);
        for (int i = 0; i < dofIterations; i++)
        {
            mat.SetFloat("_Offset", i);
            Graphics.Blit(blurA, blurB, mat, 5);
            (blurA, blurB) = (blurB, blurA);
        }
        mat.SetTexture("_BlurTex", blurA);
        mat.SetVector("_Focus", new Vector4(focusStart, 1f / Mathf.Max(0.01f, focusRange), dofStrength, 0));

        mat.SetTexture("_BloomTex", cur);
        mat.SetTexture("_StreakTex", streakB);
        mat.SetFloat("_BloomIntensity", bloomIntensity);
        mat.SetFloat("_GlareIntensity", glareIntensity);
        mat.SetColor("_GlareTint", glareTint);
        mat.SetFloat("_Exposure", exposure);
        mat.SetFloat("_Contrast", contrast);
        mat.SetFloat("_Saturation", saturation);
        mat.SetColor("_Lift", lift);
        mat.SetColor("_Gain", gain);
        mat.SetFloat("_Vignette", vignette);
        Graphics.Blit(src, dst, mat, 4);

        for (int i = 0; i < n; i++) { RenderTexture.ReleaseTemporary(chain[i]); chain[i] = null; }
        RenderTexture.ReleaseTemporary(streakA);
        RenderTexture.ReleaseTemporary(streakB);
        RenderTexture.ReleaseTemporary(blurA);
        RenderTexture.ReleaseTemporary(blurB);
    }

    void OnDestroy()
    {
        if (mat) Destroy(mat);
    }
}
