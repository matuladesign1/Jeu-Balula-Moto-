// BALULA MOTO — masque des cours d'eau en tranchée. Dessiné avant le sol (file Geometry-20), sans couleur ni
// profondeur : il marque seulement le stencil (valeur 4) là où passe la rivière. Le sol et la verdure (shader
// BalulaMoto/SolTrou) ne se dessinent pas sur ces pixels : on voit à travers le sol les berges et l'eau, posées
// plus bas. Pas de passe d'ombre (ni ombre portée, ni texture de profondeur).
Shader "BalulaMoto/RiviereMasque"
{
    SubShader
    {
        Tags { "Queue"="Geometry-20" "RenderType"="Opaque" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        Pass
        {
            ZWrite Off
            ColorMask 0
            Cull Off
            Stencil
            {
                Ref 4
                Comp Always
                Pass Replace
            }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target { return 0; }
            ENDCG
        }
    }
}
