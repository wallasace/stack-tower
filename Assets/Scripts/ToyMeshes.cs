using System.Collections.Generic;
using UnityEngine;

// Malhas procedurais do Toybox World: bloco com cantos arredondados (chanfro suave),
// cilindro/cone, pirâmide e prisma triangular. Poucos vértices, pensado para celular.
public static class ToyMeshes
{
    // ---------- Caixa arredondada ----------
    // Cada face é uma grade; os pontos perto das bordas são "puxados" para um arco de raio r
    // (ponto = interior + normal * r). Resultado: chanfro suave, normais lisas, ~300 vértices.

    static readonly Dictionary<long, Mesh> boxCache = new Dictionary<long, Mesh>();

    // cached = true: malha compartilhada (cenário, tamanhos fixos).
    // cached = false: malha própria de quem pediu (blocos cortados em larguras únicas); quem pede apaga.
    public static Mesh RoundedBox(Vector3 size, float radius, int segments = 3, bool cached = true)
    {
        long key = ((long)Mathf.RoundToInt(size.x * 1000) << 40) ^ ((long)Mathf.RoundToInt(size.y * 1000) << 20)
                   ^ Mathf.RoundToInt(size.z * 1000) ^ ((long)segments << 60);
        if (cached && boxCache.TryGetValue(key, out var hit) && hit) return hit;

        var h = size * 0.5f;
        float r = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        for (int axis = 0; axis < 3; axis++)
            for (int sign = -1; sign <= 1; sign += 2)
                AddFace(axis, sign, h, r, segments, verts, normals, tris);

        var mesh = new Mesh { name = "RoundedBox" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        if (cached) boxCache[key] = mesh;
        return mesh;
    }

    static List<float> AxisSamples(float h, float r, int seg)
    {
        var s = new List<float> { -h };
        for (int k = 1; k <= seg; k++) s.Add(-h + r * k / seg);
        for (int k = seg; k >= 1; k--) s.Add(h - r * k / seg);
        s.Add(h);
        return s;
    }

    static void AddFace(int axis, int sign, Vector3 h, float r, int seg,
        List<Vector3> verts, List<Vector3> normals, List<int> tris)
    {
        int ua = (axis + 1) % 3, va = (axis + 2) % 3;
        var us = AxisSamples(h[ua], r, seg);
        var vs = AxisSamples(h[va], r, seg);
        var inner = new Vector3(h.x - r, h.y - r, h.z - r);
        var faceNormal = Vector3.zero;
        faceNormal[axis] = sign;

        int start = verts.Count;
        foreach (float v in vs)
            foreach (float u in us)
            {
                var p = Vector3.zero;
                p[axis] = sign * h[axis];
                p[ua] = u;
                p[va] = v;
                var c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x),
                                    Mathf.Clamp(p.y, -inner.y, inner.y),
                                    Mathf.Clamp(p.z, -inner.z, inner.z));
                var n = p - c;
                n = n.sqrMagnitude > 1e-8f ? n.normalized : faceNormal;
                verts.Add(c + n * r);
                normals.Add(n);
            }

        int w = us.Count;
        // A ordem dos vértices depende do eixo/sinal; testa a primeira célula e inverte se preciso
        var e1 = verts[start + 1] - verts[start];
        var e2 = verts[start + w] - verts[start];
        bool flip = Vector3.Dot(Vector3.Cross(e2, e1), faceNormal) < 0f;
        for (int j = 0; j < vs.Count - 1; j++)
            for (int i = 0; i < w - 1; i++)
            {
                int a = start + j * w + i, b = a + 1, c = a + w, d = c + 1;
                if (!flip) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
            }
    }

    // ---------- Cilindro / cone / pirâmide ----------
    // Base no y = 0. radiusTop = 0 vira cone (sides = 4 vira pirâmide).

    public static Mesh Cylinder(float radiusBottom, float radiusTop, float height, int sides, bool smooth = true,
        float angleOffset = 0f)
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();
        float slope = (radiusBottom - radiusTop) / height;

        for (int i = 0; i < sides; i++)
        {
            float a0 = angleOffset + i * Mathf.PI * 2f / sides, a1 = angleOffset + (i + 1) * Mathf.PI * 2f / sides;
            var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
            var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            var b0 = d0 * radiusBottom; var b1 = d1 * radiusBottom;
            var t0 = d0 * radiusTop + Vector3.up * height; var t1 = d1 * radiusTop + Vector3.up * height;
            Vector3 n0, n1;
            if (smooth) { n0 = (d0 + Vector3.up * slope).normalized; n1 = (d1 + Vector3.up * slope).normalized; }
            else { n0 = n1 = Vector3.Cross(t0 - b0, b1 - b0).normalized; }

            int s = verts.Count;
            verts.AddRange(new[] { b0, b1, t1, t0 });
            normals.AddRange(new[] { n0, n1, n1, n0 });
            tris.AddRange(new[] { s, s + 3, s + 1, s + 1, s + 3, s + 2 });

            // Tampa de baixo
            int c = verts.Count;
            verts.AddRange(new[] { Vector3.zero, b0, b1 });
            normals.AddRange(new[] { Vector3.down, Vector3.down, Vector3.down });
            tris.AddRange(new[] { c, c + 1, c + 2 });
            // Tampa de cima
            if (radiusTop > 0f)
            {
                int t = verts.Count;
                verts.AddRange(new[] { Vector3.up * height, t1, t0 });
                normals.AddRange(new[] { Vector3.up, Vector3.up, Vector3.up });
                tris.AddRange(new[] { t, t + 1, t + 2 });
            }
        }
        var mesh = new Mesh { name = "Cylinder" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    public static Mesh Pyramid(float baseSize, float height) =>
        Cylinder(baseSize * 0.7071f, 0f, height, 4, false, Mathf.PI / 4f);

    // ---------- Prisma triangular ----------
    // Triângulo de base "width" e altura "height", extrudado em "depth". Base no y = 0.
    public static Mesh Prism(float width, float height, float depth)
    {
        float hw = width / 2f, hd = depth / 2f;
        Vector3 a = new Vector3(-hw, 0, 0), b = new Vector3(hw, 0, 0), c = new Vector3(0, height, 0);
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var n = Vector3.Cross(p1 - p0, p3 - p0).normalized;
            int s = verts.Count;
            verts.AddRange(new[] { p0, p1, p2, p3 });
            normals.AddRange(new[] { n, n, n, n });
            tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
        }
        void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            var n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            int s = verts.Count;
            verts.AddRange(new[] { p0, p1, p2 });
            normals.AddRange(new[] { n, n, n });
            tris.AddRange(new[] { s, s + 1, s + 2 });
        }

        var f = Vector3.back * hd; var k = Vector3.forward * hd;
        Tri(a + f, c + f, b + f);                 // frente
        Tri(a + k, b + k, c + k);                 // trás
        Quad(a + f, a + k, c + k, c + f);         // lado esquerdo
        Quad(c + f, c + k, b + k, b + f);         // lado direito
        Quad(a + f, b + f, b + k, a + k);         // base

        var mesh = new Mesh { name = "Prism" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
