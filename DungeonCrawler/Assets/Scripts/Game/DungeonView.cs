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
        const int PixelsPerTile = 8;
        const int OrderMap = 0;
        const int OrderStairs = 6;
        const int OrderItems = 8;
        const int OrderFog = 10;
        const int OrderEnemies = 15;
        const int OrderPlayer = 20;

        GameState _game;
        int _builtFloorVersion = -1;

        SpriteRenderer _map;
        SpriteRenderer _fog;
        SpriteRenderer _player;
        Transform _entityRoot;

        Color32[] _fogPixels;
        Texture2D _fogTexture;

        readonly List<SpriteRenderer> _itemPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _enemyPool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _markerPool = new List<SpriteRenderer>();

        public void Initialize(GameState game)
        {
            _game = game;

            _map = CreateRenderer("Map", OrderMap, transform);
            _fog = CreateRenderer("Fog", OrderFog, transform);

            _entityRoot = new GameObject("Entities").transform;
            _entityRoot.SetParent(transform, false);

            _player = CreateRenderer("Player", OrderPlayer, _entityRoot);
            _player.sprite = SpriteFactory.Shape(ShapeKind.Disc);
            _player.color = Palette.Player;

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
            int w = dungeon.Width * PixelsPerTile;
            int h = dungeon.Height * PixelsPerTile;
            var pixels = new Color32[w * h];

            for (int ty = 0; ty < dungeon.Height; ty++)
                for (int tx = 0; tx < dungeon.Width; tx++)
                    PaintTile(dungeon, pixels, w, tx, ty);

            if (_map.sprite != null)
            {
                Destroy(_map.sprite.texture);
                Destroy(_map.sprite);
            }
            _map.sprite = SpriteFactory.FromPixels(pixels, w, h, PixelsPerTile, "dc_map");
            _map.transform.localPosition = Vector3.zero;

            BuildFogTexture(dungeon);
            BuildStairMarkers(dungeon);
        }

        void PaintTile(DungeonData dungeon, Color32[] pixels, int stride, int tx, int ty)
        {
            TileType tile = dungeon[tx, ty];
            Color32 baseColor;
            switch (tile)
            {
                case TileType.Wall: baseColor = Palette.WallFace; break;
                case TileType.Door: baseColor = Palette.FloorBase; break;
                default: baseColor = Palette.FloorBase; break;
            }

            bool wall = tile == TileType.Wall;
            bool capped = wall && ty + 1 < dungeon.Height && dungeon[tx, ty + 1] != TileType.Wall;
            int originX = tx * PixelsPerTile;
            int originY = ty * PixelsPerTile;

            for (int py = 0; py < PixelsPerTile; py++)
            {
                for (int px = 0; px < PixelsPerTile; px++)
                {
                    Color32 c = baseColor;

                    if (wall)
                    {
                        // A lit cap on walls that face open space reads as depth.
                        if (capped && py >= PixelsPerTile - 2) c = Palette.WallTop;
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

                    pixels[(originY + py) * stride + originX + px] = c;
                }
            }

            if (tile == TileType.Door) PaintDoor(dungeon, pixels, stride, tx, ty);
        }

        void PaintDoor(DungeonData dungeon, Color32[] pixels, int stride, int tx, int ty)
        {
            bool vertical = dungeon.BlocksSight(tx - 1, ty) && dungeon.BlocksSight(tx + 1, ty);
            int originX = tx * PixelsPerTile;
            int originY = ty * PixelsPerTile;

            for (int py = 0; py < PixelsPerTile; py++)
            {
                for (int px = 0; px < PixelsPerTile; px++)
                {
                    bool onBar = vertical
                        ? py >= 3 && py <= 4
                        : px >= 3 && px <= 4;
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
            down.sprite = SpriteFactory.Shape(ShapeKind.Stairs);
            down.color = Palette.StairsDown;
            down.transform.localPosition = TileCenter(dungeon.StairsDown);
            down.gameObject.SetActive(true);

            SpriteRenderer up = Marker(1);
            up.sprite = SpriteFactory.Shape(ShapeKind.Stairs);
            up.color = Palette.StairsUp;
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
                renderer.sprite = SpriteFactory.Shape(ShapeForItem(ground.Item.Def.Kind));
                renderer.color = Palette.ForItem(ground.Item.Def.Kind);
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
                renderer.sprite = SpriteFactory.Shape(ShapeForEnemy(enemy.Archetype));

                // Wounded monsters darken, so you can read a fight without a health bar per monster.
                float health = enemy.MaxHp <= 0 ? 1f : enemy.Hp / (float)enemy.MaxHp;
                renderer.color = Color.Lerp(Multiply(Palette.ForEnemy(enemy.Archetype), 0.45f),
                                            Palette.ForEnemy(enemy.Archetype), 0.35f + 0.65f * health);
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
