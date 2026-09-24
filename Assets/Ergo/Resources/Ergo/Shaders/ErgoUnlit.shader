// A-Frame's "flat" shader: a single unlit colour (used by the gaze cursor ring).
Shader "Ergo/Unlit"
{
    Properties
    {
        _Color ("Colour (sRGB)", Vector) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 color = saturate(_Color.rgb);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                color = GammaToLinearSpace(color);
                #endif
                return float4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
