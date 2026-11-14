// BALULA MOTO — nuages de poussière soulevés par les véhicules (avenues de terre, trottoirs) : disque doux transparent
// teinté par la couleur des particules, sans texture, avec le brouillard de la scène.
Shader "BalulaMoto/Poussiere"
{
    Properties
    {
        _Color ("Couleur", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.col = v.color * _Color;
                o.uv = v.uv * 2.0 - 1.0;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                half r2 = dot(i.uv, i.uv);
                half a = saturate(1.0 - r2);
                a = a * a;
                fixed4 c = fixed4(i.col.rgb, i.col.a * a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
