using UnityEngine;
using UnityEngine.UI;

// Céu estrelado com parallax (inspirado em "Pure CSS Parallax Pixel Stars"):
// três camadas de estrelas quadradas (1, 2 e 3 px) subindo em velocidades diferentes.
// Cada camada é uma textura que se repete; a animação só desloca o UV, então é leve até no celular.
public class StarField : MonoBehaviour
{
    const int Tile = 512;      // tamanho da textura de cada camada
    const float TexelSize = 2f; // cada pixel da textura ocupa 2 unidades da tela de referência

    struct Layer { public RawImage image; public float speed; }
    Layer[] layers;
    RectTransform area;

    // Densidade igual à do original: 700 / 200 / 100 estrelas em 2000x2000 px
    public static StarField Create(Transform parent)
    {
        var go = new GameObject("Stars", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var field = go.AddComponent<StarField>();
        field.area = rt;
        field.layers = new[]
        {
            field.MakeLayer(1, 700, 40f, 101), // pequenas: mais rápidas (50s para 2000px)
            field.MakeLayer(2, 200, 20f, 202), // médias (100s)
            field.MakeLayer(3, 100, 13.3f, 303), // grandes: mais lentas (150s)
        };
        return field;
    }

    Layer MakeLayer(int starSize, int countPer2000, float speed, int seed)
    {
        var tex = new Texture2D(Tile, Tile, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
        };
        var px = new Color32[Tile * Tile];
        var rng = new System.Random(seed);
        // Mesma densidade do original, ajustada para a área da textura
        int count = Mathf.Max(1, Mathf.RoundToInt(countPer2000 * (Tile * (float)Tile) / (2000f * 2000f)));
        var white = new Color32(255, 255, 255, 255);
        for (int i = 0; i < count; i++)
        {
            int sx = rng.Next(Tile), sy = rng.Next(Tile);
            for (int y = 0; y < starSize; y++)
                for (int x = 0; x < starSize; x++)
                    px[((sy + y) % Tile) * Tile + (sx + x) % Tile] = white;
        }
        tex.SetPixels32(px);
        tex.Apply();

        var go = new GameObject("Layer" + starSize, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var img = go.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return new Layer { image = img, speed = speed };
    }

    void Update()
    {
        var size = area.rect.size;
        float uvW = size.x / (Tile * TexelSize), uvH = size.y / (Tile * TexelSize);
        float t = Time.unscaledTime;
        foreach (var l in layers)
        {
            // Estrelas sobem: o UV desce com o tempo
            float y = -Mathf.Repeat(t * l.speed / (Tile * TexelSize), 1f);
            l.image.uvRect = new Rect(0f, y, uvW, uvH);
        }
    }
}
