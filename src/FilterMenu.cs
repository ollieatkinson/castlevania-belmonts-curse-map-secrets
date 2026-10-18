using BepInEx.Configuration;
using RedWine.ResourceManagement;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MapAssist;

// A separate canvas keeps the game's map navigation and selectable list intact.
// Font and checkbox sprites are borrowed from installed game assets, never copied.
internal sealed class FilterMenu
{
    private static readonly Color Gold = new(0.914f, 0.8f, 0.518f, 1f);
    private static readonly Color Parchment = new(0.85f, 0.79f, 0.67f, 1f);
    private readonly GameObject root;
    private readonly RectTransform panel;
    private readonly GameObject[] pages = new GameObject[3];
    private readonly List<(TextMeshProUGUI Label, int Index)> tabs = new();
    private readonly MarkerIcons icons;
    private int page;
    private readonly TextMeshProUGUI sizeLabel;
    private readonly TextMeshProUGUI summary;
    private readonly TextMeshProUGUI expandHint;
    private readonly List<(ConfigEntry<bool> Setting, Image Check, MarkerKind? Kind, TextMeshProUGUI? Count)> rows = new();
    private readonly TMP_FontAsset font;
    private readonly Sprite checkbox = null!;
    private readonly Sprite checkmark = null!;
    private readonly Action changed;

    internal FilterMenu(Transform owner, Action onChanged, MarkerIcons markerIcons)
    {
        changed = onChanged;
        icons = markerIcons;
        var template = ResourceManager.LoadAssetByRef<GameObject>(ResourceContext.Always,
            new AssetReference("17b199ad7998c5b4fb66a03f1d8c8a14"));
        font = ResourceManager.LoadAssetByRef<TMP_FontAsset>(ResourceContext.Always,
            new AssetReference("015129531cf17024cbbd13de5ca7e951"));
        if (!template || !font) throw new InvalidOperationException("Game menu artwork or font is unavailable.");
        foreach (var image in template.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name == "IconContainer" && image.transform.parent.name == "Checkbox") checkbox = image.sprite;
            if (image.gameObject.name == "Checkmark") checkmark = image.sprite;
        }
        if (!checkbox || !checkmark) throw new InvalidOperationException("Game checkbox artwork is unavailable.");

