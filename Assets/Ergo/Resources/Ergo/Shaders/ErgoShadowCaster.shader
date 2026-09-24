// Renders Ergo's directional shadow map (see ErgoRenderer.RenderShadows). Light-space depth is packed into the
// red and green channels of an ordinary ARGB32 target, as three.js packed depth on WebGL 1. Only faces turned
// away from the light are drawn, like three.js' renderReverseSided, which keeps lit surfaces free of acne. The
// facing test uses normals instead of hardware culling so it does not depend on the platform's winding rules.
Shader "Hidden/Ergo/ShadowCaster"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4x4 _ErgoShadowMatrix;   // world -> light clip space, in the GPU's conventions
            float4x4 _ErgoWorldToShadow;  // world -> shadow map uv (xy) and depth (z)
            float4 _ErgoSunDirection;     // xyz: unit vector pointing at the light

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float depth : TEXCOORD0;
                float facing : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
                o.pos = mul(_ErgoShadowMatrix, world);
                o.depth = mul(_ErgoWorldToShadow, world).z;
                o.facing = dot(UnityObjectToWorldNormal(v.normal), _ErgoSunDirection.xyz);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                clip(-i.facing);
                return float4(EncodeFloatRG(clamp(i.depth, 0.0, 0.9999)), 0.0, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
