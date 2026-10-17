using BepInEx;
using System.Text.Json;
using UnityEngine;

namespace MapAssist;

internal static class SaveState
{
    private static readonly Dictionary<string, bool> live = new(StringComparer.Ordinal);
    private static readonly HashSet<string> items = new(StringComparer.Ordinal);
    private static readonly HashSet<string> absorbed = new(StringComparer.Ordinal);
    private static readonly HashSet<string> keys = new(StringComparer.Ordinal);
    private static HashSet<string> readLore = new(StringComparer.Ordinal);
    private static Dictionary<string, string> aliases = new(StringComparer.Ordinal);
    private static bool observationsDirty;
    private static float nextWrite;
    private static string? saveId;
    private static string world = "";
    private static bool snapshotReady;
    internal static int Revision;
    private static string ObservationPath => Path.Combine(Paths.ConfigPath, "MapAssist", $"{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(saveId ?? "")))}.json");

    internal static void Reset()
    {
        live.Clear(); items.Clear(); keys.Clear(); absorbed.Clear(); snapshotReady = false; world = ""; Revision++;
    }

    internal static void CheckSession()
    {
        var logic = SaveLogic.Get();
        var id = logic && logic.CurrentSave?.playerSave != null ? logic.GetSaveId() : null;
        if (id == saveId) return;
        FlushObservations(true);
        Reset(); saveId = id;
        readLore = new(StringComparer.Ordinal);
        aliases = new(StringComparer.Ordinal); observationsDirty = false;
        if (string.IsNullOrEmpty(id)) return;
        try
        {
            if (File.Exists(ObservationPath))
            {
                var text = File.ReadAllText(ObservationPath);
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.ValueKind == JsonValueKind.Array) readLore = JsonSerializer.Deserialize<HashSet<string>>(text) ?? readLore;
                else
                {
                    var observations = JsonSerializer.Deserialize<Observations>(text);
                    if (observations != null) { readLore = observations.Lore ?? readLore; aliases = observations.Aliases ?? aliases; }
                }
            }
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"Could not read Map Secrets lore history: {e.Message}"); }
    }

    internal static void Snapshot()
    {
        CheckSession();
        if (!MainCharacterSingleton.TryGetEntity(out var entity) || !entity) { snapshotReady = false; return; }
        var current = entity.GetComponent<CurrentRoomInstance>();
        if (!current || !current.currentRoom) return;
        world = current.currentRoom.worldRoomData.worldName;
        items.Clear(); keys.Clear(); absorbed.Clear();
        var inventory = MainCharacterSingleton.GetComponent<InventoryComponent>();
        if (inventory && inventory.inventory != null)
        {
            foreach (var item in inventory.inventory.GetAllItemsInInventory()) if (item) items.Add(item.itemID);
            var weapon = inventory.inventory.GetEquippedWeapon(); if (weapon) items.Add(weapon.itemID);
            var spell = inventory.inventory.GetEquippedSpell(); if (spell) items.Add(spell.itemID);
            foreach (var charm in inventory.inventory.GetEquippedCharms()) if (charm) items.Add(charm.itemID);
        }
        var progression = MainCharacterSingleton.GetComponent<ProgressionComponent>();
        if (progression) foreach (var key in progression.progressionKeys) if (key) keys.Add(key.guid);
        var characters = MainCharacterSingleton.GetComponent<CharacterProgressionComponent>();
        if (characters)
            foreach (var entry in characters.characterProgressionSteps)
                if (entry.Key && entry.Value >= CharacterProgressionStep.Absorbed) absorbed.Add(entry.Key.guid);
        snapshotReady = inventory && inventory.inventory != null;
    }

    internal static void Record(string room, string id, bool collected)
    {
        CheckSession();
        if (string.IsNullOrEmpty(id)) return;
        var key = room + ":" + id;
        if (!live.TryGetValue(key, out var old) || old != collected) { live[key] = collected; Revision++; }
    }

    internal static void RecordLore(string room, Vector3 position)
    {
        CheckSession();
        if (string.IsNullOrEmpty(saveId) || !Plugin.Catalog.ByRoom.TryGetValue(room, out var markers)) return;
        var lore = markers.Where(m => m.Category == MarkerKind.Lore)
            .OrderBy(m => Math.Pow(m.X - position.x, 2) + Math.Pow(m.Y - position.y, 2)).FirstOrDefault();
        if (lore == null || Math.Pow(lore.X - position.x, 2) + Math.Pow(lore.Y - position.y, 2) > 3 * 3 || !readLore.Add(lore.Id)) return;
        Revision++;
        observationsDirty = true;
        FlushObservations(true);
    }

    internal static void BindAlias(PickupMarker marker, string actualId)
    {
        if (string.IsNullOrEmpty(actualId) || actualId == marker.SaveId || aliases.GetValueOrDefault(marker.Id) == actualId) return;
        aliases[marker.Id] = actualId;
        observationsDirty = true;
    }

    internal static void FlushObservations(bool force = false)
    {
        if (!observationsDirty || string.IsNullOrEmpty(saveId) || (!force && Time.unscaledTime < nextWrite)) return;
        nextWrite = Time.unscaledTime + 3f;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ObservationPath)!);
            var temporary = ObservationPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Observations { Lore = readLore, Aliases = aliases }));
            File.Move(temporary, ObservationPath, true);
            observationsDirty = false;
        }
        catch (Exception e) { Plugin.Logger.LogWarning($"Could not save Map Secrets observations: {e.Message}"); }
    }

    private sealed class Observations
    {
        public Observations() { }
        public HashSet<string> Lore { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Aliases { get; set; } = new(StringComparer.Ordinal);
    }

    internal static CollectionState GetCollected(PickupMarker marker)
    {
        if (readLore.Contains(marker.Id)) return CollectionState.Collected;
        if (marker.State == "observed") return CollectionState.Unknown;
        if (marker.State == "secret")
        {
            if (live.TryGetValue(marker.Room + ":secret:" + marker.SecretIndex, out var revealed) && revealed) return CollectionState.Collected;
            var explorer = MainCharacterSingleton.GetComponent<ExplorerComponent>();
            return explorer && explorer.DiscoveredSecretZone(marker.Room, marker.SecretIndex) ? CollectionState.Collected : CollectionState.Uncollected;
        }
        if (marker.ProgressKeys.Count > 0 && marker.ProgressKeys.All(keys.Contains)) return CollectionState.Collected;
        if (!string.IsNullOrEmpty(marker.ItemId) && items.Contains(marker.ItemId)) return CollectionState.Collected;
        if (!string.IsNullOrEmpty(marker.CharacterGuid) && absorbed.Contains(marker.CharacterGuid)) return CollectionState.Collected;
        if (!string.IsNullOrEmpty(marker.SaveId))
        {
            var actualId = aliases.GetValueOrDefault(marker.Id, marker.SaveId);
            if (live.TryGetValue(marker.Room + ":" + actualId, out var collected)) return collected ? CollectionState.Collected : CollectionState.Uncollected;
            var save = SaveLogic.Get();
            if (!save) return CollectionState.Unknown;
            var key = marker.State == "encounter" ? "isComplete" : "activated";
            var runtime = save.RuntimeWorldSave;
            if (marker.World == world && runtime?.savableEntityStates != null && runtime.savableEntityStates.TryGetValue(actualId, out var current))
                return MarkerRules.ReadCollectionState(current, key);
            var worlds = save.CurrentSave?.worldSaves;
            if (worlds != null && worlds.TryGetValue(marker.World, out var savedWorld)
                && savedWorld?.savableEntityStates != null && savedWorld.savableEntityStates.TryGetValue(actualId, out var saved))
                return MarkerRules.ReadCollectionState(saved, key);
            return CollectionState.Uncollected;
        }
        if (marker.State == "item" && snapshotReady) return CollectionState.Uncollected;
        if (marker.State == "progression" && snapshotReady) return CollectionState.Uncollected;
        return CollectionState.Unknown;
    }

    internal static bool IsRoomExplored(ExplorerComponent? explorer, string roomId)
        => explorer && explorer!.worldRoomExplorationStates.TryGetValue(roomId, out var state)
            && state != null && (state.usedRoomSavePoint || state.ExploredAtLeastOneChunk() || state.enteredZonesIds?.Count > 0);
}
