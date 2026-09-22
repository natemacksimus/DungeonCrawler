using System.Text;
using UnityEngine;
using UnityEngine.UI;
using DungeonCrawler.Core;

namespace DungeonCrawler.Game
{
    /// <summary>
    /// The whole interface, built in code at startup: stat line, HP and XP bars, message log,
    /// inventory panel, and the end-of-run summary. Text and bars only, per the spec.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        const int LogLines = 7;

        GameState _game;
        Font _font;

        Text _statsText;
        Text _logText;
        Text _inventoryText;
        Text _hintText;
        Image _hpFill;
        Image _xpFill;
        Text _hpLabel;

        GameObject _overlay;
        Text _overlayTitle;
        Text _overlayBody;

        readonly StringBuilder _builder = new StringBuilder(512);

        public void Initialize(GameState game)
        {
            _game = game;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("HUD Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            BuildTopBar(canvasGo.transform);
            BuildLogPanel(canvasGo.transform);
            BuildInventoryPanel(canvasGo.transform);
            BuildHintBar(canvasGo.transform);
            BuildOverlay(canvasGo.transform);
        }

        // ---------------------------------------------------------------- construction

        void BuildTopBar(Transform parent)
        {
            RectTransform panel = Panel(parent, "Top Bar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -88f), new Vector2(-12f, -12f));

            _statsText = Label(panel, "Stats", 22, TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(14f, 6f), new Vector2(-14f, -8f));

            RectTransform hpTrack = Bar(panel, "HP Track", Palette.HpTrack,
                new Vector2(14f, 8f), new Vector2(274f, 24f));
            _hpFill = Bar(panel, "HP Fill", Palette.HpFill, new Vector2(14f, 8f), new Vector2(274f, 24f))
                .GetComponent<Image>();

            _hpLabel = Label(hpTrack, "HP Label", 16, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform xpTrack = Bar(panel, "XP Track", Palette.HpTrack,
                new Vector2(286f, 8f), new Vector2(486f, 24f));
            _xpFill = Bar(panel, "XP Fill", Palette.XpFill, new Vector2(286f, 8f), new Vector2(486f, 24f))
                .GetComponent<Image>();
            Label(xpTrack, "XP Label", 14, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).text = "XP";
        }

        void BuildLogPanel(Transform parent)
        {
            RectTransform panel = Panel(parent, "Log Panel", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(12f, 44f), new Vector2(692f, 216f));

            _logText = Label(panel, "Log", 18, TextAnchor.LowerLeft,
                Vector2.zero, Vector2.one, new Vector2(14f, 10f), new Vector2(-14f, -10f));
        }

        void BuildInventoryPanel(Transform parent)
        {
            RectTransform panel = Panel(parent, "Inventory Panel", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-352f, 44f), new Vector2(-12f, 420f));

            _inventoryText = Label(panel, "Inventory", 18, TextAnchor.UpperLeft,
                Vector2.zero, Vector2.one, new Vector2(14f, 10f), new Vector2(-14f, -10f));
        }

        void BuildHintBar(Transform parent)
        {
            var go = new GameObject("Hints");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.offsetMin = new Vector2(12f, 8f);
            rect.offsetMax = new Vector2(-12f, 34f);

            _hintText = AddText(go, 16, TextAnchor.MiddleLeft);
            _hintText.color = Palette.HudDim;
            _hintText.text = "Move: arrows / WASD  ·  Diagonals: Q E Z C  ·  Wait: space  ·  Use item: 1-9  ·  Descend: >  ·  Restart: R";
        }

        void BuildOverlay(Transform parent)
        {
            _overlay = new GameObject("Overlay");
            _overlay.transform.SetParent(parent, false);
            var rect = _overlay.AddComponent<RectTransform>();
            Stretch(rect);

            var dim = _overlay.AddComponent<Image>();
            dim.sprite = SpriteFactory.White;
            dim.color = new Color(0.02f, 0.03f, 0.05f, 0.86f);

            RectTransform box = Panel(_overlay.transform, "Summary", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300f, -190f), new Vector2(300f, 190f));

            _overlayTitle = Label(box, "Title", 40, TextAnchor.UpperCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -78f), new Vector2(-16f, -18f));

            _overlayBody = Label(box, "Body", 22, TextAnchor.UpperCenter,
                Vector2.zero, Vector2.one, new Vector2(16f, 16f), new Vector2(-16f, -84f));

            _overlay.SetActive(false);
        }

        // ---------------------------------------------------------------- per-turn refresh

