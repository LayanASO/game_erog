using UnityEngine;
using UnityEngine.Rendering;

namespace Ergo
{
    /// <summary>
    /// Port of the flat-shaded <c>a-ocean</c> primitive (Don McCurdy's aframe-extras, as bundled with Ergo): a
    /// grid whose vertices bob up and down on independent sine waves.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class OceanSurface : MonoBehaviour
    {
        private Mesh mesh;
        private Vector3[] gridPositions;
        private float[] waveAngle;
        private float[] waveAmplitude;
        private float[] waveSpeed;
        private float[] waveHeight;
        private int[] cornerGridIndex;
        private Vector3[] vertices;
        private Vector3[] normals;

        /// <summary>
        /// Builds the grid. Parameters match the a-ocean attributes: <c>width</c>, <c>depth</c>, <c>density</c>,
        /// <c>amplitude</c>, <c>amplitude-variance</c>, <c>speed</c> (radians per second) and <c>speed-variance</c>.
        /// </summary>
        public void Build(float width, float depth, int density, float amplitude, float amplitudeVariance, float speed, float speedVariance, System.Random random)
        {
            density = Mathf.Max(1, density);
            int columns = density + 1;
            int gridCount = columns * columns;
            gridPositions = new Vector3[gridCount];
            waveAngle = new float[gridCount];
            waveAmplitude = new float[gridCount];
            waveSpeed = new float[gridCount];
            waveHeight = new float[gridCount];

            float cellWidth = width / density;
            float cellDepth = depth / density;
            for (int iy = 0; iy < columns; iy++)
            {
                for (int ix = 0; ix < columns; ix++)
                {
                    int index = iy * columns + ix;

                    // THREE.PlaneGeometry lies in XY; the primitive's rotation="-90 0 0" lays it flat, so its
                    // row coordinate ends up on Unity's Z axis.
                    gridPositions[index] = new Vector3(ix * cellWidth - width / 2f, 0f, -(iy * cellDepth - depth / 2f));
                    waveAngle[index] = (float)(random.NextDouble() * Mathf.PI * 2f);
                    waveAmplitude[index] = amplitude + (float)random.NextDouble() * amplitudeVariance;

                    // ocean.js stores radians per millisecond; keep radians per second.
                    waveSpeed[index] = speed + (float)random.NextDouble() * speedVariance;
                }
            }

            // Two triangles per cell, split along the same diagonal as three.js, one vertex per corner so every
            // facet gets its own normal (flat shading).
            int cells = density * density;
            cornerGridIndex = new int[cells * 6];
            int corner = 0;
            for (int iy = 0; iy < density; iy++)
            {
                for (int ix = 0; ix < density; ix++)
                {
                    int a = iy * columns + ix;
                    int b = (iy + 1) * columns + ix;
                    int c = (iy + 1) * columns + ix + 1;
                    int d = iy * columns + ix + 1;

                    // three.js faces (a, b, d) and (b, c, d), wound for Unity.
                    cornerGridIndex[corner++] = a;
                    cornerGridIndex[corner++] = d;
                    cornerGridIndex[corner++] = b;
                    cornerGridIndex[corner++] = b;
                    cornerGridIndex[corner++] = d;
                    cornerGridIndex[corner++] = c;
                }
            }

            vertices = new Vector3[cornerGridIndex.Length];
            normals = new Vector3[cornerGridIndex.Length];
            var triangles = new int[cornerGridIndex.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = i;
            }

            mesh = new Mesh { name = "Ocean" };
            mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.MarkDynamic();
            UpdateMesh(0f);
            mesh.triangles = triangles;

            float maxHeight = amplitude + amplitudeVariance + 0.01f;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(width, maxHeight * 2f, depth));
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void Update()
        {
            if (mesh == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            UpdateMesh(deltaTime);
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        private void UpdateMesh(float deltaTime)
        {
            // ocean.js: z = z0 + sin(angle) * amplitude, then angle += speed * dt.
            for (int i = 0; i < waveHeight.Length; i++)
            {
                waveHeight[i] = Mathf.Sin(waveAngle[i]) * waveAmplitude[i];
                waveAngle[i] = Mathf.Repeat(waveAngle[i] + waveSpeed[i] * deltaTime, Mathf.PI * 2f);
            }

            for (int i = 0; i < vertices.Length; i += 3)
            {
                Vector3 p0 = Corner(cornerGridIndex[i]);
                Vector3 p1 = Corner(cornerGridIndex[i + 1]);
                Vector3 p2 = Corner(cornerGridIndex[i + 2]);
                Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                vertices[i] = p0;
                vertices[i + 1] = p1;
                vertices[i + 2] = p2;
                normals[i] = normal;
                normals[i + 1] = normal;
                normals[i + 2] = normal;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
        }

        private Vector3 Corner(int gridIndex)
        {
            Vector3 position = gridPositions[gridIndex];
            position.y = waveHeight[gridIndex];
            return position;
        }
    }
}
