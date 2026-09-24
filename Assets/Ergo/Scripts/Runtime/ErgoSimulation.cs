using System;
using System.Collections.Generic;

namespace Ergo
{
    /// <summary>A tree obstacle travelling down one lane.</summary>
    public sealed class TreeState
    {
        /// <summary>Unique, increasing id (the original named trees "tree-1", "tree-2", ...).</summary>
        public int Id;

        /// <summary>Lane index: 0 left, 1 centre, 2 right.</summary>
        public int Lane;

        /// <summary>Seconds since the tree started moving.</summary>
        public float Age;

        /// <summary>Current Z in A-Frame track units (from -7 towards 1.5).</summary>
        public float Z;

        /// <summary>Z on the previous tick; used to catch trees that cross the hit zone between frames.</summary>
        public float PreviousZ;

        /// <summary>Whether this tree has already been scored.</summary>
        public bool Counted;

        internal bool Removed;
    }

    /// <summary>
    /// Engine-independent port of the game rules in the original <c>runner.js</c>: lanes, rows of trees,
    /// collisions, scoring and the running/game-over state. Views subscribe to the events.
    /// </summary>
    public sealed class ErgoSimulation
    {
        /// <summary>Number of lanes.</summary>
        public const int LaneCount = 3;

        private readonly ErgoSettings settings;
        private readonly System.Random random;
        private readonly List<TreeState> trees = new List<TreeState>();
        private readonly List<TreeState> removedThisTick = new List<TreeState>();
        private readonly int[] laneOrder = new int[LaneCount];
        private float spawnClock;
        private bool spawning;
        private int treesCreated;

        /// <summary>Creates the simulation. A seed of 0 picks a random seed.</summary>
        public ErgoSimulation(ErgoSettings settings, int seed)
        {
            this.settings = settings ?? new ErgoSettings();
            random = seed == 0 ? new System.Random() : new System.Random(seed);
            PlayerLane = 1;
        }

        /// <summary>Raised after a tree is created.</summary>
        public event Action<TreeState> TreeAdded;

        /// <summary>Raised after a tree leaves the track.</summary>
        public event Action<TreeState> TreeRemoved;

        /// <summary>Raised when the score changes.</summary>
        public event Action ScoreChanged;

        /// <summary>Raised when a run starts.</summary>
        public event Action GameStarted;

        /// <summary>Raised when the player hits a tree.</summary>
        public event Action GameOver;

        /// <summary>Lane the player is in: 0 left, 1 centre, 2 right.</summary>
        public int PlayerLane { get; private set; }

        /// <summary>True while a run is in progress.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Trees that passed the player in the current (or last) run.</summary>
        public int Score { get; private set; }

        /// <summary>Trees currently on the track, oldest first.</summary>
        public IList<TreeState> Trees
        {
            get { return trees.AsReadOnly(); }
        }

        /// <summary>movePlayerTo(): moves the player to a lane, clamped to the track.</summary>
        public void MovePlayerTo(int lane)
        {
            PlayerLane = Math.Max(0, Math.Min(LaneCount - 1, lane));
        }

        /// <summary>startGame(): resets the score and starts spawning rows of trees. Ignored while running.</summary>
        public bool StartGame()
        {
            if (IsRunning)
            {
                return false;
            }

            IsRunning = true;
            Score = 0;
            spawning = true;
            spawnClock = 0f;

            var started = GameStarted;
            if (started != null)
            {
                started();
            }

            RaiseScoreChanged();
            return true;
        }

        /// <summary>gameOver(): stops the run and the spawner. Trees already on the track keep moving.</summary>
        public void EndGame()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            spawning = false;

            var over = GameOver;
            if (over != null)
            {
                over();
            }
        }

        /// <summary>Z of a tree <paramref name="age"/> seconds after it appeared.</summary>
        public float TreeZAt(float age)
        {
            float duration = Math.Max(settings.treeTravelTime, 0.0001f);
            float k = Math.Min(1f, Math.Max(0f, age / duration));
            return settings.treeStartZ + (settings.treeEndZ - settings.treeStartZ) * Easing.Evaluate(settings.treeEasing, k);
        }

