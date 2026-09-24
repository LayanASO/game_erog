// World-space text drawn from a dynamic font atlas (see WorldText.cs). Vertex colours carry the authored sRGB
// colour and the text opacity. Like A-Frame's SDF text shader, it is not affected by fog.
Shader "Ergo/Text"
{
    Properties
    {
        _MainTex ("Font texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 color = i.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                color = GammaToLinearSpace(color);
                #endif
                return float4(color, i.color.a * tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }

    Fallback Off
}
