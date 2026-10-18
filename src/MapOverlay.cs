using Il2CppInterop.Runtime.Attributes;
using MotherBase.RoomDesigner;
using MotherBase.ApplicationFlowManager;
using RedWine.ResourceManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MapAssist;

public sealed class MapOverlay : MonoBehaviour
{
    internal static MapOverlay? Instance;
    private LevelMap? map;
    private UIMapLevelManager? mapUi;
    private GameObject? root;
    private Material? markerMaterial;
    private readonly List<MarkerVisual> markers = new();
    private readonly MarkerIcons icons = new();
    private FilterMenu? menu;
    private MarkerTooltip? tooltip;
    private MapProjection? projection;
    private MarkerVisual? hovered;
    private float hoverStarted;
    private bool pointerMode;
    private bool hoverUnavailable;
    private float nextRefresh;
    private bool presentationSuspended;
    private readonly Dictionary<MarkerKind, int> counts = new();

    public MapOverlay(IntPtr pointer) : base(pointer) { }
    public void Awake() => Instance = this;

    public void OnDestroy()
    {
        Detach();
        menu?.Dispose();
        tooltip?.Dispose();
        icons.Dispose();
        if (markerMaterial) Object.Destroy(markerMaterial);
        Instance = null;
    }

    [HideFromIl2Cpp]
    internal void Attach(LevelMap? levelMap, UIMapLevelManager? owner = null)
    {
        Detach();
        if (!levelMap || !levelMap!.context || !levelMap.mapCamera || !levelMap.mapCamera.mapCamera) return;
        SaveState.Snapshot();
        var manager = LevelManager.Get();
        var level = levelMap.context.level;
        if (!manager || !level || !manager.worldDataDictionary.TryGetItem(level, out var reference)) return;
        var world = ResourceManager.LoadAssetByRef<WorldData>(ResourceContext.UI_GAMEMENU, reference);
        if (!world) return;
        map = levelMap;
        presentationSuspended = false;
        var mapManager = UIMapManager.Get();
        mapUi = owner ? owner : mapManager ? mapManager.levelManager : null;
        projection = new MapProjection(levelMap.mapCamera.mapCamera, mapUi ? mapUi!.levelMapImage : null);
        pointerMode = Cursor.visible;
        hoverUnavailable = false;
        if (tooltip == null)
        {
            try { tooltip = new MarkerTooltip(transform); }
            catch (Exception e) { hoverUnavailable = true; Plugin.Logger.LogWarning($"Marker labels unavailable; map filters remain active: {e.Message}"); }
        }
        // The controls must not depend on every marker's artwork loading.
        if (menu == null)
        {
            try { menu = new FilterMenu(transform, SettingsChanged, icons); }
            catch (Exception e) { Plugin.Logger.LogError($"Map filter menu unavailable; retrying next map opening: {e}"); }
        }
        try
        {
            var rooms = new Dictionary<string, WorldRoomData>(StringComparer.Ordinal);
            foreach (var room in world.GetRooms()) rooms[room.id] = room;
            var template = levelMap.GetComponentInChildren<SpriteRenderer>(true);
            var layer = template ? template.gameObject.layer : levelMap.context.gameObject.layer;
            if (!markerMaterial)
            {
                var shader = Shader.Find("Sprites/Default");
                if (!shader) throw new InvalidOperationException("Sprite shader is unavailable.");
                markerMaterial = new Material(shader) { renderQueue = 4000 };
            }
            root = new GameObject("Map Secrets overlay");
            root.transform.SetParent(levelMap.context.transform, false);
            foreach (var pickup in Plugin.Catalog.Markers)
            {
                if ((!pickup.Verified && !Plugin.Unverified.Value) || !rooms.TryGetValue(pickup.Room, out var room)) continue;
                var point = MarkerCoordinates.ToWorld(pickup.X, pickup.Y, room.positionCoord.x, room.positionCoord.y,
                    world.worldSettings.chunkWidth, world.worldSettings.chunkHeight);
                var position = new Vector3(point.X, point.Y, 0f);
                var zone = levelMap.context.settings.navigationZonesManager.GetNavigationZoneAtPosition(position);
                if (!zone)
                {
                    Plugin.Logger.LogWarning($"Skipping marker outside map navigation zones: {pickup.Room}, {pickup.Id}, {position}");
                    continue;
                }
                try { CreateMarker(pickup.Category, pickup.Room, position, zone, layer, template, pickup, pickup.SecretIndex); }
                catch (Exception e) { Plugin.Logger.LogWarning($"Skipping unavailable marker {pickup.Id}: {e.Message}"); }
            }
            RefreshVisibility();
            Plugin.Logger.LogInfo($"Map overlay: {markers.Count} markers loaded for {level.name}. Save filtering active; no player markers added.");
        }
        catch (Exception e)
        {
            // Keep map attachment and menu hotkey alive even if marker construction fails.
            if (root) { root!.SetActive(false); Object.Destroy(root); }
            root = null;
            markers.Clear();
            counts.Clear();
            Plugin.Logger.LogError($"Could not draw markers; map filters remain available: {e}");
        }
    }

