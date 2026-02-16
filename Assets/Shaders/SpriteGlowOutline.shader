Shader "Custom/SpriteGlowOutline"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _GlowColor("Glow Color", Color) = (1,1,1,1)
        _GlowSize("Glow Size", Float) = 1.5
        _GlowStrength("Glow Strength", Float) = 1.25
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _GlowColor;
            float _GlowSize;
            float _GlowStrength;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, IN.uv) * IN.color;
                float baseAlpha = baseCol.a;

                float2 px = _MainTex_TexelSize.xy * max(_GlowSize, 0.0);
                float neighborA = 0.0;

                neighborA = max(neighborA, tex2D(_MainTex, IN.uv + float2( px.x, 0)).a);
                neighborA = max(neighborA, tex2D(_MainTex, IN.uv + float2(-px.x, 0)).a);
                neighborA = max(neighborA, tex2D(_MainTex, IN.uv + float2(0,  px.y)).a);
                neighborA = max(neighborA, tex2D(_MainTex, IN.uv + float2(0, -px.y)).a);
                neighborA = max(neighborA, tex2D(_MainTex, IN.uv + float2( px.x,  px.y)).a);
                neighborA = max(neighborA, tex2D(_MainTex, IN.uv + float2(-px.x,  px.y)).a);
                neighborA = max(neighborA, tex2D(_MainTex, IN.uv + float2( px.x, -px.y)).a);
                neighborA = max(neighborA, tex2D(_MainTex, IN.uv + float2(-px.x, -px.y)).a);

                float glowMask = saturate((neighborA - baseAlpha) * max(_GlowStrength, 0.0));
                fixed3 rgb = lerp(baseCol.rgb, _GlowColor.rgb, glowMask * _GlowColor.a);
                float a = saturate(baseAlpha + glowMask * _GlowColor.a);

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}
