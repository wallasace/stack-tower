// Madeira pintada de brinquedo (Toybox World) — PBR "Standard", tudo procedural (sem texturas):
// - veio da madeira por ruído fractal (fbm) com anéis deformados + fibras finas + poros,
//   aparecendo por baixo da tinta (albedo e rugosidade variam com o veio);
// - pintura gasta nas arestas revelando madeira crua;
// - quinas brilhantes: o chanfro é mais liso (reflexo especular forte do sol) e ganha um brilho
//   de borda quando está virado para a luz, que o bloom transforma em "glint";
// - oclusão falsa na base. Instancing para cor/tamanho por objeto (um material para todos os blocos).
Shader "Toybox/PaintedWood"
{
    Properties
    {
        _Color ("Cor da pintura", Color) = (1,1,1,1)
        _WoodColor ("Madeira crua (bordas gastas)", Color) = (0.85,0.67,0.45,1)
        _HalfSize ("Meio tamanho do objeto (x,y,z)", Vector) = (1,0.25,0.45,0)
        _Seed ("Semente do veio", Float) = 0
        _Glossiness ("Suavidade da tinta", Range(0,1)) = 0.35
        _EdgeGloss ("Suavidade nas quinas", Range(0,1)) = 0.72
        _GrainScale ("Escala do veio", Float) = 5
        _GrainStrength ("Força do veio", Range(0,0.4)) = 0.14
        _WearWidth ("Largura do desgaste nas bordas", Range(0,0.1)) = 0.03
        _WearAmount ("Quantidade de desgaste", Range(0,1)) = 0.3
        _BaseAO ("Oclusão na base", Range(0,1)) = 0.3
        _EdgeGlow ("Brilho das quinas viradas para o sol", Range(0,2)) = 0.6
        _EdgeGlowColor ("Cor do brilho das quinas", Color) = (1,0.93,0.78,1)
        _PlankWidth ("Largura das tábuas (0 = sem)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.5
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling

        fixed4 _WoodColor, _EdgeGlowColor;
        half _Glossiness, _EdgeGloss, _GrainScale, _GrainStrength, _WearWidth, _WearAmount, _BaseAO, _EdgeGlow, _PlankWidth;

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_DEFINE_INSTANCED_PROP(float4, _HalfSize)
            UNITY_DEFINE_INSTANCED_PROP(float, _Seed)
        UNITY_INSTANCING_BUFFER_END(Props)

        struct Input
        {
            float3 objPos;
            float3 worldNormal;
            float3 viewDir;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex.xyz;
        }

        float hash3(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float noise3(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(hash3(i + float3(0,0,0)), hash3(i + float3(1,0,0)), f.x),
                             lerp(hash3(i + float3(0,1,0)), hash3(i + float3(1,1,0)), f.x), f.y),
                        lerp(lerp(hash3(i + float3(0,0,1)), hash3(i + float3(1,0,1)), f.x),
                             lerp(hash3(i + float3(0,1,1)), hash3(i + float3(1,1,1)), f.x), f.y), f.z);
        }

        float fbm(float3 p)
        {
            float s = 0.0, a = 0.5;
            for (int k = 0; k < 4; k++) { s += noise3(p) * a; p *= 2.03; a *= 0.5; }
            return s;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 paint = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
            float3 hs = UNITY_ACCESS_INSTANCED_PROP(Props, _HalfSize).xyz;
            float seed = UNITY_ACCESS_INSTANCED_PROP(Props, _Seed);
            float3 p = IN.objPos;

            // ---- Veio: tábua serrada no sentido do comprimento (x) ----
            float3 q = p * _GrainScale + seed * 7.13;
            float warp = fbm(q * float3(0.15, 0.9, 0.9)) * 2.2;                 // deforma os anéis
            float rings = frac((p.y * 2.3 + p.z * 1.7) * _GrainScale * 0.9 + warp);
            float ringLine = smoothstep(0.0, 0.18, rings) * smoothstep(1.0, 0.55, rings); // lenho claro/escuro
            float fibers = noise3(q * float3(0.35, 14.0, 14.0));                // fibras finas e longas
            float pores = step(0.82, noise3(q * float3(1.2, 30.0, 30.0)));      // poros escuros
            float grain = saturate(ringLine * 0.6 + fibers * 0.4 - pores * 0.25);
            float shade = 1.0 - _GrainStrength + grain * _GrainStrength * 1.4;

            // Tábuas (chão): frestas finas no eixo z
            if (_PlankWidth > 0.0)
            {
                float s = abs(frac(p.z / _PlankWidth) - 0.5) * 2.0;
                shade *= lerp(0.86, 1.0, saturate((1.0 - s) * 45.0));
            }

            // ---- Arestas: distância até a aresta mais próxima (onde duas faces se encontram) ----
            float3 d = hs - abs(p);
            float edge = min(max(d.x, d.y), min(max(d.y, d.z), max(d.x, d.z)));
            float wearMask = 1.0 - smoothstep(0.0, _WearWidth, edge);
            float wearNoise = fbm(p * 18.0 + seed * 3.1);
            float wear = saturate(wearMask * smoothstep(1.0 - _WearAmount, 1.0, wearNoise + wearMask * 0.3));
            float bevel = 1.0 - smoothstep(0.0, 0.07, edge);                  // faixa do chanfro

            fixed3 painted = paint.rgb * shade;
            fixed3 bare = _WoodColor.rgb * (0.8 + grain * 0.35);
            fixed3 albedo = lerp(painted, bare, wear);

            // Oclusão falsa perto da face de baixo
            float ao = lerp(1.0 - _BaseAO, 1.0, smoothstep(-hs.y, -hs.y + 0.14, p.y));

            // Quinas viradas para o sol ganham um brilho quente (vira "glint" com o bloom)
            float3 n = normalize(IN.worldNormal);
            float3 l = normalize(_WorldSpaceLightPos0.xyz);
            float facing = saturate(dot(n, l));
            float rim = pow(1.0 - saturate(dot(n, normalize(IN.viewDir))), 3.0);

            o.Albedo = albedo * ao;
            o.Metallic = 0.0;
            // Tinta semi-fosca com variação pelo veio; chanfro bem mais liso (reflexo forte nas quinas)
            float gloss = _Glossiness * lerp(0.75, 1.1, grain);
            gloss = lerp(gloss, _EdgeGloss, bevel * (1.0 - wear));
            o.Smoothness = gloss * lerp(1.0, 0.5, wear);
            o.Occlusion = ao;
            o.Emission = _EdgeGlowColor.rgb * paint.rgb * (bevel * facing * _EdgeGlow + rim * facing * 0.15);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
