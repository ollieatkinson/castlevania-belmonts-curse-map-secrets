using RedWine.ResourceManagement;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MapAssist;

// Screen-space labels use the same installed font and colours as the filters.
// No selectable map items, colliders, raycasters or personal marker slots are added.
internal sealed class MarkerTooltip
{
    private static readonly Color Gold = new(0.914f, 0.8f, 0.518f, 1f);
    private readonly GameObject root;
    private readonly RectTransform canvasRect;
    private readonly RectTransform card;
    private readonly TextMeshProUGUI title;
    private readonly TextMeshProUGUI category;
    private string currentTitle = "";
    private string currentCategory = "";

    internal MarkerTooltip(Transform owner)
    {
        var font = ResourceManager.LoadAssetByRef<TMP_FontAsset>(ResourceContext.Always,
            new AssetReference("015129531cf17024cbbd13de5ca7e951"));
        if (!font) throw new InvalidOperationException("The game's map font is unavailable.");
        root = new GameObject("Map Secrets hover label");
        root.SetActive(false);
        root.transform.SetParent(owner, false);
        try
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31999; // Below the filter panel.
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            canvasRect = root.GetComponent<RectTransform>();
            card = Rect(root.transform, "Label", 0, 0, 240, 88);
            card.anchorMin = card.anchorMax = Vector2.zero;
            card.pivot = Vector2.zero;
            Image(card, new Color(0.035f, 0.028f, 0.018f, 0.96f));
            Border(card, true, false); Border(card, true, true);
            Border(card, false, false); Border(card, false, true);
            title = Text(card, font, 16, 10, 372, 34, 24, Gold);
            title.enableWordWrapping = true;
            title.maxVisibleLines = 3;
            category = Text(card, font, 16, 46, 372, 24, 18, new Color(0.85f, 0.79f, 0.67f, 1f));
        }
        catch { Object.Destroy(root); throw; }
    }

    internal void Show(string name, string kind, Vector2 screenPosition)
    {
        if (!root) return;
        root.SetActive(true);
        if (currentTitle != name || currentCategory != kind)
        {
            currentTitle = name; currentCategory = kind;
            title.text = name;
            category.text = kind;
            var preferred = title.GetPreferredValues(name, 372, Mathf.Infinity);
            var subtitle = category.GetPreferredValues(kind, 372, Mathf.Infinity);
            var width = Mathf.Clamp(Mathf.Max(preferred.x, subtitle.x) + 32, 180, 404);
            var height = Mathf.Clamp(preferred.y + 4, 34, 92);
            title.rectTransform.sizeDelta = new Vector2(width - 32, height);
            category.rectTransform.sizeDelta = new Vector2(width - 32, 24);
            category.rectTransform.anchoredPosition = new Vector2(16, -height - 14);
            card.sizeDelta = new Vector2(width, height + 50);
        }
        // CanvasScaler handles ultrawide/resolution changes. Flip beside screen edges.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out var local)) { Hide(); return; }
        var bounds = canvasRect.rect;
        var anchor = local - bounds.min;
        var size = card.sizeDelta;
        var x = anchor.x + 24;
        var y = anchor.y + 20;
        if (x + size.x > bounds.width - 12) x = anchor.x - size.x - 24;
        if (y + size.y > bounds.height - 12) y = anchor.y - size.y - 20;
        card.anchoredPosition = new Vector2(Mathf.Clamp(x, 12, Mathf.Max(12, bounds.width - size.x - 12)),
            Mathf.Clamp(y, 12, Mathf.Max(12, bounds.height - size.y - 12)));
    }

    internal void Hide() { if (root) root.SetActive(false); }
    internal void Dispose() { if (root) Object.Destroy(root); }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = new GameObject(name).AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static void Border(RectTransform parent, bool horizontal, bool far)
    {
        var edge = Rect(parent, "Gold border", 0, 0, 1, 1);
        edge.anchorMin = horizontal ? new Vector2(0, far ? 1 : 0) : new Vector2(far ? 1 : 0, 0);
        edge.anchorMax = horizontal ? new Vector2(1, far ? 1 : 0) : new Vector2(far ? 1 : 0, 1);
        edge.offsetMin = Vector2.zero;
        edge.offsetMax = horizontal ? new Vector2(0, 1) : new Vector2(1, 0);
        Image(edge, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
    }

    private static void Image(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static TextMeshProUGUI Text(Transform parent, TMP_FontAsset font, float x, float y,
        float width, float height, float size, Color color)
    {
        var text = Rect(parent, "Text", x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSharedMaterial = font.material;
        text.fontSize = size;
        text.text = "";
        text.color = color;
        text.richText = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }
}