    [HideFromIl2Cpp]
    private void CreateMarker(MarkerKind kind, string room, Vector3 position, LevelMapNavigationZone? zone,
        int layer, SpriteRenderer? template, PickupMarker? pickup, int secretId)
    {
        var sprites = pickup != null ? icons.Get(pickup) : icons.Get(kind);
        if (sprites.Length == 0) return;
        var marker = new GameObject($"MapAssist {kind}") { layer = layer };
        marker.transform.SetParent(root!.transform, false);
        position.z = template ? template!.transform.position.z : 0f;
        marker.transform.position = position;
        var renderers = new List<SpriteRenderer>();
        for (var i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i];
            var layerObject = new GameObject($"Artwork {i}") { layer = layer };
            layerObject.transform.SetParent(marker.transform, false);
            // Native sprites have different pixels-per-unit settings. Normalize their
            // original canvas to one unit without stretching or changing the atlas UVs.
            var size = Mathf.Max(sprite.rect.width, sprite.rect.height);
            var unitScale = sprite.pixelsPerUnit / Mathf.Max(size, 1f);
            layerObject.transform.localScale = new Vector3(unitScale, unitScale, 1f);
            var renderer = layerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = markerMaterial;
            if (template) renderer.sortingLayerID = template!.sortingLayerID;
            renderer.sortingOrder = 30000 + i;
            renderers.Add(renderer);
        }
        markers.Add(new MarkerVisual(marker, renderers.ToArray(), kind, room, zone, pickup, secretId));
    }

    [HideFromIl2Cpp]
    internal void Detach()
    {
        if (root)
        {
            root!.SetActive(false);
            Object.Destroy(root);
        }
        menu?.Refresh(false, counts);
        tooltip?.Hide();
        hovered = null;
        projection = null;
        markers.Clear();
        root = null;
        map = null;
        mapUi = null;
        counts.Clear();
    }

    public void Update()
    {
        // Real close/loading callbacks own attachment. A preloaded map or UI
        // image can be temporarily inactive while opening; do not discard its
        // markers based on unrelated game-menu or intermediate UI state.
        if (!map) { Detach(); return; }
        try
        {
            var current = map!;
            if (ApplicationFlowManager.IsLoading) { Detach(); return; }
            if (!current.gameObject.activeInHierarchy)
            {
                if (!presentationSuspended) Plugin.Logger.LogInfo("Map overlay paused while its map is inactive; keeping markers for reactivation.");
                presentationSuspended = true;
                if (root) root!.SetActive(false);
                menu?.Refresh(false, counts);
                ClearHover();
                return;
            }
            if (presentationSuspended) Plugin.Logger.LogInfo("Map overlay resumed after map activation.");
            presentationSuspended = false;
            if (root) root!.SetActive(true);
            var ui = UIManager.GetInstance();
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (Hotkeys.Pressed(keyboard, Hotkeys.Menu))
                {
                    var expand = !Plugin.PanelVisible.Value || !Plugin.PanelExpanded.Value;
                    Plugin.PanelVisible.Value = true;
                    Plugin.PanelExpanded.Value = expand;
                }
                if (Hotkeys.Pressed(keyboard, Hotkeys.Overlay)) Plugin.Enabled.Value = !Plugin.Enabled.Value;
                if (Hotkeys.Pressed(keyboard, Hotkeys.Health)) Plugin.Health.Value = !Plugin.Health.Value;
                if (Hotkeys.Pressed(keyboard, Hotkeys.Mana)) Plugin.Mana.Value = !Plugin.Mana.Value;
                if (Hotkeys.Pressed(keyboard, Hotkeys.Secrets)) Plugin.Secrets.Value = !Plugin.Secrets.Value;
            }
            var popupVisible = ui && ui.confirmationPopup && ui.confirmationPopup.visible;
            menu?.Refresh(Plugin.PanelVisible.Value && !popupVisible, counts);
            var camera = current.mapCamera.mapCamera;
            var scale = camera.orthographicSize * 2f / Mathf.Max(camera.pixelHeight, 1)
                * Plugin.IconSize.Value * Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.5f);
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.2f;
                RefreshVisibility();
            }
            // Optional hover UI must never disable markers or the menu hotkey controls.
            try { UpdateHover(popupVisible); }
            catch (Exception e)
            {
                hovered = null;
                tooltip?.Hide();
                if (!hoverUnavailable) Plugin.Logger.LogWarning($"Marker hover disabled until the next map opening: {e.Message}");
                hoverUnavailable = true;
            }
            foreach (var marker in markers)
            {
                if (!marker.Object || !marker.Object.activeInHierarchy) continue;
                marker.Highlight = Mathf.MoveTowards(marker.Highlight, marker == hovered ? 1f : 0f, Time.unscaledDeltaTime * 9f);
                var size = scale * (1f + marker.Highlight * 0.14f);
                marker.Object.transform.localScale = new Vector3(size, size, size);
                var color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.68f, 1f), marker.Highlight * 0.55f);
                color.a = marker.Collected == CollectionState.Collected ? 0.35f : 1f;
                foreach (var renderer in marker.Renderers) if (renderer) renderer.color = color;
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Map overlay disabled for this map after an error: {e}");
            Detach();
        }
    }

    [HideFromIl2Cpp]
    private void RefreshVisibility()
    {
        if (!map) return;
        SaveState.CheckSession();
        var explorer = MainCharacterSingleton.GetComponent<ExplorerComponent>();
        var currentZone = map!.context.GetCurrentNavigationZone();
        counts.Clear();
        foreach (var marker in markers)
        {
            if (!marker.Object) continue;
            var enabled = Categories.Enabled(marker.Kind);
            var collected = marker.Pickup != null ? SaveState.GetCollected(marker.Pickup) : CollectionState.Unknown;
            var visible = Plugin.Enabled.Value && marker.Zone == currentZone
                && MarkerRules.ShouldShow(enabled, Plugin.HideCollected.Value, collected,
                    Plugin.ShowUnexplored.Value, SaveState.IsRoomExplored(explorer, marker.Room));
            marker.Object.SetActive(visible);
            marker.Collected = collected;
            if (!visible) { marker.Highlight = 0; continue; }
            counts[marker.Kind] = counts.GetValueOrDefault(marker.Kind) + 1;
        }
    }

    [HideFromIl2Cpp]
    private void UpdateHover(bool popupVisible)
    {
        if (hoverUnavailable || !Plugin.HoverLabels.Value || popupVisible || !map || projection == null
            || !map!.controller || !map.controller.AreControlsEnabled()) { ClearHover(); return; }
        var mouse = Mouse.current;
        var pad = Gamepad.current;
        var keyboard = Keyboard.current;
        if ((pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.04f || pad.rightStick.ReadValue().sqrMagnitude > 0.04f))
            || (keyboard != null && (keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed
                || keyboard.upArrowKey.isPressed || keyboard.downArrowKey.isPressed || keyboard.leftArrowKey.isPressed || keyboard.rightArrowKey.isPressed)))
            pointerMode = false;
        if (mouse != null && Cursor.visible && (mouse.delta.ReadValue().sqrMagnitude > 1f || mouse.leftButton.wasPressedThisFrame)) pointerMode = true;
        Vector2 pointer;
        if (pointerMode && mouse != null && Cursor.visible) pointer = mouse.position.ReadValue();
        else
        {
            var cursor = map.controller.cursor;
            if (!cursor || !cursor.gameObject.activeInHierarchy || !projection.TryScreenPoint(cursor.transform.position, out pointer)) { ClearHover(); return; }
        }
        if (menu?.ContainsScreenPoint(pointer) == true) { ClearHover(); return; }
        var radius = Mathf.Max(16f, Plugin.IconSize.Value * 0.6f) * Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.5f);
        var bestDistance = radius * radius;
        MarkerVisual? best = null;
        var anchor = Vector2.zero;
        foreach (var marker in markers)
        {
            if (!marker.Object || !marker.Object.activeInHierarchy || marker.Pickup == null
                || !projection.TryScreenPoint(marker.Object.transform.position, out var screen)) continue;
            var distance = (screen - pointer).sqrMagnitude;
            if (distance > bestDistance || (best != null && distance == bestDistance && marker != hovered)) continue;
            bestDistance = distance; best = marker; anchor = screen;
        }
        if (best != hovered) { hovered = best; hoverStarted = Time.unscaledTime; tooltip?.Hide(); }
        if (best?.Pickup == null) { ClearHover(); return; }
        if (Time.unscaledTime - hoverStarted < 0.18f) return;
        var title = MarkerLabels.Title(best.Pickup, Plugin.Catalog.Items);
        var category = MarkerLabels.Category(best.Kind);
        if (!best.Pickup.Verified) category += " · Approximate location";
        if (best.Collected == CollectionState.Collected) category += " · Collected";
        else if (best.Collected == CollectionState.Unknown) category += " · Completion unknown";
        tooltip?.Show(title, category, anchor);
    }

    [HideFromIl2Cpp]
    private void ClearHover() { hovered = null; tooltip?.Hide(); }

    [HideFromIl2Cpp]
    internal void RefreshNow() { if (map) RefreshVisibility(); }

    [HideFromIl2Cpp]
    private void SettingsChanged()
    {
        // Rebuild to include/exclude unverified reference locations immediately.
        var current = map;
        var owner = mapUi;
        if (current) Attach(current, owner);
    }

    private sealed record MarkerVisual(GameObject Object, SpriteRenderer[] Renderers, MarkerKind Kind,
        string Room, LevelMapNavigationZone? Zone, PickupMarker? Pickup, int SecretId)
    {
        internal float Highlight;
        internal CollectionState Collected;
    }
}
