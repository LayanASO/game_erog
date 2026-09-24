using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Ergo
{
    /// <summary>
    /// Entry point of the Unity port of Ergo. Put it on an empty GameObject (the Ergo scene already has one):
    /// on Awake it rebuilds the original scene, then runs the game loop from <c>runner.js</c>: menus, controls,
    /// rows of trees, collisions and score.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ErgoGame : MonoBehaviour
    {
        private const string FontResource = "Ergo/Fonts/Exo2-Bold";

        [SerializeField] private ErgoSettings settings = new ErgoSettings();

        [Tooltip("Seed for the tree rows and ocean waves. 0 picks a new random seed every run.")]
        [SerializeField] private int randomSeed = 0;

        private ErgoRenderer ergoRenderer;
        private ErgoWorld world;
        private ErgoSimulation simulation;
        private TreeViews trees;
        private bool mobileControls;

        /// <summary>The game rules (score, lanes, trees); null until Awake has run.</summary>
        public ErgoSimulation Simulation
        {
            get { return simulation; }
        }

        /// <summary>Tunables, initialised with the original game's values.</summary>
        public ErgoSettings Settings
        {
            get { return settings; }
        }

        /// <summary>True when the mobile (head-turn and gaze) controls are in use.</summary>
        public bool MobileControls
        {
            get { return mobileControls; }
        }

        /// <summary>startGame(): starts a run unless one is already going.</summary>
        public void StartGame()
        {
            if (simulation != null)
            {
                simulation.StartGame();
            }
        }

        /// <summary>movePlayerTo(): moves the player to lane 0 (left), 1 (centre) or 2 (right).</summary>
        public void MovePlayerTo(int lane)
        {
            simulation.MovePlayerTo(lane);
            world.Player.localPosition = AFrame.Position(settings.LaneX(simulation.PlayerLane), 0f, 0f);
        }

        private void Awake()
        {
            ergoRenderer = new ErgoRenderer();
            if (!ergoRenderer.IsValid)
            {
                Debug.LogError("Ergo: the Ergo shaders could not be loaded, so the game cannot start.", this);
                enabled = false;
                return;
            }

            mobileControls = settings.controlScheme == ControlScheme.Mobile
                || (settings.controlScheme == ControlScheme.Automatic && Application.isMobilePlatform);
            if (Application.isMobilePlatform)
            {
                // The player steers by turning their head, so keep the screen awake and the frame rate up.
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                Application.targetFrameRate = 60;
            }

            var random = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
            world = ErgoWorld.Build(transform, ergoRenderer, LoadFont(), settings, mobileControls, random);

            simulation = new ErgoSimulation(settings, random.Next(1, int.MaxValue));
            trees = new TreeViews(world, settings, ergoRenderer);
            simulation.TreeAdded += trees.Show;
            simulation.TreeRemoved += trees.Hide;
            simulation.ScoreChanged += OnScoreChanged;
            simulation.GameStarted += OnGameStarted;
            simulation.GameOver += OnGameOver;
            world.StartButton.Clicked += StartGame;
            world.RestartButton.Clicked += StartGame;

            // window.onload: setupAllMenus, setupScore, setupTrees, setupInstructions, setupCursor.
            ShowStartMenu();
            world.Score.Text = string.Empty;
            SetupInstructions();
            world.CursorRing.SetActive(mobileControls);
            MovePlayerTo(1);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            world.Look.Tick();

            if (mobileControls)
            {
                // lane-controls: turning the head (or phone) past the threshold picks the left or right lane.
                float yaw = AFrame.Yaw(world.Camera.transform.rotation);
                MovePlayerTo(yaw > settings.headTurnThreshold ? 0 : yaw < -settings.headTurnThreshold ? 2 : 1);
                world.Cursor.Tick(deltaTime);
                HandleTap();
            }
            else
            {
                // window.onkeydown: any key starts the game; the arrow keys and A/D change lane.
                if (ErgoInput.AnyKeyPressed())
                {
                    StartGame();
                }

                if (ErgoInput.LeftPressed())
                {
                    MovePlayerTo(simulation.PlayerLane - 1);
                }

                if (ErgoInput.RightPressed())
                {
                    MovePlayerTo(simulation.PlayerLane + 1);
                }
            }

            simulation.Tick(deltaTime);
            trees.Sync();
        }

        private void LateUpdate()
        {
            ergoRenderer.UpdateLighting(settings, world.PlayerLight);
            ergoRenderer.RenderShadows(settings);
        }

        private void OnDestroy()
        {
            if (simulation != null)
            {
                simulation.TreeAdded -= trees.Show;
                simulation.TreeRemoved -= trees.Hide;
                simulation.ScoreChanged -= OnScoreChanged;
                simulation.GameStarted -= OnGameStarted;
                simulation.GameOver -= OnGameOver;
            }

            if (ergoRenderer != null)
            {
                ergoRenderer.Dispose();
            }
        }

        // A tap presses whatever the gaze cursor is on (A-Frame's cursor turns canvas clicks into clicks on the
        // intersected entity); tapping a button directly works too.
        private void HandleTap()
        {
            Vector2 position;
            bool pressedThisFrame;
            ErgoInput.GetPointer(out position, out pressedThisFrame);
            if (!pressedThisFrame)
            {
                return;
            }

            ClickTarget target = world.Cursor.Hovered;
            if (target == null)
            {
                target = world.Cursor.FindTarget(world.Camera.ScreenPointToRay(position));
            }

            if (target != null)
            {
                target.Click();
            }
        }

        private void OnGameStarted()
        {
            HideAllMenus();
        }

        private void OnScoreChanged()
        {
            // updateScoreDisplay()
            if (simulation.IsRunning)
            {
                world.Score.Text = simulation.Score.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void OnGameOver()
        {
            ShowGameOverMenu();
            SetupInstructions();

            // teardownScore()
            world.Score.Text = string.Empty;
            world.GameScore.Text = simulation.Score.ToString(CultureInfo.InvariantCulture);
        }

        private void ShowStartMenu()
        {
            world.MenuContainer.SetActive(true);
            world.GameOverMenu.SetActive(false);
            world.StartMenu.SetActive(true);
            world.StartButton.clickable = true;
            world.RestartButton.clickable = false;
        }

        private void ShowGameOverMenu()
        {
            world.MenuContainer.SetActive(true);
            world.StartMenu.SetActive(false);
            world.GameOverMenu.SetActive(true);
            world.StartButton.clickable = false;
            world.RestartButton.clickable = true;
        }

        private void HideAllMenus()
        {
            world.MenuContainer.SetActive(false);
            world.StartButton.clickable = false;
            world.RestartButton.clickable = false;
        }

        // setupInstructions(): keyboard instructions on desktop, head-turn instructions and buttons on mobile.
        private void SetupInstructions()
        {
            world.StartCopyDesktop.SetActive(!mobileControls);
            world.GameOverCopyDesktop.SetActive(!mobileControls);
            world.StartCopyMobile.SetActive(mobileControls);
            world.GameOverCopyMobile.SetActive(mobileControls);
        }

        private static Font LoadFont()
        {
            var font = Resources.Load<Font>(FontResource);
            if (font == null)
            {
                Debug.LogWarning("Ergo: Exo 2 font not found, falling back to Unity's built-in font.");
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return font;
        }

        /// <summary>Pooled tree objects that follow the simulation's <see cref="TreeState"/>s.</summary>
        private sealed class TreeViews
        {
            private readonly ErgoWorld world;
            private readonly ErgoSettings settings;
            private readonly ErgoRenderer renderer;
            private readonly Stack<Transform> pool = new Stack<Transform>();
            private readonly Dictionary<TreeState, Transform> active = new Dictionary<TreeState, Transform>();

            public TreeViews(ErgoWorld world, ErgoSettings settings, ErgoRenderer renderer)
            {
                this.world = world;
                this.settings = settings;
                this.renderer = renderer;
            }

            public void Show(TreeState tree)
            {
                Transform view = pool.Count > 0 ? pool.Pop() : Create();
                view.name = "tree-" + tree.Id.ToString(CultureInfo.InvariantCulture);
                view.gameObject.SetActive(true);
                active[tree] = view;
                Place(tree, view);
            }

            public void Hide(TreeState tree)
            {
                Transform view;
                if (!active.TryGetValue(tree, out view))
                {
                    return;
                }

                active.Remove(tree);
                view.gameObject.SetActive(false);
                pool.Push(view);
            }

            public void Sync()
            {
                foreach (KeyValuePair<TreeState, Transform> pair in active)
                {
                    Place(pair.Key, pair.Value);
                }
            }

            private void Place(TreeState tree, Transform view)
            {
                view.localPosition = AFrame.Position(settings.LaneX(tree.Lane), settings.TreeY(tree.Lane), tree.Z);
            }

            // The tree templates: scale 0.3, foliage cone at the origin, trunk half a unit below.
            private Transform Create()
            {
                var root = new GameObject("Tree").transform;
                root.SetParent(world.Track, false);
                root.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                AddPart(root, "Foliage", world.TreeFoliageMesh, Vector3.zero);
                AddPart(root, "Trunk", world.TreeTrunkMesh, AFrame.Position(0f, -0.5f, 0f));
                return root;
            }

            private void AddPart(Transform root, string name, Mesh mesh, Vector3 localPosition)
            {
                var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(root, false);
                part.transform.localPosition = localPosition;
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                var meshRenderer = part.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = world.TreeMaterial;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                renderer.AddShadowCaster(meshRenderer);
            }
        }
    }
}
