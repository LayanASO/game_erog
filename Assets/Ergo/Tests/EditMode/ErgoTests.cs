using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Ergo.Tests
{
    /// <summary>
    /// Checks the port against the original game. Expected numbers were recorded from the A-Frame build
    /// (aframe-low-poly 0.0.2 and three.js r87) running in a browser.
    /// </summary>
    public class LowPolyTests
    {
        [Test]
        public void RandomSequenceMatchesAframeLowPoly()
        {
            var random = new LowPolyRandom(LowPolyRandom.DefaultSeed);
            double[] expected = { 0.859316073, 0.978929455, 0.081040624, 0.057081968, 0.100149520, 0.481930513 };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(random.NextDouble(), Is.EqualTo(expected[i]).Within(1e-9), "value " + i);
            }
        }

        [Test]
        public void RandomSequenceStaysInStepOverManyDraws()
        {
            var random = new LowPolyRandom(LowPolyRandom.DefaultSeed);
            double value = 0;
            for (int i = 0; i < 1000; i++)
            {
                value = random.NextDouble();
            }

            Assert.That(value, Is.EqualTo(0.632103130).Within(1e-9));
        }

        [Test]
        public void PlatformMatchesThreeJsGeometry()
        {
            ThreeMesh mesh = ThreeGeometry.Cylinder(1.9f, 1.9f, 20f, 20, 20);
            ThreeGeometry.Jitter(mesh, 0.05f, 0.05f, LowPolyRandom.DefaultSeed);

            Assert.That(mesh.Vertices.Count, Is.EqualTo(422));
            Assert.That(mesh.FaceCount, Is.EqualTo(840));
            AssertVertex(mesh.Vertices[0], -0.076510f, 10.025765f, 1.943609f);
            AssertVertex(mesh.Vertices[1], 0.599845f, 9.966864f, 1.843112f);
            AssertVertex(mesh.Vertices[421], 0.054535f, -10.079393f, -0.044106f);
            CollectionAssert.AreEqual(new[] { 0, 20, 1 }, mesh.Triangles.GetRange(0, 3));
            CollectionAssert.AreEqual(new[] { 400, 419, 421 }, mesh.Triangles.GetRange(mesh.Triangles.Count - 3, 3));
        }

        [Test]
        public void IcebergVertexCountsMatchThreeJs()
        {
            Assert.That(ThreeGeometry.Cylinder(0.15f, 0.5f, 1f, 5, 3).Vertices.Count, Is.EqualTo(22));
            Assert.That(ThreeGeometry.Cylinder(0.25f, 0.35f, 0.5f, 7, 3).FaceCount, Is.EqualTo(56));
            Assert.That(ThreeGeometry.Cylinder(0.25f, 0.25f, 0.5f, 6, 2).FaceCount, Is.EqualTo(36));
        }

        [Test]
        public void FlatMeshFacesPointOutwards()
        {
            Mesh mesh = ThreeGeometry.ToUnityMesh(ThreeGeometry.Box(1f, 1f, 1f), true, "Box");
            try
            {
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 center = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
                    Vector3 winding = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
                    Assert.That(Vector3.Dot(winding, center), Is.GreaterThan(0f), "triangle " + (i / 3) + " is wound inwards");
                    Assert.That(Vector3.Dot(normals[triangles[i]], center), Is.GreaterThan(0f), "normal " + (i / 3) + " points inwards");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        private static void AssertVertex(Vector3 actual, float x, float y, float z)
        {
            Assert.That(actual.x, Is.EqualTo(x).Within(1e-4f));
            Assert.That(actual.y, Is.EqualTo(y).Within(1e-4f));
            Assert.That(actual.z, Is.EqualTo(z).Within(1e-4f));
        }
    }

    public class ConversionTests
    {
        [Test]
        public void EasingMatchesTweenJs()
        {
            Assert.That(Easing.Evaluate(EasingType.CubicInOut, 0f), Is.EqualTo(0f));
            Assert.That(Easing.Evaluate(EasingType.CubicInOut, 0.25f), Is.EqualTo(0.0625f).Within(1e-6f));
            Assert.That(Easing.Evaluate(EasingType.CubicInOut, 0.5f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(Easing.Evaluate(EasingType.CubicInOut, 1f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(Easing.Evaluate(EasingType.CubicIn, 0.5f), Is.EqualTo(0.125f).Within(1e-6f));
            Assert.That(Easing.Evaluate(EasingType.CubicOut, 0.5f), Is.EqualTo(0.875f).Within(1e-6f));
            Assert.That(Easing.Evaluate(EasingType.Linear, 0.3f), Is.EqualTo(0.3f).Within(1e-6f));
        }

        [Test]
        public void RotationsMirrorThreeJs()
        {
            // In three.js rotating +90 degrees about X turns +Y into +Z; mirrored, that is -Z in Unity.
            AssertVector(AFrame.Rotation(90f, 0f, 0f) * Vector3.up, new Vector3(0f, 0f, -1f));

            // A-Frame text faces +Z and is turned to face the camera with rotation="0 -90 0" when placed at +X.
            AssertVector(AFrame.Rotation(0f, -90f, 0f) * Vector3.back, new Vector3(-1f, 0f, 0f));
        }

        [Test]
        public void TrackSitsWhereTheOriginalTreeContainerWas()
        {
            // <lp-cone position="0 -3.5 -1.5" rotation="90 0 0" scale="2 2 2"> containing
            // <a-entity id="tree-container" position="0 .5 -1.5" rotation="-90 0 0">.
            Matrix4x4 platform = Matrix4x4.TRS(AFrame.Position(0f, -3.5f, -1.5f), AFrame.Rotation(90f, 0f, 0f), new Vector3(2f, 2f, 2f));
            Matrix4x4 track = platform * Matrix4x4.TRS(AFrame.Position(0f, 0.5f, -1.5f), AFrame.Rotation(-90f, 0f, 0f), Vector3.one);

            // three.js reports the container at world (0, -0.5, -0.5) with no rotation and scale 2.
            AssertVector(track.MultiplyPoint3x4(Vector3.zero), AFrame.Position(0f, -0.5f, -0.5f));
            AssertVector(track.MultiplyVector(Vector3.up), new Vector3(0f, 2f, 0f));
            AssertVector(track.MultiplyVector(Vector3.forward), new Vector3(0f, 0f, 2f));
        }

        [Test]
        public void YawIsPositiveWhenTurningLeft()
        {
            Assert.That(AFrame.Yaw(Quaternion.Euler(0f, -30f, 0f)), Is.EqualTo(30f * Mathf.Deg2Rad).Within(1e-5f));
            Assert.That(AFrame.Yaw(Quaternion.Euler(0f, 30f, 0f)), Is.EqualTo(-30f * Mathf.Deg2Rad).Within(1e-5f));
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), "expected " + expected + " but was " + actual);
        }
    }

    public class SimulationTests
    {
        [Test]
        public void TreesFollowAframesDefaultEasing()
        {
            var simulation = new ErgoSimulation(new ErgoSettings(), 1);
            Assert.That(simulation.TreeZAt(0f), Is.EqualTo(-7f).Within(1e-5f));
            Assert.That(simulation.TreeZAt(2.5f), Is.EqualTo(-2.75f).Within(1e-4f));
            Assert.That(simulation.TreeZAt(5f), Is.EqualTo(1.5f).Within(1e-5f));

            // Measured in the browser: 3.566 s after it appeared a tree was at z = 0.696.
            Assert.That(simulation.TreeZAt(3.566f), Is.EqualTo(0.696f).Within(0.01f));
        }

        [Test]
        public void PlayerLaneIsClamped()
        {
            var simulation = new ErgoSimulation(new ErgoSettings(), 1);
            simulation.MovePlayerTo(-3);
            Assert.That(simulation.PlayerLane, Is.EqualTo(0));
            simulation.MovePlayerTo(7);
            Assert.That(simulation.PlayerLane, Is.EqualTo(2));
        }

        [Test]
        public void TreeInThePlayersLaneEndsTheGame()
        {
            ErgoSimulation simulation = RunningSimulationWithoutRows();
            bool gameOver = false;
            simulation.GameOver += () => gameOver = true;
            simulation.MovePlayerTo(1);
            simulation.AddTree(1);

            Step(simulation, 5f);

            Assert.That(gameOver, Is.True);
            Assert.That(simulation.IsRunning, Is.False);
            Assert.That(simulation.Score, Is.EqualTo(0));
        }

        [Test]
        public void TreeInAnotherLaneScoresAndIsRemoved()
        {
            ErgoSimulation simulation = RunningSimulationWithoutRows();
            var removed = new List<TreeState>();
            simulation.TreeRemoved += removed.Add;
            simulation.MovePlayerTo(1);
            TreeState tree = simulation.AddTree(0);

            Step(simulation, 4.5f);

            Assert.That(simulation.IsRunning, Is.True);
            Assert.That(simulation.Score, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { tree }, removed);
            Assert.That(simulation.Trees.Count, Is.EqualTo(0));
        }

        [Test]
        public void ALongFrameCannotSkipTheHitZone()
        {
            ErgoSimulation simulation = RunningSimulationWithoutRows();
            simulation.MovePlayerTo(2);
            simulation.AddTree(2);

            simulation.Tick(3.3f);
            Assert.That(simulation.IsRunning, Is.True, "the tree has not reached the player yet");

            // One 0.5 s frame takes the tree from z = 0.17 to z = 1.03, straight over the 0.6 - 0.7 zone.
            simulation.Tick(0.5f);
            Assert.That(simulation.IsRunning, Is.False);
        }

        [Test]
        public void RowsArriveEveryHalfSecond()
        {
            var settings = new ErgoSettings { laneTreeProbability = new[] { 1f, 1f, 1f } };
            var simulation = new ErgoSimulation(settings, 3);
            simulation.StartGame();

            simulation.Tick(0.49f);
            Assert.That(simulation.Trees.Count, Is.EqualTo(0));
            simulation.Tick(0.02f);
            Assert.That(simulation.Trees.Count, Is.EqualTo(2), "a full row still leaves one lane open");
            simulation.Tick(0.5f);
            Assert.That(simulation.Trees.Count, Is.EqualTo(4));
        }

        [Test]
        public void RowsNeverBlockEveryLane()
        {
            var simulation = new ErgoSimulation(new ErgoSettings(), 42);
            var rowSizes = new int[4];
            for (int row = 0; row < 4000; row++)
            {
                rowSizes[simulation.AddTreesRandomly(0f)]++;
            }

            Assert.That(rowSizes[3], Is.EqualTo(0));

            // Three independent 50% draws capped at two trees: P(0) = 1/8, P(1) = 3/8, P(2) = 1/2.
            Assert.That(rowSizes[0] / 4000f, Is.EqualTo(0.125f).Within(0.03f));
            Assert.That(rowSizes[1] / 4000f, Is.EqualTo(0.375f).Within(0.03f));
            Assert.That(rowSizes[2] / 4000f, Is.EqualTo(0.5f).Within(0.03f));
        }

        [Test]
        public void GameOverStopsSpawningButTreesKeepMoving()
        {
            var settings = new ErgoSettings { laneTreeProbability = new[] { 1f, 1f, 1f } };
            var simulation = new ErgoSimulation(settings, 5);
            simulation.StartGame();
            Step(simulation, 1.1f);
            int trees = simulation.Trees.Count;
            float z = simulation.Trees[0].Z;

            simulation.EndGame();
            simulation.Tick(1f);

            Assert.That(simulation.Trees.Count, Is.EqualTo(trees));
            Assert.That(simulation.Trees[0].Z, Is.GreaterThan(z));
        }

        private static ErgoSimulation RunningSimulationWithoutRows()
        {
            var settings = new ErgoSettings { laneTreeProbability = new[] { 0f, 0f, 0f } };
            var simulation = new ErgoSimulation(settings, 7);
            simulation.StartGame();
            return simulation;
        }

        private static void Step(ErgoSimulation simulation, float seconds)
        {
            const float frame = 1f / 60f;
            for (float time = 0f; time < seconds; time += frame)
            {
                simulation.Tick(frame);
            }
        }
    }
}
