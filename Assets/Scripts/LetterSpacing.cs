using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Espaçamento entre letras para o Text do Unity (que não tem essa opção).
// spacing é em "em": 0.1 = 10% do tamanho da fonte entre cada letra. Só para textos de uma linha.
// Precisa vir ANTES do Shadow no GameObject, senão a sombra fica desalinhada.
[RequireComponent(typeof(Text))]
public class LetterSpacing : BaseMeshEffect
{
    [SerializeField] float spacing;
    static readonly List<UIVertex> verts = new List<UIVertex>();

    public float Spacing
    {
        get => spacing;
        set { spacing = value; if (graphic) graphic.SetVerticesDirty(); }
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || Mathf.Approximately(spacing, 0f)) return;
        var text = (Text)graphic;
        if (text.text.IndexOf('\n') >= 0) return;

        verts.Clear();
        vh.GetUIVertexStream(verts);
        int quads = verts.Count / 6; // cada letra = 2 triângulos
        if (quads < 2) return;

        float px = spacing * text.fontSize;
        float start = text.alignment switch
        {
            TextAnchor.UpperLeft or TextAnchor.MiddleLeft or TextAnchor.LowerLeft => 0f,
            TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight => -(quads - 1) * px,
            _ => -(quads - 1) * px / 2f,
        };
        for (int q = 0; q < quads; q++)
        {
            float dx = start + q * px;
            for (int v = 0; v < 6; v++)
            {
                var vert = verts[q * 6 + v];
                vert.position.x += dx;
                verts[q * 6 + v] = vert;
            }
        }
        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }
}
