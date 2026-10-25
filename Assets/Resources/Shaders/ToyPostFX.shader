// Pós-processamento do Toybox World (pipeline padrão, OnRenderImage):
// 0 pré-filtro do bloom (limiar suave)   1 downsample   2 upsample aditivo (filtro tenda)
// 3 glare: rastro horizontal (anamórfico)   4 composição: bloom + glare, exposição, contraste/saturação,
//   tonemapping ACES e vinheta   5 desfoque Kawase (fundo desfocado)
Shader "Hidden/Toybox/PostFX"
{
    Properties { _MainTex ("", 2D) = "white" {} }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex, _BloomTex, _StreakTex, _BlurTex;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float4 _Focus;           // início do desfoque, 1/alcance, força
    float4 _MainTex_TexelSize;
    float4 _Filter;          // limiar, limiar - joelho, 2*joelho, 0.25/joelho
    float2 _Dir;             // direção do rastro de glare
    float _Offset;           // passo do Kawase
    half _BloomIntensity, _GlareIntensity, _Exposure, _Contrast, _Saturation, _Vignette;
    half3 _GlareTint, _Lift, _Gain;

    half3 Tap(float2 uv) { return tex2D(_MainTex, uv).rgb; }

    half3 SampleBox(float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-delta, delta).xxyy;
        return (Tap(uv + o.xy) + Tap(uv + o.zy) + Tap(uv + o.xw) + Tap(uv + o.zw)) * 0.25;
    }

    half3 Prefilter(half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Filter.y, 0, _Filter.z);
        soft = soft * soft * _Filter.w;
        half contrib = max(soft, br - _Filter.x) / max(br, 1e-4);
        return c * contrib;
    }

    // Curva ACES (ajuste de Narkowicz): realces suaves, sombras ricas, cores saturadas sem estourar
    half3 ACES(half3 x)
    {
        return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
    }

    half4 FragPrefilter(v2f_img i) : SV_Target { return half4(Prefilter(SampleBox(i.uv, 1)), 1); }
    half4 FragDown(v2f_img i) : SV_Target { return half4(SampleBox(i.uv, 1), 1); }
    half4 FragUp(v2f_img i) : SV_Target { return half4(SampleBox(i.uv, 0.5), 1); }

    half4 FragStreak(v2f_img i) : SV_Target
    {
        float2 step = _Dir * _MainTex_TexelSize.xy;
        half3 c = Tap(i.uv) * 0.2;
        c += (Tap(i.uv + step * 1.5) + Tap(i.uv - step * 1.5)) * 0.17;
        c += (Tap(i.uv + step * 3.5) + Tap(i.uv - step * 3.5)) * 0.12;
        c += (Tap(i.uv + step * 6.0) + Tap(i.uv - step * 6.0)) * 0.08;
        c += (Tap(i.uv + step * 9.0) + Tap(i.uv - step * 9.0)) * 0.03;
        return half4(c, 1);
    }

    half4 FragComposite(v2f_img i) : SV_Target
    {
        half3 c = Tap(i.uv);
        // Profundidade de campo: mistura com a versão desfocada conforme a distância
        float raw = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
        float eye = LinearEyeDepth(raw);
        half coc = saturate((eye - _Focus.x) * _Focus.y) * _Focus.z;
        // Céu, sol e nuvens não escrevem profundidade: desfoque mais leve para manter as nuvens definidas
        if (Linear01Depth(raw) > 0.999) coc *= 0.3;
        c = lerp(c, tex2D(_BlurTex, i.uv).rgb, coc);
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
        c += tex2D(_StreakTex, i.uv).rgb * _GlareTint * _GlareIntensity;
        c *= _Exposure;

        // Contraste em torno do cinza médio (linear) e saturação
        c = pow(max(c, 1e-5) / 0.18, _Contrast) * 0.18;
        half l = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = max(0, lerp(l.xxx, c, _Saturation));
        c = c * _Gain + _Lift;

        // Tonemapping que preserva o matiz: a curva ACES por canal desvia tons claros saturados
        // (madeira vira laranja, coral vira vermelho); misturamos com a versão aplicada só na luminância
        half lum = max(dot(c, half3(0.2126, 0.7152, 0.0722)), 1e-4);
        half3 byLum = c * (ACES(lum.xxx).x / lum);
        c = saturate(lerp(ACES(c), byLum, 0.65));

        float2 d = i.uv - 0.5;
        d.x *= _MainTex_TexelSize.z / _MainTex_TexelSize.w; // vinheta redonda em qualquer proporção
        c *= 1 - _Vignette * smoothstep(0.35, 1.1, length(d));
        return half4(c, 1);
    }

    half4 FragKawase(v2f_img i) : SV_Target
    {
        float2 o = _MainTex_TexelSize.xy * (_Offset + 0.5);
        half4 c = tex2D(_MainTex, i.uv + float2(o.x, o.y));
        c += tex2D(_MainTex, i.uv + float2(-o.x, o.y));
        c += tex2D(_MainTex, i.uv + float2(o.x, -o.y));
        c += tex2D(_MainTex, i.uv + float2(-o.x, -o.y));
        return c * 0.25;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragStreak
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragComposite
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragKawase
            ENDCG }
    }
    Fallback Off
}
