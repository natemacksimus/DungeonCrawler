using UnityEngine;
using DungeonCrawler.Core;

namespace DungeonCrawler.Game
{
    /// <summary>
    /// Orthographic follow camera. It eases toward the player and stays inside the floor bounds, so
    /// the view never shows the void past the map edge.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        /// <summary>How many tiles tall the view is.</summary>
        public float ViewHeightTiles = 26f;

        /// <summary>Tiles per second of catch-up. High enough that turn-based play still feels instant.</summary>
        public float FollowSpeed = 14f;

        Camera _camera;
        GameState _game;

        public void Initialize(Camera camera, GameState game)
        {
            _camera = camera;
            _game = game;

            _camera.orthographic = true;
            _camera.orthographicSize = ViewHeightTiles * 0.5f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Palette.Background;

            // The first floor does not exist yet at initialise time; the bootstrap snaps us once it does.
            SnapToPlayer();
        }

        public void SnapToPlayer()
        {
            if (_camera == null || _game == null || _game.Dungeon == null) return;
            _camera.transform.position = DesiredPosition();
        }

        void LateUpdate()
        {
            if (_camera == null || _game == null || _game.Dungeon == null) return;

            Vector3 target = DesiredPosition();
            Vector3 current = _camera.transform.position;
            _camera.transform.position = Vector3.Lerp(current, target, 1f - Mathf.Exp(-FollowSpeed * Time.deltaTime));
        }

        Vector3 DesiredPosition()
        {
            Vector3 focus = DungeonView.TileCenter(_game.Player.Position);

            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;
            DungeonData dungeon = _game.Dungeon;

            float x = ClampAxis(focus.x, halfWidth, dungeon.Width);
            float y = ClampAxis(focus.y, halfHeight, dungeon.Height);
            return new Vector3(x, y, -10f);
        }

        static float ClampAxis(float value, float halfExtent, float mapExtent)
        {
            // Centre the map on any axis it cannot fill.
            if (mapExtent <= halfExtent * 2f) return mapExtent * 0.5f;
            return Mathf.Clamp(value, halfExtent, mapExtent - halfExtent);
        }
    }
}
