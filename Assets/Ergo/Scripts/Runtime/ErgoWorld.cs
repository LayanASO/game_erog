using UnityEngine;

namespace Ergo
{
    /// <summary>
    /// Builds the scene described by the original <c>index.html</c>, entity by entity, with the same numbers
    /// (converted through <see cref="AFrame"/>), and keeps references to the parts the game drives.
    /// </summary>
    public sealed class ErgoWorld
    {
        private const int TitleGlyphSize = 128;
        private const int HeadingGlyphSize = 64;
        private const int CopyGlyphSize = 48;

        // Mixins from index.html: title (width 40), heading (width 10) and copy (width 5), all at opacity 0.75.
        private const float TitleWidth = 40f;
        private const float HeadingWidth = 10f;
        private const float CopyWidth = 5f;
        private const float MixinOpacity = 0.75f;

        private readonly ErgoRenderer renderer;
        private readonly Font font;
        private readonly Material textMaterial;

        public Camera Camera;
        public CameraLook Look;
        public GazeCursor Cursor;
        public GameObject CursorRing;
        public Transform Track;
        public Transform Player;
        public ErgoPointLight PlayerLight;
        public WorldText Score;
        public WorldText GameScore;
        public GameObject MenuContainer;
        public GameObject StartMenu;
        public GameObject GameOverMenu;
        public GameObject StartCopyDesktop;
        public GameObject StartCopyMobile;
        public GameObject GameOverCopyDesktop;
        public GameObject GameOverCopyMobile;
        public ClickTarget StartButton;
        public ClickTarget RestartButton;
        public Mesh TreeFoliageMesh;
        public Mesh TreeTrunkMesh;
        public Material TreeMaterial;

        private ErgoWorld(ErgoRenderer ergoRenderer, Font textFont)
        {
            renderer = ergoRenderer;
            font = textFont;
            textMaterial = renderer.CreateText(font);
            textMaterial.renderQueue = 3010; // after the see-through ocean, as in the original draw order
        }

        /// <summary>Creates every object of the original scene under <paramref name="root"/>.</summary>
        public static ErgoWorld Build(Transform root, ErgoRenderer renderer, Font font, ErgoSettings settings, bool mobileControls, System.Random random)
        {
            var world = new ErgoWorld(renderer, font);
            world.BuildCamera(settings, mobileControls);
            world.BuildIcebergs(root);
            world.BuildOcean(root, random);
            world.BuildPlatform(root, settings);
            return world;
        }

        // <a-camera lane-controls position="0 0 2.5"> with the default 1.6 m user height, 80 degree field of
        // view, and the fuse cursor ring attached one metre in front.
        private void BuildCamera(ErgoSettings settings, bool mobileControls)
        {
            Camera = UnityEngine.Camera.main;
            if (Camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                Camera = cameraObject.AddComponent<Camera>();
            }

            Transform cameraTransform = Camera.transform;
            cameraTransform.position = AFrame.Position(0f, 1.6f, 2.5f);
            cameraTransform.rotation = Quaternion.identity;
            Camera.fieldOfView = 80f;
            Camera.nearClipPlane = 0.01f;
            Camera.farClipPlane = 1000f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = settings.skyColor;

            Look = Camera.GetComponent<CameraLook>();
            if (Look == null)
            {
                Look = Camera.gameObject.AddComponent<CameraLook>();
            }

            Look.Initialize(settings, mobileControls);

            CursorRing = CreateMesh("Cursor", cameraTransform, ThreeGeometry.Ring(0.02f, 0.03f, 32, 10), false, renderer.CreateUnlit("Ergo Cursor", Color.white));
            CursorRing.transform.localPosition = AFrame.Position(0f, 0f, -1f);
            CursorRing.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            Cursor = CursorRing.AddComponent<GazeCursor>();
            Cursor.Initialize(Camera, settings.fuseTimeout);
        }

