// BALULA MOTO — flaques de lumière au sol (lampadaires, phares, ampoules des parcelles) : disque additif
// dont l'intensité suit la nuit (_BalulaNight). Dessiné seulement la nuit, sans écrire dans la profondeur.
Shader "BalulaMoto/Halo"
{
    Properties
    {
        _Color ("Couleur", Color) = (1,1,1,1)
        _Force ("Intensité", Range(0,3)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            half _Force;
            half _BalulaNight;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float d = length(UnityObjectToViewPos(v.vertex));
                float f = unity_FogParams.x * d;
                half on = saturate((_BalulaNight - 0.15) * 2.0);
                o.col = fixed4(v.color.rgb * _Color.rgb * (_Force * on * saturate(exp2(-f * f))), 1);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                half r = saturate(1.0 - dot(i.uv, i.uv));
                return fixed4(i.col.rgb * r * r, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
