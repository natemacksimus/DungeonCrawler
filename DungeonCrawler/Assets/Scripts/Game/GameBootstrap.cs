using UnityEngine;
using UnityEngine.InputSystem;
using DungeonCrawler.Core;

namespace DungeonCrawler.Game
{
    /// <summary>
    /// The one component the scene needs. It owns the simulation, builds the view, HUD and camera at
    /// startup, and translates keys into turns.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Run")]
        [Tooltip("0 picks a random seed each session. Set a value to replay an exact dungeon.")]
        [SerializeField] int seed;

        [Tooltip("How many floors deep the dungeon goes.")]
        [SerializeField] int maxDepth = 8;

        [Header("Diagnostics")]
        [Tooltip("Runs automated games before play starts and logs the results.")]
        [SerializeField] bool selfPlayOnStart;

        [SerializeField] int selfPlayRuns = 5;

        [Header("Input feel")]
        [Tooltip("Seconds a movement key must be held before it starts repeating.")]
        [SerializeField] float repeatDelay = 0.30f;

        [Tooltip("Seconds between repeats while a movement key is held.")]
        [SerializeField] float repeatInterval = 0.07f;

        [Header("Visuals")]
        [Tooltip("Optional artwork replacing the built-in procedural shapes. Leave empty to keep defaults.")]
        [SerializeField] VisualOverrides visualOverrides;

        GameState _game;
        DungeonView _view;
        GameHud _hud;
        CameraRig _rig;

        Vec2I _heldDirection;
        float _heldTime;
        float _nextRepeat;

        public GameState Game { get { return _game; } }

        void Awake()
        {
            int actualSeed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
            _game = new GameState(actualSeed, new GameConfig { MaxDepth = maxDepth });

            _view = gameObject.AddComponent<DungeonView>();
            _hud = gameObject.AddComponent<GameHud>();

            Camera camera = Camera.main;
            if (camera == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camera = camGo.AddComponent<Camera>();
            }
            _rig = camera.gameObject.GetComponent<CameraRig>();
            if (_rig == null) _rig = camera.gameObject.AddComponent<CameraRig>();

            _view.Initialize(_game, visualOverrides);
            _hud.Initialize(_game);
            _rig.Initialize(camera, _game);

            Debug.Log("[DungeonCrawler] seed " + actualSeed + ", " + maxDepth + " floors.");
        }

        void Start()
        {
            if (selfPlayOnStart) RunSelfPlay();
            StartRun();
        }

        void StartRun()
        {
            _game.StartNewRun();
            ReportFloorValidation();
            _rig.SnapToPlayer();
            Redraw();
        }

        void Redraw()
        {
            _view.Refresh();
            _hud.Refresh();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[Key.M].wasPressedThisFrame)
            {
                Debug.Log("[DungeonCrawler] floor " + _game.Depth + " (" + _game.LastValidation + ")\n" +
                          _game.Dungeon.ToAscii());
            }

            if (keyboard[Key.T].wasPressedThisFrame) RunSelfPlay();

            if (_game.Status != GameStatus.Playing)
            {
                if (keyboard[Key.R].wasPressedThisFrame)
                {
                    _game.Restart();
                    ReportFloorValidation();
                    _rig.SnapToPlayer();
                    Redraw();
                }
                return;
            }

            if (HandleMovement(keyboard)) return;
            if (HandleCommands(keyboard)) return;
        }

        // ---------------------------------------------------------------- input

        bool HandleMovement(Keyboard keyboard)
        {
            Vec2I direction = ReadDirection(keyboard);

            if (direction == Vec2I.Zero)
            {
                _heldDirection = Vec2I.Zero;
                _heldTime = 0f;
                return false;
            }

            if (direction != _heldDirection)
            {
                // A fresh press always resolves immediately; holding repeats after a short delay.
                _heldDirection = direction;
                _heldTime = 0f;
                _nextRepeat = repeatDelay;
                Step(direction);
                return true;
            }

            _heldTime += Time.deltaTime;
            if (_heldTime >= _nextRepeat)
            {
                _nextRepeat = _heldTime + repeatInterval;
                Step(direction);
                return true;
            }
            return false;
        }

        void Step(Vec2I direction)
        {
            if (_game.PlayerStep(direction)) Redraw();
            else _hud.Refresh();
        }

        static Vec2I ReadDirection(Keyboard keyboard)
        {
            int x = 0;
            int y = 0;

            if (Held(keyboard, Key.UpArrow, Key.W, Key.Numpad8)) y += 1;
            if (Held(keyboard, Key.DownArrow, Key.S, Key.Numpad2)) y -= 1;
            if (Held(keyboard, Key.LeftArrow, Key.A, Key.Numpad4)) x -= 1;
            if (Held(keyboard, Key.RightArrow, Key.D, Key.Numpad6)) x += 1;

            if (Held(keyboard, Key.Q, Key.Numpad7)) { x -= 1; y += 1; }
            if (Held(keyboard, Key.E, Key.Numpad9)) { x += 1; y += 1; }
            if (Held(keyboard, Key.Z, Key.Numpad1)) { x -= 1; y -= 1; }
            if (Held(keyboard, Key.C, Key.Numpad3)) { x += 1; y -= 1; }

            return new Vec2I(Mathf.Clamp(x, -1, 1), Mathf.Clamp(y, -1, 1));
        }

        static bool Held(Keyboard keyboard, Key a, Key b, Key c = Key.None)
        {
            if (keyboard[a].isPressed) return true;
            if (keyboard[b].isPressed) return true;
            return c != Key.None && keyboard[c].isPressed;
        }

        bool HandleCommands(Keyboard keyboard)
        {
            if (keyboard[Key.Space].wasPressedThisFrame || keyboard[Key.Numpad5].wasPressedThisFrame)
            {
                if (_game.PlayerWait()) Redraw();
                return true;
            }

            // '>' is shift+period on most layouts, so accept the bare period and enter too.
            if (keyboard[Key.Period].wasPressedThisFrame || keyboard[Key.Enter].wasPressedThisFrame ||
                keyboard[Key.NumpadEnter].wasPressedThisFrame)
            {
                int depthBefore = _game.Depth;
                if (_game.PlayerDescend())
                {
                    if (_game.Depth != depthBefore) _rig.SnapToPlayer();
                    ReportFloorValidation();
                    Redraw();
                }
                else _hud.Refresh();
                return true;
            }

            bool dropModifier = keyboard[Key.LeftShift].isPressed || keyboard[Key.RightShift].isPressed;
            for (int slot = 0; slot < 10; slot++)
            {
                Key key = slot == 9 ? Key.Digit0 : (Key)((int)Key.Digit1 + slot);
                if (!keyboard[key].wasPressedThisFrame) continue;
                bool acted = dropModifier ? _game.PlayerDropItem(slot) : _game.PlayerUseItem(slot);
                if (acted) Redraw();
                else _hud.Refresh();
                return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- diagnostics

        void ReportFloorValidation()
        {
            ValidationReport report = _game.LastValidation;
            if (report == null) return;

            if (report.IsValid) Debug.Log("[DungeonCrawler] floor " + _game.Depth + " " + report);
            else Debug.LogError("[DungeonCrawler] floor " + _game.Depth + " is not playable: " + report);
        }

        [ContextMenu("Run Self-Play Batch")]
        public void RunSelfPlay()
        {
            int batchSeed = seed != 0 ? seed : 12345;
            Debug.Log("[DungeonCrawler] self-play x" + selfPlayRuns + "\n" +
                      SelfPlay.RunBatch(batchSeed, selfPlayRuns));
        }
    }
}