        /// <summary>Adds a tree to a lane immediately (addTreeTo()).</summary>
        public TreeState AddTree(int lane)
        {
            return AddTree(lane, 0f);
        }

        /// <summary>
        /// Advances the game: moves trees, removes the ones out of sight, checks collisions and scores
        /// (the original <c>player</c> component tick), then spawns rows on the original 500 ms cadence.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            for (int i = 0; i < trees.Count; i++)
            {
                TreeState tree = trees[i];
                tree.PreviousZ = tree.Z;
                tree.Age += deltaTime;
                tree.Z = TreeZAt(tree.Age);

                if (tree.Z > settings.treeRemoveZ && !tree.Removed)
                {
                    tree.Removed = true;
                    removedThisTick.Add(tree);
                }

                if (!IsRunning)
                {
                    continue;
                }

                // The original checked collisionZStart < z < collisionZEnd once per frame. Testing the distance
                // covered since the last tick keeps that behaviour but cannot be skipped by a slow frame.
                if (tree.Lane == PlayerLane && tree.Z > settings.collisionZStart && tree.PreviousZ < settings.collisionZEnd)
                {
                    EndGame();
                    continue;
                }

                if (tree.Z > settings.collisionZEnd && !tree.Counted)
                {
                    tree.Counted = true;
                    Score++;
                    RaiseScoreChanged();
                }
            }

            if (removedThisTick.Count > 0)
            {
                for (int i = 0; i < removedThisTick.Count; i++)
                {
                    TreeState tree = removedThisTick[i];
                    trees.Remove(tree);
                    var removed = TreeRemoved;
                    if (removed != null)
                    {
                        removed(tree);
                    }
                }

                removedThisTick.Clear();
            }

            if (spawning)
            {
                float interval = Math.Max(settings.treeSpawnInterval, 0.01f);
                spawnClock += deltaTime;
                if (spawnClock >= interval)
                {
                    spawnClock -= interval;
                    if (spawnClock >= interval)
                    {
                        // Like setInterval after a long stall: fire once, then carry on from the current time.
                        spawnClock %= interval;
                    }

                    // The row was due spawnClock seconds ago, so its trees start with that much head start.
                    AddTreesRandomly(spawnClock);
                }
            }
        }

        /// <summary>
        /// addTreesRandomly(): shuffles the lanes, then gives each one a chance of a tree until the row is full.
        /// </summary>
        public int AddTreesRandomly(float age)
        {
            for (int i = 0; i < LaneCount; i++)
            {
                laneOrder[i] = i;
            }

            // Same Fisher-Yates variant as shuffle() in runner.js.
            for (int i = LaneCount - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(random.NextDouble() * (i + 1));
                int swap = laneOrder[i];
                laneOrder[i] = laneOrder[j];
                laneOrder[j] = swap;
            }

            int added = 0;
            for (int i = 0; i < LaneCount; i++)
            {
                int lane = laneOrder[i];
                if (random.NextDouble() < settings.TreeProbability(lane) && added < settings.maxTreesPerRow)
                {
                    AddTree(lane, age);
                    added++;
                }
            }

            return added;
        }

        private TreeState AddTree(int lane, float age)
        {
            var tree = new TreeState
            {
                Id = ++treesCreated,
                Lane = Math.Max(0, Math.Min(LaneCount - 1, lane)),
                Age = Math.Max(0f, age),
            };
            tree.Z = TreeZAt(tree.Age);
            tree.PreviousZ = tree.Z;
            trees.Add(tree);

            var added = TreeAdded;
            if (added != null)
            {
                added(tree);
            }

            return tree;
        }

        private void RaiseScoreChanged()
        {
            var changed = ScoreChanged;
            if (changed != null)
            {
                changed();
            }
        }
    }
}
