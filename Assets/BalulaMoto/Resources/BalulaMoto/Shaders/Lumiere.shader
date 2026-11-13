// BALULA MOTO — ampoules, fenêtres éclairées, phares : couleur du sommet (× _Color), sombre le jour et
// lumineuse la nuit. La nuit est donnée par la valeur globale _BalulaNight (0 = jour, 1 = nuit), réglée par
// WorldEnvironment : aucune liste d'objets à parcourir pour allumer la ville.
Shader "BalulaMoto/Lumiere"
{
    Properties
    {
        _Color ("Couleur", Color) = (1,1,1,1)
        _Jour ("Luminosité de jour", Range(0,1)) = 0.22
        _Nuit ("Luminosité de nuit", Range(0,4)) = 1.6
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            half _Jour, _Nuit;
            half _BalulaNight;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; half fog : TEXCOORD0; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Le canal alpha du sommet décale l'allumage : toutes les fenêtres ne s'allument pas en même temps
                half on = saturate((_BalulaNight - (1.0 - v.color.a) * 0.6) * 4.0);
                o.col = fixed4(v.color.rgb * _Color.rgb * lerp(_Jour, _Nuit, on), 1);
                float d = length(UnityObjectToViewPos(v.vertex));
                float f = unity_FogParams.x * d;
                o.fog = saturate(exp2(-f * f));
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                // De loin une lumière reste visible dans la brume : le brouillard ne l'éteint qu'à moitié
                fixed3 c = lerp(unity_FogColor.rgb, i.col.rgb, lerp(1.0, i.fog, 0.5 + 0.5 * (1.0 - _BalulaNight)));
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
