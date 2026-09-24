using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ergo
{
    /// <summary>
    /// Creates Ergo's materials and feeds the shaders their lighting: ambient, the directional "sun", the
    /// player's point light, linear fog and a directional shadow map, all matching A-Frame 0.7 / three.js r87.
    /// Everything is done through Ergo's own shaders and globals, so it works in any render pipeline.
    /// </summary>
    public sealed class ErgoRenderer : IDisposable
    {
        private const string ShaderFolder = "Ergo/Shaders/";

        // three.js' default DirectionalLight shadow camera as used by the original scene.
        private const float ShadowExtent = 5f;
        private const float ShadowNear = 0.5f;
        private const float ShadowFar = 500f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissiveId = Shader.PropertyToID("_Emissive");
        private static readonly int PhongId = Shader.PropertyToID("_Phong");
        private static readonly int ReceiveShadowsId = Shader.PropertyToID("_ReceiveShadows");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int AmbientId = Shader.PropertyToID("_ErgoAmbient");
        private static readonly int SunDirectionId = Shader.PropertyToID("_ErgoSunDirection");
        private static readonly int SunColorId = Shader.PropertyToID("_ErgoSunColor");
        private static readonly int PointPositionId = Shader.PropertyToID("_ErgoPointPosition");
        private static readonly int PointColorId = Shader.PropertyToID("_ErgoPointColor");
        private static readonly int FogColorId = Shader.PropertyToID("_ErgoFogColor");
        private static readonly int FogParamsId = Shader.PropertyToID("_ErgoFogParams");
        private static readonly int ShadowMapId = Shader.PropertyToID("_ErgoShadowMap");
        private static readonly int ShadowMatrixId = Shader.PropertyToID("_ErgoShadowMatrix");
        private static readonly int WorldToShadowId = Shader.PropertyToID("_ErgoWorldToShadow");
        private static readonly int ShadowParamsId = Shader.PropertyToID("_ErgoShadowParams");

        private readonly Shader litShader;
        private readonly Shader textShader;
        private readonly Shader unlitShader;
        private readonly Shader shadowCasterShader;
        private readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();
        private readonly List<Renderer> shadowCasters = new List<Renderer>();

        private RenderTexture shadowMap;
        private Material shadowCasterMaterial;
        private CommandBuffer shadowCommands;

        /// <summary>Loads the Ergo shaders from Resources.</summary>
        public ErgoRenderer()
        {
            litShader = LoadShader("ErgoLit", "Ergo/Lit");
            textShader = LoadShader("ErgoText", "Ergo/Text");
            unlitShader = LoadShader("ErgoUnlit", "Ergo/Unlit");
            shadowCasterShader = LoadShader("ErgoShadowCaster", "Hidden/Ergo/ShadowCaster");
        }

        /// <summary>True when the shaders needed to draw the scene were found.</summary>
        public bool IsValid
        {
            get { return litShader != null && textShader != null && unlitShader != null; }
        }

        /// <summary>A MeshStandardMaterial look (roughness 0.5, metalness 0) with optional emissive and shadows.</summary>
        public Material CreateStandard(string name, Color albedo, Color emissive, float emissiveIntensity, bool receiveShadows)
        {
            Material material = CreateLit(name, albedo, 1f);
            material.SetVector(EmissiveId, Raw(emissive) * emissiveIntensity);
            material.SetFloat(ReceiveShadowsId, receiveShadows ? 1f : 0f);
            return material;
        }

        /// <summary>A MeshPhongMaterial look (shininess 30, specular #111111), transparent below full opacity.</summary>
        public Material CreatePhong(string name, Color albedo, float opacity)
        {
            Material material = CreateLit(name, albedo, opacity);
            material.SetFloat(PhongId, 1f);
            return material;
        }

        /// <summary>Material for <see cref="WorldText"/> meshes using <paramref name="font"/>'s atlas.</summary>
        public Material CreateText(Font font)
        {
            var material = new Material(textShader) { name = "Ergo Text" };
            if (font != null && font.material != null)
            {
                material.mainTexture = font.material.mainTexture;
            }

            ownedObjects.Add(material);
            return material;
        }

        /// <summary>A-Frame's flat (unlit) shader.</summary>
        public Material CreateUnlit(string name, Color color)
        {
            var material = new Material(unlitShader) { name = name };
            material.SetVector(ColorId, Raw(color));
            ownedObjects.Add(material);
            return material;
        }

        /// <summary>Registers a mesh that casts shadows (the original's <c>shadow</c> component).</summary>
        public void AddShadowCaster(Renderer caster)
        {
            if (caster != null && !shadowCasters.Contains(caster))
            {
                shadowCasters.Add(caster);
            }
        }

        /// <summary>Keeps a runtime-created asset alive with the renderer and destroys it on dispose.</summary>
        public T Own<T>(T asset) where T : UnityEngine.Object
        {
            if (asset != null)
            {
                ownedObjects.Add(asset);
            }

            return asset;
        }

        /// <summary>Uploads the lights and fog for this frame.</summary>
        public void UpdateLighting(ErgoSettings settings, ErgoPointLight pointLight)
        {
            Shader.SetGlobalVector(AmbientId, Raw(settings.ambientColor) * settings.ambientIntensity);
            Shader.SetGlobalVector(SunDirectionId, SunDirection(settings));
            Shader.SetGlobalVector(SunColorId, Raw(settings.sunColor) * settings.sunIntensity);

            if (pointLight != null && pointLight.isActiveAndEnabled)
            {
                Shader.SetGlobalVector(PointPositionId, pointLight.transform.position);
                Shader.SetGlobalVector(PointColorId, Raw(pointLight.color) * pointLight.intensity);
            }
            else
            {
                Shader.SetGlobalVector(PointColorId, Vector4.zero);
            }

            Shader.SetGlobalVector(FogColorId, Raw(settings.skyColor));
            Shader.SetGlobalVector(FogParamsId, new Vector4(settings.fogNear, Mathf.Max(settings.fogFar, settings.fogNear + 0.001f), 1f, 0f));
        }

        /// <summary>
        /// Renders the directional shadow map: an orthographic 10 x 10 m view from the light towards the origin,
        /// the default three.js shadow camera the original relied on (so only the area around the player gets
        /// shadows, as before).
        /// </summary>
        public void RenderShadows(ErgoSettings settings)
        {
            if (!settings.shadows || shadowCasterShader == null)
            {
                Shader.SetGlobalVector(ShadowParamsId, Vector4.zero);
                return;
            }

            int size = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(64, settings.shadowMapSize)), 64, 4096);
            EnsureShadowResources(size);

            Vector3 lightPosition = AFrame.Position(settings.sunPosition);
            Vector3 forward = -lightPosition.normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
            Matrix4x4 lightToWorld = Matrix4x4.TRS(lightPosition, Quaternion.LookRotation(forward, up), Vector3.one);

            // OpenGL-style view (looking down -Z) and projection, as used by Camera.worldToCameraMatrix.
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * lightToWorld.inverse;
            Matrix4x4 projection = Matrix4x4.Ortho(-ShadowExtent, ShadowExtent, -ShadowExtent, ShadowExtent, ShadowNear, ShadowFar);
            Matrix4x4 toTexture = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f));
            Matrix4x4 worldToShadow = toTexture * projection * view;
            Matrix4x4 shadowMatrix = GL.GetGPUProjectionMatrix(projection, true) * view;

            Shader.SetGlobalMatrix(ShadowMatrixId, shadowMatrix);
            Shader.SetGlobalMatrix(WorldToShadowId, worldToShadow);
            Shader.SetGlobalVector(SunDirectionId, SunDirection(settings));

            shadowCommands.Clear();
            shadowCommands.SetRenderTarget(shadowMap);
            shadowCommands.ClearRenderTarget(true, true, Color.white);
            for (int i = 0; i < shadowCasters.Count; i++)
            {
                Renderer caster = shadowCasters[i];
                if (caster != null && caster.enabled && caster.gameObject.activeInHierarchy)
                {
                    shadowCommands.DrawRenderer(caster, shadowCasterMaterial, 0, 0);
                }
            }

            Graphics.ExecuteCommandBuffer(shadowCommands);

            Shader.SetGlobalTexture(ShadowMapId, shadowMap);
            Shader.SetGlobalVector(ShadowParamsId, new Vector4(1f / size, 0f, 1f, 0f));
        }

        /// <summary>Destroys every material, mesh and render target created at runtime.</summary>
        public void Dispose()
        {
            for (int i = 0; i < ownedObjects.Count; i++)
            {
                DestroyObject(ownedObjects[i]);
            }

            ownedObjects.Clear();
            shadowCasters.Clear();
            ReleaseShadowResources();
            Shader.SetGlobalVector(ShadowParamsId, Vector4.zero);
        }

        private Material CreateLit(string name, Color albedo, float opacity)
        {
            var material = new Material(litShader) { name = name };
            material.SetVector(ColorId, new Vector4(albedo.r, albedo.g, albedo.b, opacity));
            if (opacity < 1f)
            {
                // three.js keeps depth writes on for transparent materials.
                material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
                material.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat(ZWriteId, 1f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            ownedObjects.Add(material);
            return material;
        }

        private void EnsureShadowResources(int size)
        {
            if (shadowMap != null && shadowMap.width != size)
            {
                ReleaseShadowResources();
            }

            if (shadowMap == null)
            {
                shadowMap = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "Ergo Shadow Map",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false,
                };
                shadowMap.Create();
            }
            else if (!shadowMap.IsCreated())
            {
                // Render textures can be lost, e.g. when a mobile app is suspended.
                shadowMap.Create();
            }

            if (shadowCasterMaterial == null)
            {
                shadowCasterMaterial = new Material(shadowCasterShader) { name = "Ergo Shadow Caster" };
            }

            if (shadowCommands == null)
            {
                shadowCommands = new CommandBuffer { name = "Ergo Shadow Map" };
            }
        }

        private void ReleaseShadowResources()
        {
            if (shadowMap != null)
            {
                shadowMap.Release();
                DestroyObject(shadowMap);
                shadowMap = null;
            }

            if (shadowCasterMaterial != null)
            {
                DestroyObject(shadowCasterMaterial);
                shadowCasterMaterial = null;
            }

            if (shadowCommands != null)
            {
                shadowCommands.Release();
                shadowCommands = null;
            }
        }

        private static Vector4 SunDirection(ErgoSettings settings)
        {
            // The directional light shines from its position towards the origin (three.js' default target).
            Vector3 toLight = AFrame.Position(settings.sunPosition);
            if (toLight.sqrMagnitude < 1e-6f)
            {
                toLight = Vector3.up;
            }

            return toLight.normalized;
        }

        // Colours go to the shaders exactly as authored; the shaders do their own colour-space handling.
        private static Vector4 Raw(Color color)
        {
            return new Vector4(color.r, color.g, color.b, color.a);
        }

        private static Shader LoadShader(string resourceName, string shaderName)
        {
            var shader = Resources.Load<Shader>(ShaderFolder + resourceName);
            if (shader == null)
            {
                shader = Shader.Find(shaderName);
            }

            if (shader == null)
            {
                Debug.LogError("Ergo: shader '" + shaderName + "' is missing (expected in Resources/" + ShaderFolder + ").");
            }

            return shader;
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
