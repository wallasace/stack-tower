// Céu de dia para o skybox (Toybox World): degradê azul-ciano no horizonte -> azul céu no alto,
// sol com brilho quente no alto à esquerda. Um único cálculo por pixel, sem texturas.
Shader "Toybox/Sky"
{
    Properties
    {
        _ZenithColor ("Alto do céu", Color) = (0.18,0.55,0.93,1)
        _SkyColor ("Céu", Color) = (0.28,0.65,0.96,1)
        _HorizonColor ("Horizonte", Color) = (0.66,0.86,0.99,1)
        _SunDir ("Direção do sol", Vector) = (-0.55,0.45,0.7,0)
        _SunColor ("Cor do brilho do sol", Color) = (1,0.95,0.8,1)
        _SunSize ("Tamanho do disco", Range(0.9,0.9999)) = 0.9965
        _SunGlow ("Espalhamento do brilho", Range(1,64)) = 6
        _SunGlowStrength ("Força do brilho", Range(0,2)) = 0.85
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ZenithColor, _SkyColor, _HorizonColor, _SunColor;
            float4 _SunDir;
            half _SunSize, _SunGlow, _SunGlowStrength;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = saturate(d.y);
                // Horizonte claro -> céu -> alto mais saturado
                fixed3 col = lerp(_HorizonColor.rgb, _SkyColor.rgb, smoothstep(0.0, 0.1, h));
                col = lerp(col, _ZenithColor.rgb, smoothstep(0.08, 0.36, h));
                // Abaixo do horizonte fica na cor do horizonte (escondido pelo chão)
                col = d.y < 0.0 ? _HorizonColor.rgb : col;

                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                float glow = pow(s, _SunGlow) * _SunGlowStrength;
                float disc = smoothstep(_SunSize, _SunSize + 0.002, s);
                col = col + _SunColor.rgb * glow;
                col = lerp(col, fixed3(1.0, 0.99, 0.93), disc);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
