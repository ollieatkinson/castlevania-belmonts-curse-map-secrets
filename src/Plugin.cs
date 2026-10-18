using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using System.Security.Cryptography;
using UnityEngine;

namespace MapAssist;

[BepInPlugin(Id, "Map Secrets", BuildInfo.Version)]
public sealed class Plugin : BasePlugin
{
    public const string Id = "ollieatkinson.mapassist";
    internal static ManualLogSource Logger = null!;
    internal static MarkerCatalog Catalog = null!;
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<bool> Health = null!;
    internal static ConfigEntry<bool> Mana = null!;
    internal static ConfigEntry<bool> Secrets = null!;
    internal static ConfigEntry<bool> HideCollected = null!;
    internal static ConfigEntry<bool> ShowUnexplored = null!;
    internal static ConfigEntry<bool> PanelExpanded = null!;
    internal static ConfigEntry<int> IconSize = null!;
    internal static ConfigEntry<bool> PanelVisible = null!;
    internal static ConfigEntry<bool> HoverLabels = null!;

    internal static ConfigEntry<bool> RoomBell = null!;
    internal static ConfigEntry<float> BellVolume = null!;
    internal static ConfigEntry<bool> Unverified = null!;

    public override void Load()
    {
        Logger = Log;
        Enabled = Config.Bind("Markers", "Enabled", true, "Show Map Secrets markers. Toggle with F6 while the area map is open.");
        Categories.Bind(Config);
        Health = Categories.Settings[MarkerKind.Health];
        Mana = Categories.Settings[MarkerKind.Mana];
        Secrets = Categories.Settings[MarkerKind.Secret];
        RoomBell = Config.Bind("Notifications", "FirstVisitBell", true, "Play a bell on the first visit to a room with remaining markers in enabled categories.");
        BellVolume = Config.Bind("Notifications", "BellVolume", 0.25f, new ConfigDescription("Bell volume, independent of the game's FMOD mixer.", new AcceptableValueRange<float>(0f, 1f)));
        Unverified = Config.Bind("Filters", "ShowUnverified", false, "Include reference locations that could not be matched confidently to native discovery data. Completion may be unknown.");
        HideCollected = Config.Bind("Filters", "HideCollected", true, "Hide collected upgrades and already discovered secret areas.");
        ShowUnexplored = Config.Bind("Filters", "ShowUnexplored", true, "Include destinations in rooms that have not been explored.");
        PanelVisible = Config.Bind("Display", "ShowControls", true, "Show the small Map Secrets controls on the area map. Hotkeys work even when hidden.");
        HoverLabels = Config.Bind("Display", "MarkerTooltips", true, "Highlight visible markers and show their name when hovered with the mouse or map cursor.");
        PanelExpanded = Config.Bind("Display", "FiltersExpanded", false, "Expand the map filter controls. Toggle with F5.");
        IconSize = Config.Bind("Display", "IconSize", 40, new ConfigDescription("Marker size in pixels at 1080p.", new AcceptableValueRange<int>(12, 80)));
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("MapAssist.markers.json")
            ?? throw new InvalidDataException("Marker catalog is missing.");
        Catalog = MarkerCatalog.Load(stream);
        using var assembly = File.OpenRead(Path.Combine(Paths.GameRootPath, "GameAssembly.dll"));
        using var sha256 = SHA256.Create();
        var hash = Convert.ToHexString(sha256.ComputeHash(assembly));
        if (!hash.Equals(Catalog.GameAssemblySha256, StringComparison.OrdinalIgnoreCase))
        {
            Log.LogError("Map Secrets marker data does not match this game build. Regenerate the catalog before using this version.");
            return;
        }
        new Harmony(Id).PatchAll(typeof(Plugin).Assembly);
        AddComponent<MapOverlay>();
        AddComponent<RoomObserver>();
        Log.LogInfo($"Map Secrets {BuildInfo.Version} loaded: {Catalog.Markers.Count} mapped locations across {Catalog.Areas.Count} areas; F5 opens category filters.");
    }
}

[HarmonyPatch(typeof(UIMapLevelManager), nameof(UIMapLevelManager.OnOpenLevelMapCompleted))]
internal static class MapOpened
{
    private static void Postfix(UIMapLevelManager __instance)
    {
        try { MapOverlay.Instance?.Attach(__instance.GetCurrentlyOpenedLevelMap(), __instance); }
        catch (Exception e) { Plugin.Logger.LogError($"Could not display map markers: {e}"); }
    }
}

[HarmonyPatch(typeof(UIMapLevelManager), nameof(UIMapLevelManager.OnCloseLevelMapCompleted))]
internal static class MapClosed
{
    private static void Postfix() => MapOverlay.Instance?.Detach();
}

[HarmonyPatch(typeof(ActivableEntity), nameof(ActivableEntity.ActivateInternal))]
internal static class PickupActivated
{
    private static void Postfix(ActivableEntity __instance)
    {
        try
        {
            if (!__instance.IsActivated()) return;
            var savable = __instance.GetComponent<SavableEntityBase>();
            if (!savable || !Plugin.Catalog.SaveIds.Contains(savable.guid)) return;
            var room = __instance.GetComponentInParent<MotherBase.RoomDesigner.RoomInstance>();
            if (room) SaveState.Record(room.worldRoomData.id, savable.guid, true);
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"Could not refresh a collected heart marker: {e.Message}"); }
    }
}

[HarmonyPatch(typeof(MainCharacterSpawnLogic), nameof(MainCharacterSpawnLogic.Initialize))]
internal static class ResetTransientPickups
{
    private static void Prefix() { MapOverlay.Instance?.Detach(); SaveState.Reset(); RoomObserver.Instance?.Reset(); }
}

// Close at the start of UI teardown, rather than waiting for an animation or
// scene-unload completion callback that travel may bypass.
[HarmonyPatch(typeof(UIMapManager), nameof(UIMapManager.OnMapClosed))]
internal static class MapMenuClosed
{
    private static void Prefix() => MapOverlay.Instance?.Detach();
}

[HarmonyPatch(typeof(UIMapLevelManager), nameof(UIMapLevelManager.CloseLevelMap))]
internal static class MapStartsClosing
{
    private static void Prefix() => MapOverlay.Instance?.Detach();
}

[HarmonyPatch(typeof(UIMapLevelManager), nameof(UIMapLevelManager.OnDisable))]
internal static class MapUiDisabled
{
    private static void Prefix() => MapOverlay.Instance?.Detach();
}
