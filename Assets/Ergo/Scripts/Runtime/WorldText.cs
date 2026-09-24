using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ergo
{
    /// <summary>
    /// World-space text laid out like A-Frame 0.7's <c>text</c> component: <c>width</c> is the width of the
    /// block in local units, sized so that <c>wrapCount</c> + 0.5 digits fill it, lines wrap at that width and
    /// the block is centred (anchor, align and baseline all "center", as in Ergo's mixins).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WorldText : MonoBehaviour
    {
        private const string Digits = "0123456789";

        [SerializeField, TextArea] private string text = string.Empty;
        [SerializeField] private float width = 1f;
        [SerializeField] private int wrapCount = 40;
        [SerializeField] private Color color = Color.white;
        [SerializeField, Range(0f, 1f)] private float opacity = 1f;
        [SerializeField] private int glyphSize = 64;

        private Font font;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private bool dirty = true;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<string> lines = new List<string>();
        private readonly StringBuilder lineBuilder = new StringBuilder();

        /// <summary>Displayed text (the <c>value</c> attribute).</summary>
        public string Text
        {
            get { return text; }
            set
            {
                string newText = value ?? string.Empty;
                if (newText == text)
                {
                    return;
                }

                text = newText;
                dirty = true;
            }
        }

        /// <summary>Sets up the text; <paramref name="textMaterial"/> must use the font's texture.</summary>
        public void Initialize(Font textFont, Material textMaterial, string value, float blockWidth, Color textColor, float textOpacity, int pixelSize)
        {
            font = textFont;
            text = value ?? string.Empty;
            width = blockWidth;
            color = textColor;
            opacity = textOpacity;
            glyphSize = Mathf.Max(8, pixelSize);

            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = textMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            if (mesh == null)
            {
                mesh = new Mesh { name = "Text" };
                mesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            dirty = true;
            Rebuild();
        }

        private void OnEnable()
        {
            Font.textureRebuilt += OnFontTextureRebuilt;
            dirty = true;
        }

        private void OnDisable()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        private void Update()
        {
            // Unity keeps a dynamic font's glyphs only while they are requested, so ask every frame (all texts do
            // this before any of them rebuild in LateUpdate).
            RequestGlyphs();
        }

        private void LateUpdate()
        {
            if (dirty)
            {
                Rebuild();
            }
        }

        private void RequestGlyphs()
        {
            if (font == null)
            {
                return;
            }

            font.RequestCharactersInTexture(Digits, glyphSize, FontStyle.Normal);
            if (text.Length > 0)
            {
                font.RequestCharactersInTexture(text, glyphSize, FontStyle.Normal);
            }
        }

        private void OnFontTextureRebuilt(Font rebuiltFont)
        {
            // The atlas moved every glyph; rebuild on the next LateUpdate rather than inside the callback.
            if (rebuiltFont == font)
            {
                dirty = true;
            }
        }

        private void Rebuild()
        {
            if (font == null || mesh == null)
            {
                return;
            }

            RequestGlyphs();
            dirty = false;

            // A rebuilt atlas can be a new texture, so keep the (shared) text material pointing at it.
            Material material = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (material != null && font.material != null && material.mainTexture != font.material.mainTexture)
            {
                material.mainTexture = font.material.mainTexture;
            }

            // A-Frame sizes text from the average advance of the digits ("widthFactor").
            float digitAdvance = 0f;
            CharacterInfo info;
            for (int i = 0; i < Digits.Length; i++)
            {
                if (font.GetCharacterInfo(Digits[i], out info, glyphSize, FontStyle.Normal))
                {
                    digitAdvance += info.advance;
                }
            }

            digitAdvance = Mathf.Max(1f, digitAdvance / Digits.Length);
            float wrapPixels = (0.5f + wrapCount) * digitAdvance;
            float scale = width / wrapPixels;

            WrapLines(wrapPixels);

            float lineHeight = font.fontSize > 0 ? font.lineHeight * (float)glyphSize / font.fontSize : glyphSize * 1.25f;
            float ascent = font.fontSize > 0 ? font.ascent * (float)glyphSize / font.fontSize : glyphSize;
            if (lineHeight <= 0f)
            {
                lineHeight = glyphSize * 1.25f;
            }

            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();

            var vertexColor = (Color32)new Color(color.r, color.g, color.b, color.a * opacity);
            float blockHeight = lines.Count * lineHeight;
            for (int l = 0; l < lines.Count; l++)
            {
                string line = lines[l];
                float lineWidth = MeasureLine(line);
                float penX = -lineWidth / 2f;
                float baseline = blockHeight / 2f - ascent - l * lineHeight;
                for (int c = 0; c < line.Length; c++)
                {
                    if (!font.GetCharacterInfo(line[c], out info, glyphSize, FontStyle.Normal))
                    {
                        continue;
                    }

                    if (info.maxX > info.minX && info.maxY > info.minY)
                    {
                        AddGlyph(info, penX, baseline, scale, vertexColor);
                    }

                    penX += info.advance;
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        private void AddGlyph(CharacterInfo info, float penX, float baseline, float scale, Color32 vertexColor)
        {
            float x0 = (penX + info.minX) * scale;
            float x1 = (penX + info.maxX) * scale;
            float y0 = (baseline + info.minY) * scale;
            float y1 = (baseline + info.maxY) * scale;

            // Wound so the glyph faces -Z, i.e. reads correctly for a camera looking down +Z.
            int start = vertices.Count;
            vertices.Add(new Vector3(x0, y0, 0f));
            vertices.Add(new Vector3(x0, y1, 0f));
            vertices.Add(new Vector3(x1, y1, 0f));
            vertices.Add(new Vector3(x1, y0, 0f));
            uvs.Add(info.uvBottomLeft);
            uvs.Add(info.uvTopLeft);
            uvs.Add(info.uvTopRight);
            uvs.Add(info.uvBottomRight);
            for (int i = 0; i < 4; i++)
            {
                colors.Add(vertexColor);
            }

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        // Greedy word wrap at the block width, like layout-bmfont-text in "normal" white-space mode.
        private void WrapLines(float wrapPixels)
        {
            lines.Clear();
            string[] paragraphs = text.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                string[] words = paragraphs[p].Split(' ');
                lineBuilder.Length = 0;
                for (int w = 0; w < words.Length; w++)
                {
                    if (words[w].Length == 0)
                    {
                        continue;
                    }

                    string candidate = lineBuilder.Length == 0 ? words[w] : lineBuilder + " " + words[w];
                    if (lineBuilder.Length > 0 && MeasureLine(candidate) > wrapPixels)
                    {
                        lines.Add(lineBuilder.ToString());
                        lineBuilder.Length = 0;
                        lineBuilder.Append(words[w]);
                    }
                    else
                    {
                        lineBuilder.Length = 0;
                        lineBuilder.Append(candidate);
                    }
                }

                lines.Add(lineBuilder.ToString());
            }
        }

        private float MeasureLine(string line)
        {
            float total = 0f;
            CharacterInfo info;
            for (int i = 0; i < line.Length; i++)
            {
                if (font.GetCharacterInfo(line[i], out info, glyphSize, FontStyle.Normal))
                {
                    total += info.advance;
                }
            }

            return total;
        }
    }
}