        public void Refresh()
        {
            if (_game == null) return;

            Player player = _game.Player;

            _builder.Length = 0;
            _builder.Append("FLOOR ").Append(_game.Depth).Append(" / ").Append(_game.MaxDepth);
            _builder.Append("     LEVEL ").Append(player.Level);
            _builder.Append("     ATK ").Append(player.TotalAttack);
            _builder.Append("     DEF ").Append(player.TotalDefense);
            _builder.Append("     GOLD ").Append(player.Gold);
            _builder.Append("     TURN ").Append(_game.Turn);
            if (player.AttackBuffTurns > 0) _builder.Append("     MIGHT ").Append(player.AttackBuffTurns);
            if (player.DefenseBuffTurns > 0) _builder.Append("     WARD ").Append(player.DefenseBuffTurns);
            _statsText.text = _builder.ToString();

            float hp = player.MaxHp <= 0 ? 0f : Mathf.Clamp01(player.Hp / (float)player.MaxHp);
            _hpFill.rectTransform.localScale = new Vector3(hp, 1f, 1f);
            _hpLabel.text = "HP " + player.Hp + " / " + player.MaxHp;

            float xp = player.XpToNextLevel <= 0 ? 0f : Mathf.Clamp01(player.Xp / (float)player.XpToNextLevel);
            _xpFill.rectTransform.localScale = new Vector3(xp, 1f, 1f);

            _builder.Length = 0;
            foreach (string line in _game.Log.Tail(LogLines))
            {
                if (_builder.Length > 0) _builder.Append('\n');
                _builder.Append(line);
            }
            _logText.text = _builder.ToString();

            _inventoryText.text = BuildInventoryText(player);

            bool over = _game.Status != GameStatus.Playing;
            if (_overlay.activeSelf != over) _overlay.SetActive(over);
            if (over)
            {
                _overlayTitle.text = _game.Status == GameStatus.Victory ? "YOU ESCAPED" : "YOU DIED";
                _overlayTitle.color = _game.Status == GameStatus.Victory ? Palette.Gold : Palette.HpFill;
                _overlayBody.text = SummaryBody();
            }
        }

        string BuildInventoryText(Player player)
        {
            _builder.Length = 0;
            _builder.Append("PACK  ").Append(player.Pack.Count).Append('/').Append(player.Pack.Capacity).Append('\n');

            if (player.Pack.Count == 0) _builder.Append("  (empty)\n");
            for (int i = 0; i < player.Pack.Count; i++)
            {
                int key = (i + 1) % 10;
                _builder.Append("  ").Append(key).Append(". ").Append(player.Pack[i].Describe()).Append('\n');
            }

            _builder.Append('\n').Append("EQUIPPED\n");
            _builder.Append("  weapon: ").Append(player.Weapon == null ? "fists" : player.Weapon.Describe()).Append('\n');
            _builder.Append("  armor:  ").Append(player.Armor == null ? "rags" : player.Armor.Describe());
            return _builder.ToString();
        }

        string SummaryBody()
        {
            RunStats stats = _game.Stats;
            _builder.Length = 0;
            if (_game.Status == GameStatus.Dead && !string.IsNullOrEmpty(stats.KilledBy))
                _builder.Append("Killed by ").Append(Phrasing.WithArticle(stats.KilledBy))
                        .Append(" on floor ").Append(_game.Depth).Append(".\n\n");
            else if (_game.Status == GameStatus.Victory)
                _builder.Append("You carried ").Append(stats.Gold).Append(" gold into the daylight.\n\n");

            _builder.Append("Deepest floor: ").Append(stats.DeepestDepth).Append(" / ").Append(_game.MaxDepth).Append('\n');
            _builder.Append("Character level: ").Append(stats.Level).Append('\n');
            _builder.Append("Monsters slain: ").Append(stats.Kills).Append('\n');
            _builder.Append("Items used: ").Append(stats.ItemsUsed).Append('\n');
            _builder.Append("Gold collected: ").Append(stats.Gold).Append('\n');
            _builder.Append("Turns taken: ").Append(stats.Turns).Append("\n\n");
            _builder.Append("Press R to crawl again.");
            return _builder.ToString();
        }

        // ---------------------------------------------------------------- UI plumbing

        RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var image = go.AddComponent<Image>();
            image.sprite = SpriteFactory.White;
            image.color = Palette.HudPanel;
            return rect;
        }

        RectTransform Bar(Transform parent, string name, Color color, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            // Left-edge pivot must be set before the offsets, so scaling a fill grows rightwards
            // from where the bar starts rather than shifting the whole rect.
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var image = go.AddComponent<Image>();
            image.sprite = SpriteFactory.White;
            image.color = color;
            return rect;
        }

        Text Label(Transform parent, string name, int fontSize, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return AddText(go, fontSize, anchor);
        }

        Text AddText(GameObject go, int fontSize, TextAnchor anchor)
        {
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Palette.HudText;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
