// Lighting as A-Frame 0.7 rendered it with three.js r87 (MeshStandardMaterial or MeshPhongMaterial with flat
// shading): ambient light, one directional light with a 3x3 PCF shadow map, and one point light with no falloff
// (distance 0). The maths runs on the authored sRGB values because A-Frame 0.7 had no gamma handling, and the
// result is faded into the fog with smoothstep, like three.js' linear fog.
//
// Only Ergo's own global uniforms are used (set by ErgoRenderer), so the shader looks the same in the built-in
// render pipeline and in URP.
Shader "Ergo/Lit"
{
    Properties
    {
        _Color ("Colour (sRGB) and opacity", Vector) = (1, 1, 1, 1)
        _Emissive ("Emissive (sRGB x intensity)", Vector) = (0, 0, 0, 0)
        _Phong ("Phong model (0 = standard)", Float) = 0
        _Roughness ("Roughness (standard model)", Float) = 0.5
        _Shininess ("Shininess (Phong model)", Float) = 30
        _Specular ("Specular colour (Phong model)", Vector) = (0.0666667, 0.0666667, 0.0666667, 1)
        _ReceiveShadows ("Receive shadows", Float) = 0
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Color;
            float4 _Emissive;
            float _Phong;
            float _Roughness;
            float _Shininess;
            float4 _Specular;
            float _ReceiveShadows;

            float4 _ErgoAmbient;          // rgb: ambient colour x intensity
            float4 _ErgoSunDirection;     // xyz: unit vector pointing at the directional light
            float4 _ErgoSunColor;         // rgb: directional colour x intensity
            float4 _ErgoPointPosition;    // xyz: world position of the point light
            float4 _ErgoPointColor;       // rgb: point colour x intensity
            float4 _ErgoFogColor;         // rgb: fog colour
            float4 _ErgoFogParams;        // x: near, y: far, z: 1 when fog is on
            sampler2D _ErgoShadowMap;     // light-space depth packed into RG
            float4x4 _ErgoWorldToShadow;  // world position -> shadow map uv (xy) and depth (z), all in [0, 1]
            float4 _ErgoShadowParams;     // x: texel size, y: depth bias, z: 1 when shadows are on

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float viewDepth : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
                o.pos = mul(UNITY_MATRIX_VP, world);
                o.worldPos = world.xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDepth = -mul(UNITY_MATRIX_V, world).z;
                return o;
            }

            // three.js F_Schlick (spherical Gaussian approximation).
            float3 FresnelSchlick(float3 specularColor, float dotLH)
            {
                float fresnel = exp2((-5.55473 * dotLH - 6.98316) * dotLH);
                return (1.0 - specularColor) * fresnel + specularColor;
            }

            // RE_Direct_Physical (standard, metalness 0, GGX) or RE_Direct_BlinnPhong, without physically
            // correct lights: the irradiance is scaled by PI and the Lambert term divides it back out.
            float3 DirectLight(float3 n, float3 v, float3 l, float3 lightColor, float3 albedo)
            {
                float dotNL = saturate(dot(n, l));
                float3 irradiance = dotNL * lightColor;
                float3 halfVector = l + v;
                float3 h = halfVector * rsqrt(max(dot(halfVector, halfVector), 0.00000001));
                float dotNH = saturate(dot(n, h));
                float dotLH = saturate(dot(l, h));

                float3 brdf;
                if (_Phong > 0.5)
                {
                    float3 f = FresnelSchlick(_Specular.rgb, dotLH);
                    float d = (_Shininess * 0.5 + 1.0) * pow(max(dotNH, 0.0001), _Shininess) / UNITY_PI;
                    brdf = f * (0.25 * d);
                }
                else
                {
                    float dotNV = saturate(dot(n, v));
                    float alpha = _Roughness * _Roughness;
                    float a2 = alpha * alpha;
                    float3 f = FresnelSchlick(float3(0.04, 0.04, 0.04), dotLH);
                    float gv = dotNL * sqrt(a2 + (1.0 - a2) * dotNV * dotNV);
                    float gl = dotNV * sqrt(a2 + (1.0 - a2) * dotNL * dotNL);
                    float g = 0.5 / max(gv + gl, 0.000001);
                    float denom = dotNH * dotNH * (a2 - 1.0) + 1.0;
                    float d = a2 / (UNITY_PI * denom * denom);
                    brdf = f * (g * d);
                }

                return irradiance * albedo + UNITY_PI * irradiance * brdf;
            }

            float ShadowTap(float2 uv, float depth)
            {
                float stored = DecodeFloatRG(tex2Dlod(_ErgoShadowMap, float4(uv, 0.0, 0.0)).rg);
                return depth <= stored ? 1.0 : 0.0;
            }

            // three.js PCFShadowMap: nine comparisons one texel apart. Outside the shadow camera counts as lit.
            float Shadow(float3 worldPos)
            {
                if (_ErgoShadowParams.z < 0.5 || _ReceiveShadows < 0.5)
                {
                    return 1.0;
                }

                float4 coord = mul(_ErgoWorldToShadow, float4(worldPos, 1.0));
                float3 c = coord.xyz / coord.w;
                c.z += _ErgoShadowParams.y;
                if (c.x < 0.0 || c.x > 1.0 || c.y < 0.0 || c.y > 1.0 || c.z > 1.0)
                {
                    return 1.0;
                }

                float t = _ErgoShadowParams.x;
                float sum = ShadowTap(c.xy + float2(-t, -t), c.z)
                          + ShadowTap(c.xy + float2(0.0, -t), c.z)
                          + ShadowTap(c.xy + float2(t, -t), c.z)
                          + ShadowTap(c.xy + float2(-t, 0.0), c.z)
                          + ShadowTap(c.xy, c.z)
                          + ShadowTap(c.xy + float2(t, 0.0), c.z)
                          + ShadowTap(c.xy + float2(-t, t), c.z)
                          + ShadowTap(c.xy + float2(0.0, t), c.z)
                          + ShadowTap(c.xy + float2(t, t), c.z);
                return sum / 9.0;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 albedo = _Color.rgb;

                float3 color = _ErgoAmbient.rgb * albedo;
                color += Shadow(i.worldPos) * DirectLight(n, v, _ErgoSunDirection.xyz, _ErgoSunColor.rgb, albedo);

                float3 toPoint = _ErgoPointPosition.xyz - i.worldPos;
                float3 pointDirection = toPoint * rsqrt(max(dot(toPoint, toPoint), 0.00000001));
                color += DirectLight(n, v, pointDirection, _ErgoPointColor.rgb, albedo);
                color += _Emissive.rgb;

                float fog = _ErgoFogParams.z * smoothstep(_ErgoFogParams.x, _ErgoFogParams.y, i.viewDepth);
                color = saturate(lerp(color, _ErgoFogColor.rgb, fog));

                #if !defined(UNITY_COLORSPACE_GAMMA)
                color = GammaToLinearSpace(color);
                #endif
                return float4(color, _Color.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
