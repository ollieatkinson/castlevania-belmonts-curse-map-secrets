using Il2CppInterop.Runtime.Attributes;
using MotherBase.RoomDesigner;
using RedWine.ResourceManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MapAssist;

public sealed class MapOverlay : MonoBehaviour
{
    internal static MapOverlay? Instance;
    private LevelMap? map;
    private GameObject? root;
    private Material? markerMaterial;
    private readonly List<MarkerVisual> markers = new();
    private readonly MarkerIcons icons = new();
    private FilterMenu? menu;
    private bool menuUnavailable;
    private float nextRefresh;
    private readonly Dictionary<MarkerKind, int> counts = new();

    public MapOverlay(IntPtr pointer) : base(pointer) { }
    public void Awake() => Instance = this;

    public void OnDestroy()
    {
        Detach();
        menu?.Dispose();
        icons.Dispose();
        if (markerMaterial) Object.Destroy(markerMaterial);
        Instance = null;
    }

    [HideFromIl2Cpp]
    internal void Attach(LevelMap? levelMap)
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
                var origin = room.GetWorldPosition(world.worldSettings);
                var position = origin + new Vector3(pickup.X, pickup.Y, 0f);
                var zone = levelMap.context.settings.navigationZonesManager.GetNavigationZoneAtPosition(position);
                if (!zone)
                {
                    Plugin.Logger.LogWarning($"Skipping marker outside map navigation zones: {pickup.Room}, {pickup.Id}, {position}");
                    continue;
                }
                CreateMarker(pickup.Category, pickup.Room, position, zone, layer, template, pickup, pickup.SecretIndex);
            }
            RefreshVisibility();
            if (menu == null && !menuUnavailable)
            {
                try { menu = new FilterMenu(transform, SettingsChanged, icons); }
                catch (Exception e)
                {
                    menuUnavailable = true;
                    Plugin.Logger.LogError($"Map filter menu unavailable; keyboard shortcuts remain active: {e}");
                }
            }
            Plugin.Logger.LogInfo($"Map overlay: {markers.Count} markers loaded for {level.name}. Save filtering active; no player markers added.");
        }
        catch
        {
            Detach();
            throw;
        }
    }

    [HideFromIl2Cpp]
    private void CreateMarker(MarkerKind kind, string room, Vector3 position, LevelMapNavigationZone? zone,
        int layer, SpriteRenderer? template, PickupMarker? pickup, int secretId)
    {
        var sprites = pickup != null ? icons.Get(pickup) : icons.Get(kind);
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
        markers.Clear();
        root = null;
        map = null;
        counts.Clear();
    }

    public void Update()
    {
        if (!map) return;
        try
        {
            if (!map!.gameObject.activeInHierarchy) { Detach(); return; }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.f5Key.wasPressedThisFrame) Plugin.PanelExpanded.Value = !Plugin.PanelExpanded.Value;
                if (keyboard.f6Key.wasPressedThisFrame) Plugin.Enabled.Value = !Plugin.Enabled.Value;
                if (keyboard.f7Key.wasPressedThisFrame) Plugin.Health.Value = !Plugin.Health.Value;
                if (keyboard.f8Key.wasPressedThisFrame) Plugin.Mana.Value = !Plugin.Mana.Value;
                if (keyboard.f10Key.wasPressedThisFrame) Plugin.Secrets.Value = !Plugin.Secrets.Value;
            }
            var ui = UIManager.GetInstance();
            var popupVisible = ui && ui.confirmationPopup && ui.confirmationPopup.visible;
            menu?.Refresh(Plugin.PanelVisible.Value && !popupVisible, counts);
            var camera = map.mapCamera.mapCamera;
            var scale = camera.orthographicSize * 2f / Mathf.Max(camera.pixelHeight, 1)
                * Plugin.IconSize.Value * Mathf.Clamp(Screen.height / 1080f, 0.75f, 2.5f);
            foreach (var marker in markers)
                if (marker.Object) marker.Object.transform.localScale = new Vector3(scale, scale, scale);
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.2f;
                RefreshVisibility();
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
            if (!visible) continue;
            foreach (var renderer in marker.Renderers)
                renderer.color = collected == CollectionState.Collected ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            counts[marker.Kind] = counts.GetValueOrDefault(marker.Kind) + 1;
        }
    }

    [HideFromIl2Cpp]
    internal void RefreshNow() { if (map) RefreshVisibility(); }

    [HideFromIl2Cpp]
    private void SettingsChanged()
    {
        // Rebuild to include/exclude unverified reference locations immediately.
        var current = map;
        if (current) Attach(current);
    }

    private sealed record MarkerVisual(GameObject Object, SpriteRenderer[] Renderers, MarkerKind Kind,
        string Room, LevelMapNavigationZone? Zone, PickupMarker? Pickup, int SecretId);
}
