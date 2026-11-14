// BALULA MOTO — feuillage des arbres (cartes de feuilles texturées avec transparence découpée), visible des deux
// côtés : les deux faces d'une carte sont éclairées de la même façon (pas de feuilles noires vues de dos), ombres
// portées découpées selon la forme des feuilles, brouillard. Utilisé pour les arbres fournis par MATULA DESIGN (CC0).
Shader "BalulaMoto/Feuillage"
{
    Properties
    {
        _Color ("Couleur", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Seuil de transparence", Range(0,1)) = 0.42
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Feuille alphatest:_Cutoff addshadow
        #pragma target 2.0
        sampler2D _MainTex;
        fixed4 _Color;
        struct Input { float2 uv_MainTex; };

        half4 LightingFeuille (SurfaceOutput s, half3 lightDir, half atten)
        {
            // Lumière des deux côtés de la carte, adoucie (le soleil traverse un peu le feuillage)
            half ndl = abs(dot(s.Normal, lightDir)) * 0.7 + 0.3;
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * (ndl * atten);
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