        root = new GameObject("Map Secrets filters");
        root.SetActive(false);
        root.transform.SetParent(owner, false);
        try
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            root.AddComponent<GraphicRaycaster>();
            panel = Rect(root.transform, "Panel", 24, 62, 410, 532);
            panel.anchorMin = panel.anchorMax = new Vector2(0, 0);
            panel.pivot = new Vector2(0, 0);
            panel.anchoredPosition = new Vector2(24, 62);
            Image(panel, new Color(0.035f, 0.028f, 0.018f, 0.96f));
            // Fine gold rules echo the map's frame without obscuring more map area.
            Rule(panel, 0, 0, 410, 1);
            var left = Rule(panel, 0, 0, 1, 532);
            var right = Rule(panel, 409, 0, 1, 532);
            right.anchorMin = new Vector2(1, 0); right.anchorMax = new Vector2(1, 1);
            right.offsetMin = new Vector2(-1, 0); right.offsetMax = Vector2.zero;
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0, 1);
            left.offsetMin = Vector2.zero; left.offsetMax = new Vector2(1, 0);
            var bottom = Rule(panel, 0, 0, 410, 1);
            bottom.anchorMin = bottom.anchorMax = Vector2.zero;
            bottom.pivot = Vector2.zero; bottom.anchoredPosition = Vector2.zero;
            var header = Button(panel, "Filters", 10, 8, 390, 38, () => Plugin.PanelExpanded.Value = !Plugin.PanelExpanded.Value);
            Text(header, "MAP SECRETS", 12, 1, 260, 36, 27, Gold).characterSpacing = 2f;
            expandHint = Text(header, "F5", 324, 5, 54, 28, 18, Gold);
            expandHint.alignment = TextAlignmentOptions.MidlineRight;
            summary = Text(panel, "", 22, 51, 366, 26, 19, Parchment);
            for (var i = 0; i < pages.Length; i++)
            {
                var index = i;
                pages[i] = Rect(panel, "Page " + i, 0, 0, 410, 532).gameObject;
                var tab = Button(panel, "Tab " + i, 16 + i * 126, 84, 124, 32, () => page = index);
                var label = Text(tab, new[] { "Items", "Exploration", "Options" }[i], 0, 0, 124, 32, 21, Gold);
                label.alignment = TextAlignmentOptions.Midline;
                tabs.Add((label, i));
            }
            for (var i = 0; i < Categories.All.Length; i++)
            {
                var entry = Categories.All[i];
                var which = i < 10 ? 0 : 1;
                Row(pages[which].transform, 122 + (which == 0 ? i : i - 10) * 34, Categories.Settings[entry.Kind], entry.Label, entry.Key, entry.Kind);
            }
            var options = pages[2].transform;
            Row(options, 126, Plugin.Enabled, "Show markers", "F6");
            Row(options, 162, Plugin.HideCollected, "Hide collected / found", "");
            Row(options, 198, Plugin.ShowUnexplored, "Unexplored rooms", "");
            Row(options, 234, Plugin.Unverified, "Unverified locations", "");
            Row(options, 270, Plugin.RoomBell, "First-visit bell", "");
            Row(options, 306, Plugin.HoverLabels, "Marker tooltips", "");
            sizeLabel = Text(options, "", 24, 350, 260, 32, 21, Parchment);
            var smaller = Button(options, "Smaller markers", 294, 350, 40, 32, () => Plugin.IconSize.Value = Mathf.Max(12, Plugin.IconSize.Value - 4));
            Text(smaller, "-", 0, 0, 40, 32, 25, Gold).alignment = TextAlignmentOptions.Midline;
            var bigger = Button(options, "Larger markers", 344, 350, 40, 32, () => Plugin.IconSize.Value = Mathf.Min(80, Plugin.IconSize.Value + 4));
            Text(bigger, "+", 0, 0, 40, 32, 25, Gold).alignment = TextAlignmentOptions.Midline;
            Text(options, "Bell sounds only for enabled categories.", 24, 390, 360, 26, 18, Parchment);
            Text(options, "Unverified locations may remain after discovery.", 24, 420, 360, 48, 17, Parchment).enableWordWrapping = true;
            for (var i = 0; i < pages.Length; i++)
            {
                Rule(pages[i].transform, 18, 476, 374, 1);
                var reload = Button(pages[i].transform, "Reload", 14, 488, 382, 32, () => { Plugin.Enabled.ConfigFile.Reload(); changed(); });
                Text(reload, "Reload configuration", 8, 0, 366, 32, 20, Gold).alignment = TextAlignmentOptions.Midline;
            }
            Plugin.Logger.LogInfo("Map filters use the game's Latin font and settings checkbox artwork.");
        }
        catch { Object.Destroy(root); throw; }
    }

    internal void Refresh(bool visible, IReadOnlyDictionary<MarkerKind, int> counts)
    {
        if (!root) return;
        root.SetActive(visible);
        if (!visible) return;
        var expanded = Plugin.PanelExpanded.Value;
        panel.sizeDelta = new Vector2(410, expanded ? 532 : 92);
        for (var i = 0; i < pages.Length; i++) pages[i].SetActive(expanded && page == i);
        foreach (var tab in tabs)
        {
            tab.Label.transform.parent.gameObject.SetActive(expanded);
            tab.Label.color = tab.Index == page ? Gold : Parchment;
        }
        summary.text = $"{counts.Values.Sum()} visible map markers";
        expandHint.text = expanded ? "- F5" : "+ F5";
        sizeLabel.text = $"Marker size: {Plugin.IconSize.Value}";
        foreach (var row in rows)
        {
            row.Check.enabled = row.Setting.Value;
            if (row.Count && row.Kind.HasValue) row.Count!.text = counts.TryGetValue(row.Kind.Value, out var count) ? count.ToString() : "0";
        }
    }

    internal bool ContainsScreenPoint(Vector2 point) => root && root.activeInHierarchy
        && RectTransformUtility.RectangleContainsScreenPoint(panel, point, null);

    internal void Dispose() { if (root) Object.Destroy(root); }

    private void Row(Transform parent, float y, ConfigEntry<bool> setting, string label, string key, MarkerKind? kind = null)
    {
        var row = Button(parent, label, 12, y, 386, 32, () => { setting.Value = !setting.Value; changed(); });
        var frame = Image(Rect(row, "Checkbox", 6, 2, 28, 28), Gold);
        frame.sprite = checkbox;
        frame.type = UnityEngine.UI.Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 16f;
        var tick = Image(Rect(row, "Checkmark", 7, 3, 26, 26), Gold);
        tick.sprite = checkmark; tick.preserveAspect = true;
        if (kind.HasValue)
        {
            try
            {
                foreach (var sprite in icons.Get(kind.Value))
                {
                    var image = Image(Rect(row, "Category icon", 42, 0, 32, 32), Color.white);
                    image.sprite = sprite; image.preserveAspect = true;
                }
            }
            catch (Exception e) { Plugin.Logger.LogWarning($"Could not draw category icon: {e.Message}"); }
        }
        Text(row, label, kind.HasValue ? 84 : 46, 0, kind.HasValue ? 212 : 284, 32, 21, Parchment);
        TextMeshProUGUI? countLabel = null;
        if (kind.HasValue) countLabel = Text(row, "0", 300, 1, 32, 30, 19, Parchment);
        Text(row, key, 336, 2, 38, 28, 17, Gold).alignment = TextAlignmentOptions.MidlineRight;
        rows.Add((setting, tick, kind, countLabel));
    }

    private RectTransform Button(Transform parent, string name, float x, float y, float width, float height, Action action)
    {
        var rect = Rect(parent, name, x, y, width, height);
        var background = Image(rect, Color.white);
        background.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.normalColor = new Color(Gold.r, Gold.g, Gold.b, 0f);
        colors.highlightedColor = new Color(Gold.r, Gold.g, Gold.b, 0.14f);
        colors.pressedColor = new Color(Gold.r, Gold.g, Gold.b, 0.26f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        button.onClick.AddListener((UnityAction)action);
        return rect;
    }

    private TextMeshProUGUI Text(Transform parent, string value, float x, float y, float width, float height, float size, Color color)
    {
        var label = Rect(parent, "Text", x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSharedMaterial = font.material;
        label.fontSize = size;
        label.color = color;
        label.text = value;
        label.richText = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static Image Image(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return image;
    }

    private static RectTransform Rule(Transform parent, float x, float y, float width, float height)
    {
        var rect = Rect(parent, "Gold rule", x, y, width, height);
        Image(rect, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
        return rect;
    }
}
