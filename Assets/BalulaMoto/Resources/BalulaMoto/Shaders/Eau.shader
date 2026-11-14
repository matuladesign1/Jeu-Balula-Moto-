// BALULA MOTO — eau des rivières en tranchée : turquoise comme sur la photo de rivière envoyée par MATULA DESIGN,
// rides qui bougent lentement (deux lectures décalées de la texture eau_ondes, qui contient les pentes des rides),
// reflets du soleil ; un peu plus sombre vue de loin (sinon le reflet du ciel la faisait paraître claire, comme une allée).
Shader "BalulaMoto/Eau"
{
    Properties
    {
        _Color ("Couleur (vue de près)", Color) = (0.16, 0.42, 0.40, 1)
        _Deep ("Couleur (vue de loin)", Color) = (0.09, 0.27, 0.30, 1)
        _Ripple ("Rides (pentes en rouge et vert)", 2D) = "gray" {}
        _Strength ("Force des rides", Range(0, 2)) = 0.55
        _Speed ("Vitesse des rides", Float) = 0.035
        _Glossiness ("Lissé", Range(0,1)) = 0.72
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard
        #pragma target 3.0
        sampler2D _Ripple;
        fixed4 _Color;
        fixed4 _Deep;
        float _Strength;
        float _Speed;
        half _Glossiness;
        struct Input
        {
            float2 uv_Ripple;
            float3 viewDir;
        };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float t = _Time.y * _Speed;
            float2 uv = IN.uv_Ripple;
            half2 g1 = tex2D(_Ripple, uv + float2(t, t * 0.63)).rg * 2 - 1;
            half2 g2 = tex2D(_Ripple, uv * 1.73 + float2(-t * 0.71, t * 0.37)).rg * 2 - 1;
            half2 g = (g1 + g2 * 0.6) * _Strength;
            o.Normal = normalize(half3(g.x, g.y, 1));
            half facing = saturate(normalize(IN.viewDir).z);
            o.Albedo = lerp(_Deep.rgb, _Color.rgb, facing);
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
