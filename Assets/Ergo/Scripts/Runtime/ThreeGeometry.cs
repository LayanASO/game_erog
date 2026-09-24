using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ergo
{
    /// <summary>
    /// Triangle mesh in three.js space (right-handed, counter-clockwise front faces), built with the same
    /// vertex order as the three.js r87 geometry classes that A-Frame 0.7 used.
    /// </summary>
    public sealed class ThreeMesh
    {
        /// <summary>Vertex positions.</summary>
        public readonly List<Vector3> Vertices = new List<Vector3>();

        /// <summary>Triangle indices, three per face.</summary>
        public readonly List<int> Triangles = new List<int>();

        /// <summary>Optional per-vertex normals for smooth shading; flat shading ignores them.</summary>
        public List<Vector3> Normals;

        /// <summary>Number of triangles.</summary>
        public int FaceCount
        {
            get { return Triangles.Count / 3; }
        }
    }

    /// <summary>
    /// Geometry builders ported from three.js r87 and aframe-low-poly 0.0.2, plus conversion to Unity meshes.
    /// </summary>
    public static class ThreeGeometry
    {
        private struct Double3
        {
            public double X;
            public double Y;
            public double Z;

            public Double3(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        /// <summary>
        /// THREE.CylinderGeometry (closed, full circle) followed by Geometry.mergeVertices(), which is what both
        /// A-Frame's cone primitive and aframe-low-poly's <c>lp-cone</c> produce.
        /// </summary>
        public static ThreeMesh Cylinder(float radiusTop, float radiusBottom, float height, int radialSegments, int heightSegments)
        {
            radialSegments = Math.Max(3, radialSegments);
            heightSegments = Math.Max(1, heightSegments);

            var raw = new List<Double3>();
            var faces = new List<int>();
            double halfHeight = height / 2.0;
            double thetaLength = Math.PI * 2.0;

            // Torso: rows from top (v = 0) to bottom (v = 1), each row wrapping around the full circle.
            var rows = new int[heightSegments + 1][];
            for (int y = 0; y <= heightSegments; y++)
            {
                var row = new int[radialSegments + 1];
                double v = (double)y / heightSegments;
                double radius = v * (radiusBottom - radiusTop) + radiusTop;
                for (int x = 0; x <= radialSegments; x++)
                {
                    double theta = (double)x / radialSegments * thetaLength;
                    row[x] = raw.Count;
                    raw.Add(new Double3(radius * Math.Sin(theta), -v * height + halfHeight, radius * Math.Cos(theta)));
                }

                rows[y] = row;
            }

            for (int x = 0; x < radialSegments; x++)
            {
                for (int y = 0; y < heightSegments; y++)
                {
                    int a = rows[y][x];
                    int b = rows[y + 1][x];
                    int c = rows[y + 1][x + 1];
                    int d = rows[y][x + 1];
                    AddFace(faces, a, b, d);
                    AddFace(faces, b, c, d);
                }
            }

            if (radiusTop > 0f)
            {
                AddCap(raw, faces, true, radiusTop, halfHeight, radialSegments, thetaLength);
            }

            if (radiusBottom > 0f)
            {
                AddCap(raw, faces, false, radiusBottom, halfHeight, radialSegments, thetaLength);
            }

            return MergeVertices(raw, faces);
        }

        /// <summary>
        /// aframe-low-poly 0.0.2 vertex randomisation: every coordinate of every (merged) vertex is displaced by
        /// <c>sin(angle) * (amplitude + random * variance)</c>, drawing from the seeded generator in order.
        /// </summary>
        public static void Jitter(ThreeMesh mesh, float amplitude, float amplitudeVariance, string seed)
        {
            var random = new LowPolyRandom(string.IsNullOrEmpty(seed) ? LowPolyRandom.DefaultSeed : seed);
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                Vector3 vertex = mesh.Vertices[i];
                vertex.x = (float)(vertex.x + Displacement(random, amplitude, amplitudeVariance));
                vertex.y = (float)(vertex.y + Displacement(random, amplitude, amplitudeVariance));
                vertex.z = (float)(vertex.z + Displacement(random, amplitude, amplitudeVariance));
                mesh.Vertices[i] = vertex;
            }
        }

        /// <summary>THREE.BoxGeometry with one segment per side.</summary>
        public static ThreeMesh Box(float width, float height, float depth)
        {
            var mesh = new ThreeMesh();
            var half = new Vector3(width / 2f, height / 2f, depth / 2f);
            AddBoxFace(mesh, half, Vector3.right, Vector3.back, Vector3.up);
            AddBoxFace(mesh, half, Vector3.left, Vector3.forward, Vector3.up);
            AddBoxFace(mesh, half, Vector3.up, Vector3.right, Vector3.back);
            AddBoxFace(mesh, half, Vector3.down, Vector3.right, Vector3.forward);
            AddBoxFace(mesh, half, Vector3.forward, Vector3.right, Vector3.up);
            AddBoxFace(mesh, half, Vector3.back, Vector3.left, Vector3.up);
            return mesh;
        }

        /// <summary>THREE.SphereGeometry (full sphere) with smooth normals.</summary>
        public static ThreeMesh Sphere(float radius, int widthSegments, int heightSegments)
        {
            widthSegments = Math.Max(3, widthSegments);
            heightSegments = Math.Max(2, heightSegments);

            var mesh = new ThreeMesh { Normals = new List<Vector3>() };
            var grid = new int[heightSegments + 1][];
            for (int iy = 0; iy <= heightSegments; iy++)
            {
                var row = new int[widthSegments + 1];
                double v = (double)iy / heightSegments;
                for (int ix = 0; ix <= widthSegments; ix++)
                {
                    double u = (double)ix / widthSegments;
                    double phi = u * Math.PI * 2.0;
                    double theta = v * Math.PI;
                    var normal = new Vector3(
                        (float)(-Math.Cos(phi) * Math.Sin(theta)),
                        (float)Math.Cos(theta),
                        (float)(Math.Sin(phi) * Math.Sin(theta)));
                    row[ix] = mesh.Vertices.Count;
                    mesh.Vertices.Add(normal * radius);
                    mesh.Normals.Add(normal);
                }

                grid[iy] = row;
            }

            for (int iy = 0; iy < heightSegments; iy++)
            {
                for (int ix = 0; ix < widthSegments; ix++)
                {
                    int a = grid[iy][ix + 1];
                    int b = grid[iy][ix];
                    int c = grid[iy + 1][ix];
                    int d = grid[iy + 1][ix + 1];
                    if (iy != 0)
                    {
                        AddFace(mesh.Triangles, a, b, d);
                    }

                    if (iy != heightSegments - 1)
                    {
                        AddFace(mesh.Triangles, b, c, d);
                    }
                }
            }

            return mesh;
        }

        /// <summary>THREE.RingGeometry (flat annulus in the XY plane facing +Z).</summary>
        public static ThreeMesh Ring(float innerRadius, float outerRadius, int thetaSegments, int phiSegments)
        {
            thetaSegments = Math.Max(3, thetaSegments);
            phiSegments = Math.Max(1, phiSegments);

            var mesh = new ThreeMesh();
            double radiusStep = (outerRadius - innerRadius) / (double)phiSegments;
            double radius = innerRadius;
            for (int j = 0; j <= phiSegments; j++)
            {
                for (int i = 0; i <= thetaSegments; i++)
                {
                    double segment = (double)i / thetaSegments * Math.PI * 2.0;
                    mesh.Vertices.Add(new Vector3((float)(radius * Math.Cos(segment)), (float)(radius * Math.Sin(segment)), 0f));
                }

                radius += radiusStep;
            }

            for (int j = 0; j < phiSegments; j++)
            {
                int level = j * (thetaSegments + 1);
                for (int i = 0; i < thetaSegments; i++)
                {
                    int a = i + level;
                    int b = a + thetaSegments + 1;
                    int c = a + thetaSegments + 2;
                    int d = a + 1;
                    AddFace(mesh.Triangles, a, b, d);
                    AddFace(mesh.Triangles, b, c, d);
                }
            }

            return mesh;
        }

        /// <summary>
        /// Converts a three.js mesh to a Unity mesh: mirrors Z and reverses the winding so the geometry looks
        /// the same from the mirrored camera. Flat shading splits vertices per face, like three.js' flatShading.
        /// </summary>
        public static Mesh ToUnityMesh(ThreeMesh source, bool flatShading, string name)
        {
            var mesh = new Mesh { name = name };
            int faceCount = source.FaceCount;

            if (flatShading || source.Normals == null)
            {
                var vertices = new Vector3[faceCount * 3];
                var normals = new Vector3[faceCount * 3];
                var triangles = new int[faceCount * 3];
                for (int f = 0; f < faceCount; f++)
                {
                    Vector3 a = Mirror(source.Vertices[source.Triangles[f * 3]]);
                    Vector3 b = Mirror(source.Vertices[source.Triangles[f * 3 + 2]]);
                    Vector3 c = Mirror(source.Vertices[source.Triangles[f * 3 + 1]]);
                    Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                    int i = f * 3;
                    vertices[i] = a;
                    vertices[i + 1] = b;
                    vertices[i + 2] = c;
                    normals[i] = normal;
                    normals[i + 1] = normal;
                    normals[i + 2] = normal;
                    triangles[i] = i;
                    triangles[i + 1] = i + 1;
                    triangles[i + 2] = i + 2;
                }

                mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.triangles = triangles;
            }
            else
            {
                var vertices = new Vector3[source.Vertices.Count];
                var normals = new Vector3[source.Vertices.Count];
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = Mirror(source.Vertices[i]);
                    normals[i] = Mirror(source.Normals[i]);
                }

                var triangles = new int[faceCount * 3];
                for (int f = 0; f < faceCount; f++)
                {
                    triangles[f * 3] = source.Triangles[f * 3];
                    triangles[f * 3 + 1] = source.Triangles[f * 3 + 2];
                    triangles[f * 3 + 2] = source.Triangles[f * 3 + 1];
                }

                mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.triangles = triangles;
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Mirror(Vector3 threePoint)
        {
            return new Vector3(threePoint.x, threePoint.y, -threePoint.z);
        }

        private static double Displacement(LowPolyRandom random, double amplitude, double amplitudeVariance)
        {
            double angle = random.NextDouble() * Math.PI * 2.0;
            double amount = amplitude + random.NextDouble() * amplitudeVariance;
            return Math.Sin(angle) * amount;
        }

        private static void AddFace(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        private static void AddCap(List<Double3> raw, List<int> faces, bool top, double radius, double halfHeight, int radialSegments, double thetaLength)
        {
            double sign = top ? 1.0 : -1.0;
            int centerStart = raw.Count;
            for (int x = 1; x <= radialSegments; x++)
            {
                raw.Add(new Double3(0.0, halfHeight * sign, 0.0));
            }

            int centerEnd = raw.Count;
            for (int x = 0; x <= radialSegments; x++)
            {
                double theta = (double)x / radialSegments * thetaLength;
                raw.Add(new Double3(radius * Math.Sin(theta), halfHeight * sign, radius * Math.Cos(theta)));
            }

            for (int x = 0; x < radialSegments; x++)
            {
                int c = centerStart + x;
                int i = centerEnd + x;
                if (top)
                {
                    AddFace(faces, i, i + 1, c);
                }
                else
                {
                    AddFace(faces, i + 1, i, c);
                }
            }
        }

        // Geometry.mergeVertices(): positions are keyed at 4 decimal places, the first occurrence wins and
        // faces that collapse onto a repeated vertex are dropped.
        private static ThreeMesh MergeVertices(List<Double3> raw, List<int> faces)
        {
            var mesh = new ThreeMesh();
            var unique = new Dictionary<ValueTuple<long, long, long>, int>();
            var remap = new int[raw.Count];
            for (int i = 0; i < raw.Count; i++)
            {
                Double3 p = raw[i];
                var key = new ValueTuple<long, long, long>(RoundKey(p.X), RoundKey(p.Y), RoundKey(p.Z));
                int index;
                if (!unique.TryGetValue(key, out index))
                {
                    index = mesh.Vertices.Count;
                    unique.Add(key, index);
                    mesh.Vertices.Add(new Vector3((float)p.X, (float)p.Y, (float)p.Z));
                }

                remap[i] = index;
            }

            for (int f = 0; f < faces.Count; f += 3)
            {
                int a = remap[faces[f]];
                int b = remap[faces[f + 1]];
                int c = remap[faces[f + 2]];
                if (a == b || b == c || c == a)
                {
                    continue;
                }

                AddFace(mesh.Triangles, a, b, c);
            }

            return mesh;
        }

        // JavaScript's Math.round(value * 1e4): rounds half-way cases towards +infinity.
        private static long RoundKey(double value)
        {
            return (long)Math.Floor(value * 10000.0 + 0.5);
        }

        private static void AddBoxFace(ThreeMesh mesh, Vector3 half, Vector3 normal, Vector3 uAxis, Vector3 vAxis)
        {
            Vector3 center = Vector3.Scale(normal, half);
            Vector3 u = Vector3.Scale(uAxis, half);
            Vector3 v = Vector3.Scale(vAxis, half);
            int start = mesh.Vertices.Count;
            mesh.Vertices.Add(center - u - v);
            mesh.Vertices.Add(center + u - v);
            mesh.Vertices.Add(center + u + v);
            mesh.Vertices.Add(center - u + v);
            AddFace(mesh.Triangles, start, start + 1, start + 2);
            AddFace(mesh.Triangles, start, start + 2, start + 3);
        }
    }
}