        // Three lp-cone icebergs drifting and rocking on the water.
        private void BuildIcebergs(Transform root)
        {
            Material ice = renderer.CreateStandard("Ergo Iceberg", Color.white, Color.black, 0f, false);
            var group = new GameObject("Icebergs").transform;
            group.SetParent(root, false);

            Iceberg(group, ice, 0.15f, 0.5f, 1f, 5, 3, 0.05f, 0.25f,
                new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f), 1000f,
                new Vector3(3f, -0.2f, -1.5f), new Vector3(4f, -0.2f, -2.5f), 12000f);
            Iceberg(group, ice, 0.25f, 0.35f, 0.5f, 7, 3, 0.12f, 0.001f,
                new Vector3(0f, 0f, -5f), new Vector3(5f, 0f, 0f), 1500f,
                new Vector3(-4f, -0.2f, -0.5f), new Vector3(-2f, -0.2f, -0.5f), 15000f);
            Iceberg(group, ice, 0.25f, 0.25f, 0.5f, 6, 2, 0.1f, 0.001f,
                new Vector3(5f, 0f, -5f), new Vector3(5f, 0f, 0f), 800f,
                new Vector3(-3f, -0.2f, -3.5f), new Vector3(-5f, -0.2f, -5.5f), 15000f);
        }

        private void Iceberg(Transform parent, Material material, float radiusTop, float radiusBottom, float height, int radialSegments, int heightSegments,
            float amplitude, float amplitudeVariance, Vector3 rotationFrom, Vector3 rotationTo, float rotationMs, Vector3 positionFrom, Vector3 positionTo, float positionMs)
        {
            ThreeMesh shape = ThreeGeometry.Cylinder(radiusTop, radiusBottom, height, radialSegments, heightSegments);
            ThreeGeometry.Jitter(shape, amplitude, amplitudeVariance, LowPolyRandom.DefaultSeed);
            GameObject iceberg = CreateMesh("Iceberg", parent, shape, true, material);
            iceberg.AddComponent<AFrameAnimation>().Configure(AnimatedProperty.Rotation, rotationFrom, rotationTo, rotationMs,
                Easing.AFrameDefault, AnimationDirection.Alternate, true);
            iceberg.AddComponent<AFrameAnimation>().Configure(AnimatedProperty.Position, positionFrom, positionTo, positionMs,
                EasingType.Linear, AnimationDirection.Alternate, true);
        }

        // Two a-ocean layers: an opaque one and a half-transparent one with bigger waves on top.
        private void BuildOcean(Transform root, System.Random random)
        {
            Color water = AFrame.Hex("#7AD2F7");
            CreateOcean(root, "Ocean", 0.1f, 1f, water, random);
            CreateOcean(root, "Ocean (Overlay)", 0.15f, 0.5f, water, random);
        }

        private void CreateOcean(Transform root, string name, float amplitudeVariance, float opacity, Color color, System.Random random)
        {
            var ocean = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            ocean.transform.SetParent(root, false);
            var meshRenderer = ocean.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = renderer.CreatePhong("Ergo " + name, color, opacity);
            DisableUnityShadows(meshRenderer);
            ocean.AddComponent<OceanSurface>().Build(50f, 50f, 50, 0f, amplitudeVariance, 1.5f, 1f, random);
        }

