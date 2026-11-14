// BALULA MOTO — sol, verdure et étendues d'eau plates de la carte importée : éclairage « Standard » identique au
// matériau habituel, mais rien n'est dessiné là où le masque des rivières (BalulaMoto/RiviereMasque) a marqué le
// stencil : la tranchée de la rivière (berges en pente, eau plus bas) apparaît à travers le sol.
Shader "BalulaMoto/SolTrou"
{
    Properties
    {
        _Color ("Couleur", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Glossiness ("Lissé", Range(0,1)) = 0.08
        _Metallic ("Métal", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Stencil
        {
            Ref 4
            Comp NotEqual
        }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        struct Input { float2 uv_MainTex; };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
