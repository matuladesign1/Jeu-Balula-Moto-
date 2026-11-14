// BALULA MOTO — fil de fer barbelé (concertina) au-dessus des murs de clôture : texture découpée (les trous laissent
// voir le ciel), visible des deux côtés, éclairée simplement.
Shader "BalulaMoto/Barbele"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Couleur", Color) = (1,1,1,1)
        _Cutoff ("Seuil de découpe", Range(0,1)) = 0.45
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert alphatest:_Cutoff addshadow
        sampler2D _MainTex;
        fixed4 _Color;
        struct Input { float2 uv_MainTex; };
        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    Fallback "Legacy Shaders/Transparent/Cutout/VertexLit"
}