        // The big low-poly cylinder the player runs along, with the track (#tree-container) inside it.
        private void BuildPlatform(Transform root, ErgoSettings settings)
        {
            ThreeMesh shape = ThreeGeometry.Cylinder(1.9f, 1.9f, 20f, 20, 20);
            ThreeGeometry.Jitter(shape, 0.05f, 0.05f, LowPolyRandom.DefaultSeed);
            Material ground = renderer.CreateStandard("Ergo Platform", Color.white, AFrame.Hex("#005DED"), 0.1f, true);
            GameObject platform = CreateMesh("Platform", root, shape, true, ground);
            Place(platform.transform, new Vector3(0f, -3.5f, -1.5f), new Vector3(90f, 0f, 0f));
            platform.transform.localScale = new Vector3(2f, 2f, 2f);
            renderer.AddShadowCaster(platform.GetComponent<MeshRenderer>());

            Track = Entity("Track", platform.transform, new Vector3(0f, 0.5f, -1.5f), new Vector3(-90f, 0f, 0f)).transform;

            // Tell the user to turn around if facing the wrong way.
            Text(Track, "Turn around!", new Vector3(0f, 1.1f, 10f), new Vector3(0f, 180f, 0f), TitleWidth, MixinOpacity, Color.white, TitleGlyphSize);
            Text(Track, "Turn left", new Vector3(8f, 1.1f, 1.5f), new Vector3(0f, -90f, 0f), TitleWidth, MixinOpacity, Color.white, TitleGlyphSize);
            Text(Track, "Turn right", new Vector3(-8f, 1.1f, 1.5f), new Vector3(0f, 90f, 0f), TitleWidth, MixinOpacity, Color.white, TitleGlyphSize);
            Text(Track, "Look up", new Vector3(0f, 0.5f, 1.5f), new Vector3(-90f, 0f, 0f), 2f, 1f, AFrame.Hex("#333333"), CopyGlyphSize);

            // Shared tree parts: a four-sided cone of foliage on a thin trunk (mixins "foliage" and "trunk").
            TreeMaterial = renderer.CreateStandard("Ergo Tree", Color.white, Color.black, 0f, true);
            TreeFoliageMesh = renderer.Own(ThreeGeometry.ToUnityMesh(ThreeGeometry.Cylinder(0.01f, 0.3f, 1f, 4, 1), true, "Tree Foliage"));
            TreeTrunkMesh = renderer.Own(ThreeGeometry.ToUnityMesh(ThreeGeometry.Box(0.1f, 0.5f, 0.1f), true, "Tree Trunk"));

            Score = Text(Track, string.Empty, new Vector3(0f, 1.2f, -3f), Vector3.zero, TitleWidth, MixinOpacity, Color.white, TitleGlyphSize);
            Score.name = "Score";

            BuildMenus();
            BuildPlayer(settings);
        }

        private void BuildMenus()
        {
            Material buttonMaterial = renderer.CreateStandard("Ergo Button", Color.white, Color.black, 0f, true);
            MenuContainer = Entity("Menus", Track, Vector3.zero, Vector3.zero);

            StartMenu = Entity("Start Menu", MenuContainer.transform, new Vector3(0f, 1.1f, -3f), Vector3.zero);
            StartCopyDesktop = Text(StartMenu.transform, "Hit any key to start. Move left and right to avoid the trees!",
                new Vector3(0f, 1f, 0f), Vector3.zero, CopyWidth, MixinOpacity, Color.white, CopyGlyphSize).gameObject;
            StartCopyMobile = Entity("Start Copy (Mobile)", StartMenu.transform, new Vector3(0f, 1f, 0f), Vector3.zero);
            Text(StartCopyMobile.transform, "Turn left and right to move your player, and avoid the trees!",
                Vector3.zero, Vector3.zero, CopyWidth, MixinOpacity, Color.white, CopyGlyphSize);
            Text(StartCopyMobile.transform, "Start", new Vector3(0f, 0.75f, 0f), Vector3.zero, HeadingWidth, MixinOpacity, Color.white, HeadingGlyphSize);
            StartButton = Button(StartCopyMobile.transform, "Start Button", new Vector3(0f, 0.65f, -0.05f), 1.5f, 0.6f, 0.1f, buttonMaterial);
            Text(StartMenu.transform, "ERGO", Vector3.zero, Vector3.zero, TitleWidth, MixinOpacity, Color.white, TitleGlyphSize);

            GameOverMenu = Entity("Game Over", MenuContainer.transform, new Vector3(0f, 1.1f, -3f), Vector3.zero);
            GameScore = Text(GameOverMenu.transform, "?", new Vector3(0f, 1.7f, 0f), Vector3.zero, HeadingWidth, MixinOpacity, Color.white, HeadingGlyphSize);
            GameScore.name = "Game Score";
            Text(GameOverMenu.transform, "Score", new Vector3(0f, 1.2f, 0f), Vector3.zero, CopyWidth, MixinOpacity, Color.white, CopyGlyphSize);
            GameOverCopyDesktop = Text(GameOverMenu.transform, "Hit any key to play again.",
                new Vector3(0f, 0.8f, 0f), Vector3.zero, CopyWidth, MixinOpacity, Color.white, CopyGlyphSize).gameObject;
            GameOverCopyMobile = Entity("Game Over Copy (Mobile)", GameOverMenu.transform, Vector3.zero, Vector3.zero);
            Text(GameOverCopyMobile.transform, "Restart", new Vector3(0f, 0.7f, 0f), Vector3.zero, HeadingWidth, MixinOpacity, Color.white, HeadingGlyphSize);
            RestartButton = Button(GameOverCopyMobile.transform, "Restart Button", new Vector3(0f, 0.6f, -0.05f), 2f, 0.6f, 0.1f, buttonMaterial);
            Text(GameOverMenu.transform, "Game Over", Vector3.zero, Vector3.zero, TitleWidth, MixinOpacity, Color.white, TitleGlyphSize);

            Cursor.AddTarget(StartButton);
            Cursor.AddTarget(RestartButton);
        }

