// BALULA MOTO — texte 3D (plaques, enseignes, panneaux, inscriptions des véhicules).
// Remplace le shader par défaut des polices (« GUI/Text Shader »), qui dessine le texte
// par-dessus tout le reste (ZTest Always) et des deux côtés (Cull Off) : on voyait les
// plaques à travers les motos et les inscriptions de l'autre côté d'une citerne, à l'envers.
// Ici le texte est caché par ce qui se trouve devant lui et n'est dessiné que du côté
// où il se lit (le côté -Z local d'un TextMesh), quel que soit l'ordre des triangles.
Shader "BalulaMoto/Texte3D"
{
    Properties
    {
        _MainTex ("Texture de la police", 2D) = "white" {}
        _Color ("Couleur", Color) = (1,1,1,1)
    }
    SubShader
    {
        // DisableBatching : le test du côté lisible a besoin de la matrice propre à chaque texte.
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "DisableBatching"="True" }
        Lighting Off
        Cull Off
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float side : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata_t v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 axeZ = mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1));
                // Négatif quand la caméra est du côté -Z du texte, c'est-à-dire du côté où il se lit.
                o.side = dot(_WorldSpaceCameraPos - worldPos, axeZ);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                clip(-i.side);
                fixed4 col = i.color;
                col.a *= tex2D(_MainTex, i.texcoord).a;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
