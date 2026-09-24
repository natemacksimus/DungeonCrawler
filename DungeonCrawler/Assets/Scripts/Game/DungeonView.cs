using System.Collections.Generic;
using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Game
{
    /// <summary>
    /// Draws the dungeon. The map is one generated texture rebuilt per floor, fog of war is a second
    /// one-pixel-per-tile texture rebuilt per turn, and actors are pooled sprite renderers. That keeps
    /// a 60x45 floor at a handful of draw calls with no art assets and no tilemap setup.
    /// </summary>
    public sealed class DungeonView : MonoBehaviour
    {
        const int DefaultPixelsPerTile = 8;

        /// <summary>Ceiling on the auto-matched resolution below, so a huge override texture can't blow up the baked map.</summary>
        const int MaxPixelsPerTile = 64;

        const int OrderMap = 0;
        const int OrderStairs = 6;
        const int OrderItems = 8;
        const int OrderFog = 10;
        const int OrderEnemies = 15;
        const int OrderPlayer = 20;

        GameState _game;
        VisualOverrides _overrides;
        int _builtFloorVersion = -1;

        /// <summary>
        /// Pixels baked per tile in the map texture. Matches the largest tile override texture (so
        /// custom art renders 1:1 instead of being resized), or <see cref="DefaultPixelsPerTile"/> when
        /// no tile art is wired in.
        /// </summary>
        int _pixelsPerTile = DefaultPixelsPerTile;

        SpriteRenderer _map;
        SpriteRenderer _fog;
        SpriteRenderer _player;
        Transform _entityRoot;

        Color32[] _fogPixels;
        Texture2D _fogTexture;

        readonly List<SpriteRenderer> _itemPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _enemyPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _markerPool = new List<SpriteRenderer>();

        readonly HashSet<Texture2D> _warnedUnreadable = new HashSet<Texture2D>();

        public void Initialize(GameState game, VisualOverrides overrides = null)
        {
            _game = game;
            _overrides = overrides;
            _pixelsPerTile = ResolvePixelsPerTile(overrides);

            _map = CreateRenderer("Map", OrderMap, transform);
            _fog = CreateRenderer("Fog", OrderFog, transform);

            _entityRoot = new GameObject("Entities").transform;
            _entityRoot.SetParent(transform, false);

            _player = CreateRenderer("Player", OrderPlayer, _entityRoot);
            ApplyVisual(_player, _overrides != null ? _overrides.PlayerSprite : null, ShapeKind.Disc, Palette.Player);

            _builtFloorVersion = -1;
        }

        /// <summary>Syncs the whole view to the current simulation state. Cheap enough to call per turn.</summary>
        public void Refresh()
        {
            if (_game == null || _game.Dungeon == null) return;

            if (_builtFloorVersion != _game.FloorVersion)
            {
                BuildFloor();
                _builtFloorVersion = _game.FloorVersion;
            }

            UpdateFog();
            UpdateEntities();
        }

        // ---------------------------------------------------------------- floor geometry

        void BuildFloor()
        {
            DungeonData dungeon = _game.Dungeon;
            int w = dungeon.Width * _pixelsPerTile;
            int h = dungeon.Height * _pixelsPerTile;
            var pixels = new Color32[w * h];

            for (int ty = 0; ty < dungeon.Height; ty++)
                for (int tx = 0; tx < dungeon.Width; tx++)
                    PaintTile(dungeon, pixels, w, tx, ty);

            if (_map.sprite != null)
            {
                Destroy(_map.sprite.texture);
                Destroy(_map.sprite);
            }
            _map.sprite = SpriteFactory.FromPixels(pixels, w, h, _pixelsPerTile, "dc_map");
            _map.transform.localPosition = Vector3.zero;

            BuildFogTexture(dungeon);
            BuildStairMarkers(dungeon);
        }

        void PaintTile(DungeonData dungeon, Color32[] pixels, int stride, int tx, int ty)
        {
            TileType tile = dungeon[tx, ty];
            Texture2D custom = _overrides != null ? _overrides.TextureForTile(tile) : null;
            if (custom != null && !EnsureReadable(custom)) custom = null;

            Color32 baseColor;
            switch (tile)
            {
                case TileType.Wall: baseColor = Palette.WallFace; break;
                case TileType.Door: baseColor = Palette.FloorBase; break;
                default: baseColor = Palette.FloorBase; break;
            }

            bool wall = tile == TileType.Wall;
            bool capped = wall && ty + 1 < dungeon.Height && dungeon[tx, ty + 1] != TileType.Wall;
            int originX = tx * _pixelsPerTile;
            int originY = ty * _pixelsPerTile;
            int capThickness = Mathf.Max(1, _pixelsPerTile / 4);

            for (int py = 0; py < _pixelsPerTile; py++)
            {
                for (int px = 0; px < _pixelsPerTile; px++)
                {
                    Color32 c;

                    if (custom != null)
                    {
                        c = SampleTile(custom, px, py);
                    }
                    else
                    {
                        c = baseColor;

                        if (wall)
                        {
                            // A lit cap on walls that face open space reads as depth.
                            if (capped && py >= _pixelsPerTile - capThickness) c = Palette.WallTop;
                            else if (py == 0) c = Multiply(baseColor, 0.72f);
                        }
                        else
                        {
                            // Deterministic speckle so floors have texture without looking noisy.
                            int hash = (tx * 73856093) ^ (ty * 19349663) ^ (px * 83492791) ^ (py * 1500450271);
                            if ((hash & 31) == 0) c = Palette.FloorSpeck;

                            bool edge = px == 0 || py == 0;
                            if (edge && (dungeon.IsWalkable(tx - 1, ty) || dungeon.IsWalkable(tx, ty - 1)))
                                c = Multiply(c, 0.88f);
                        }
                    }

                    pixels[(originY + py) * stride + originX + px] = c;
                }
            }

            if (tile == TileType.Door && custom == null) PaintDoor(dungeon, pixels, stride, tx, ty);
        }

        /// <summary>
        /// Guards against <see cref="Texture2D.GetPixelBilinear"/> throwing on a texture that has not
        /// been imported with "Read/Write Enabled". Falls back to the default tile art instead, once,
        /// with a warning explaining the fix.
        /// </summary>
        bool EnsureReadable(Texture2D texture)
        {
            if (texture.isReadable) return true;
            if (_warnedUnreadable.Add(texture))
            {
                Debug.LogWarning("[DungeonCrawler] '" + texture.name + "' is not Read/Write Enabled, so it " +
                                  "can't be sampled for tile art. Falling back to the default look. Fix: select " +
                                  "the texture, then in Import Settings enable Read/Write, and Apply.");
            }
            return false;
        }

        /// <summary>
        /// Nearest-neighbour lookup rather than <see cref="Texture2D.GetPixelBilinear"/>, so pixel art
        /// stays crisp: a texture at exactly <see cref="_pixelsPerTile"/> resolution reproduces every
        /// source pixel 1:1, and a smaller one block-scales up instead of blurring.
        /// </summary>
        Color32 SampleTile(Texture2D source, int px, int py)
        {
            int sx = Mathf.Min(source.width - 1, px * source.width / _pixelsPerTile);
            int sy = Mathf.Min(source.height - 1, py * source.height / _pixelsPerTile);
            return source.GetPixel(sx, sy);
        }

        static int ResolvePixelsPerTile(VisualOverrides overrides)
        {
            if (overrides == null) return DefaultPixelsPerTile;

            int size = DefaultPixelsPerTile;
            size = Mathf.Max(size, LargestDimension(overrides.WallTexture));
            size = Mathf.Max(size, LargestDimension(overrides.FloorTexture));
            size = Mathf.Max(size, LargestDimension(overrides.DoorTexture));
            return Mathf.Min(size, MaxPixelsPerTile);
        }

        static int LargestDimension(Texture2D texture)
        {
            return texture == null ? 0 : Mathf.Max(texture.width, texture.height);
        }

        void PaintDoor(DungeonData dungeon, Color32[] pixels, int stride, int tx, int ty)
        {
            bool vertical = dungeon.BlocksSight(tx - 1, ty) && dungeon.BlocksSight(tx + 1, ty);
            int originX = tx * _pixelsPerTile;
            int originY = ty * _pixelsPerTile;
            int barLow = _pixelsPerTile / 2 - 1;
            int barHigh = _pixelsPerTile / 2;

            for (int py = 0; py < _pixelsPerTile; py++)
            {
                for (int px = 0; px < _pixelsPerTile; px++)
                {
                    bool onBar = vertical
                        ? py >= barLow && py <= barHigh
                        : px >= barLow && px <= barHigh;
                    if (!onBar) continue;
                    pixels[(originY + py) * stride + originX + px] = Palette.DoorWood;
                }
            }
        }

        void BuildFogTexture(DungeonData dungeon)
        {
            if (_fog.sprite != null)
            {
                Destroy(_fog.sprite.texture);
                Destroy(_fog.sprite);
            }

            _fogPixels = new Color32[dungeon.Width * dungeon.Height];
            _fogTexture = SpriteFactory.NewTexture(dungeon.Width, dungeon.Height, "dc_fog");
            var sprite = Sprite.Create(_fogTexture, new Rect(0, 0, dungeon.Width, dungeon.Height), Vector2.zero, 1f);
            sprite.name = "dc_fog";
            _fog.sprite = sprite;
            _fog.transform.localPosition = Vector3.zero;
        }

        void BuildStairMarkers(DungeonData dungeon)
        {
            for (int i = 0; i < _markerPool.Count; i++) _markerPool[i].gameObject.SetActive(false);

            SpriteRenderer down = Marker(0);
            ApplyVisual(down, _overrides != null ? _overrides.StairsDownSprite : null, ShapeKind.Stairs, Palette.StairsDown);
            down.transform.localPosition = TileCenter(dungeon.StairsDown);
            down.gameObject.SetActive(true);

            SpriteRenderer up = Marker(1);
            ApplyVisual(up, _overrides != null ? _overrides.StairsUpSprite : null, ShapeKind.Stairs, Palette.StairsUp);
            up.transform.localPosition = TileCenter(dungeon.StairsUp);
            up.gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- per-turn updates

        void UpdateFog()
        {
            DungeonData dungeon = _game.Dungeon;
            Color32 unseen = Palette.Background;
            unseen.a = 255;
            Color32 remembered = Palette.Background;
            remembered.a = 165;
            Color32 lit = Palette.Background;
            lit.a = 0;

            for (int y = 0; y < dungeon.Height; y++)
            {
                int row = y * dungeon.Width;
                for (int x = 0; x < dungeon.Width; x++)
                {
                    Color32 c = _game.Visible[x, y] ? lit : (_game.Explored[x, y] ? remembered : unseen);
                    _fogPixels[row + x] = c;
                }
            }

            _fogTexture.SetPixels32(_fogPixels);
            _fogTexture.Apply(false);
        }

        void UpdateEntities()
        {
            _player.transform.localPosition = TileCenter(_game.Player.Position);
            _player.gameObject.SetActive(_game.Player.IsAlive);

            int itemIndex = 0;
            for (int i = 0; i < _game.Ground.Count; i++)
            {
                GroundItem ground = _game.Ground[i];
                // Items stay drawn once seen; the fog layer dims remembered ones for us.
                if (!_game.Explored[ground.Position.X, ground.Position.Y]) continue;

                SpriteRenderer renderer = Pooled(_itemPool, itemIndex++, "Item", OrderItems);
                Sprite customItem = _overrides != null ? _overrides.SpriteForItem(ground.Item.Def.Kind) : null;
                ApplyVisual(renderer, customItem, ShapeForItem(ground.Item.Def.Kind), Palette.ForItem(ground.Item.Def.Kind));
                renderer.transform.localPosition = TileCenter(ground.Position);
                renderer.gameObject.SetActive(true);
            }
            Hide(_itemPool, itemIndex);

            int enemyIndex = 0;
            for (int i = 0; i < _game.Enemies.Count; i++)
            {
                Enemy enemy = _game.Enemies[i];
                if (!_game.IsVisible(enemy.Position)) continue;

                SpriteRenderer renderer = Pooled(_enemyPool, enemyIndex++, "Enemy", OrderEnemies);
                Sprite customEnemy = _overrides != null ? _overrides.SpriteForEnemy(enemy.Archetype) : null;
                renderer.sprite = customEnemy != null ? customEnemy : SpriteFactory.Shape(ShapeForEnemy(enemy.Archetype));

                // Wounded monsters darken, so you can read a fight without a health bar per monster.
                // Custom art keeps this: it dims toward grey rather than being recoloured.
                Color32 baseColor = customEnemy != null ? (Color32)Color.white : Palette.ForEnemy(enemy.Archetype);
                float health = enemy.MaxHp <= 0 ? 1f : enemy.Hp / (float)enemy.MaxHp;
                renderer.color = Color.Lerp(Multiply(baseColor, 0.45f), baseColor, 0.35f + 0.65f * health);
                renderer.transform.localPosition = TileCenter(enemy.Position);
                renderer.gameObject.SetActive(true);
            }
            Hide(_enemyPool, enemyIndex);
        }

        static ShapeKind ShapeForItem(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return ShapeKind.Flask;
                case ItemKind.Scroll: return ShapeKind.Scroll;
                case ItemKind.Weapon: return ShapeKind.Blade;
                case ItemKind.Armor: return ShapeKind.Shield;
                default: return ShapeKind.Coin;
            }
        }

        static ShapeKind ShapeForEnemy(EnemyArchetype archetype)
        {
            if (archetype == EnemyCatalog.GiantRat) return ShapeKind.Coin;
            if (archetype == EnemyCatalog.Goblin) return ShapeKind.Disc;
            if (archetype == EnemyCatalog.KoboldThief) return ShapeKind.Diamond;
            if (archetype == EnemyCatalog.SkeletonArcher) return ShapeKind.Triangle;
            if (archetype == EnemyCatalog.OrcBrute) return ShapeKind.Square;
            return ShapeKind.Disc;
        }

        // ---------------------------------------------------------------- helpers

        public static Vector3 TileCenter(Vec2I tile)
        {
            return new Vector3(tile.X + 0.5f, tile.Y + 0.5f, 0f);
        }

        SpriteRenderer Marker(int index)
        {
            return Pooled(_markerPool, index, "Marker", OrderStairs);
        }

        SpriteRenderer Pooled(List<SpriteRenderer> pool, int index, string label, int order)
        {
            while (pool.Count <= index)
            {
                SpriteRenderer created = CreateRenderer(label + " " + pool.Count, order, _entityRoot);
                created.gameObject.SetActive(false);
                pool.Add(created);
            }
            return pool[index];
        }

        static void Hide(List<SpriteRenderer> pool, int fromIndex)
        {
            for (int i = fromIndex; i < pool.Count; i++)
            {
                if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Uses a custom sprite at full colour when one is wired in, else the procedural default.</summary>
        static void ApplyVisual(SpriteRenderer renderer, Sprite custom, ShapeKind fallbackShape, Color fallbackColor)
        {
            renderer.sprite = custom != null ? custom : SpriteFactory.Shape(fallbackShape);
            renderer.color = custom != null ? Color.white : fallbackColor;
        }

        static SpriteRenderer CreateRenderer(string name, int sortingOrder, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = SpriteFactory.UnlitMaterial;
            return renderer;
        }

        static Color32 Multiply(Color32 color, float factor)
        {
            return new Color32(
                (byte)Mathf.Clamp(color.r * factor, 0f, 255f),
                (byte)Mathf.Clamp(color.g * factor, 0f, 255f),
                (byte)Mathf.Clamp(color.b * factor, 0f, 255f),
                color.a);
        }
    }
}