        // <a-entity id="player"> holding a pulsing, bobbing sphere with an orange point light inside.
        private void BuildPlayer(ErgoSettings settings)
        {
            Player = Entity("Player", Track, Vector3.zero, Vector3.zero).transform;

            Material ballMaterial = renderer.CreateStandard("Ergo Player", Color.white, Color.black, 0f, true);
            GameObject ball = CreateMesh("Ball", Player, ThreeGeometry.Sphere(1f, 36, 18), false, ballMaterial);
            renderer.AddShadowCaster(ball.GetComponent<MeshRenderer>());
            ball.AddComponent<AFrameAnimation>().Configure(AnimatedProperty.Position, new Vector3(0f, 0.5f, 0.6f), new Vector3(0f, 0.525f, 0.6f), 1000f,
                Easing.AFrameDefault, AnimationDirection.Alternate, true);
            ball.AddComponent<AFrameAnimation>().Configure(AnimatedProperty.Scale, new Vector3(0.05f, 0.05f, 0.05f), new Vector3(0.055f, 0.055f, 0.055f), 1500f,
                Easing.AFrameDefault, AnimationDirection.Alternate, true);

            var light = new GameObject("Light");
            light.transform.SetParent(ball.transform, false);
            PlayerLight = light.AddComponent<ErgoPointLight>();
            PlayerLight.color = settings.playerLightColor;
            light.AddComponent<AFrameAnimation>().Configure(AnimatedProperty.LightIntensity, new Vector3(0.35f, 0f, 0f), new Vector3(0.5f, 0f, 0f), 1000f,
                Easing.AFrameDefault, AnimationDirection.AlternateReverse, true);
        }

        private WorldText Text(Transform parent, string value, Vector3 position, Vector3 rotation, float width, float opacity, Color color, int glyphSize)
        {
            var textObject = new GameObject(string.IsNullOrEmpty(value) ? "Text" : value, typeof(MeshFilter), typeof(MeshRenderer));
            textObject.transform.SetParent(parent, false);
            Place(textObject.transform, position, rotation);
            var text = textObject.AddComponent<WorldText>();
            text.Initialize(font, textMaterial, value, width, color, opacity, glyphSize);
            return text;
        }

        private ClickTarget Button(Transform parent, string name, Vector3 position, float width, float height, float depth, Material material)
        {
            GameObject box = CreateMesh(name, parent, ThreeGeometry.Box(width, height, depth), true, material);
            Place(box.transform, position, Vector3.zero);
            var target = box.AddComponent<ClickTarget>();
            target.size = new Vector3(width, height, depth);
            return target;
        }

        private GameObject CreateMesh(string name, Transform parent, ThreeMesh shape, bool flatShading, Material material)
        {
            var meshObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            meshObject.transform.SetParent(parent, false);
            meshObject.GetComponent<MeshFilter>().sharedMesh = renderer.Own(ThreeGeometry.ToUnityMesh(shape, flatShading, name));
            var meshRenderer = meshObject.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            DisableUnityShadows(meshRenderer);
            return meshObject;
        }

        private static GameObject Entity(string name, Transform parent, Vector3 aframePosition, Vector3 aframeRotation)
        {
            var entity = new GameObject(name);
            entity.transform.SetParent(parent, false);
            Place(entity.transform, aframePosition, aframeRotation);
            return entity;
        }

        private static void Place(Transform target, Vector3 aframePosition, Vector3 aframeRotation)
        {
            target.localPosition = AFrame.Position(aframePosition);
            target.localRotation = AFrame.Rotation(aframeRotation);
        }

        // Ergo draws its own shadows (see ErgoRenderer), so Unity's shadow passes are not needed.
        private static void DisableUnityShadows(MeshRenderer meshRenderer)
        {
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
    }
}
