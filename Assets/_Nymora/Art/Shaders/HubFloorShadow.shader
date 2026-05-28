Shader "Nymora/FloorShadow"
{
    // Silhouette NOIRE d'un sprite avec dégradé d'opacité le long de la hauteur du sprite
    // (opaque à la base uv.y=0, transparent à la pointe uv.y=1) -> ombre projetée qui s'estompe.
    // Unlit + transparent : insensible aux lumières, rend correctement en URP 2D.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Alpha ("Global Alpha", Range(0,1)) = 1
        _FadeBase ("Alpha base (uv.y=0)", Range(0,1)) = 1
        _FadeTip ("Alpha pointe (uv.y=1)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _Alpha;
            float _FadeBase;
            float _FadeTip;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed texA = tex2D(_MainTex, IN.texcoord).a;
                float grad = lerp(_FadeBase, _FadeTip, saturate(IN.texcoord.y));
                fixed a = texA * IN.color.a * _Alpha * grad;
                return fixed4(0, 0, 0, a); // silhouette noire, opacité dégradée
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
