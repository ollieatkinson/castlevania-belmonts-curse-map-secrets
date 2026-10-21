using System.Text.Json;

namespace MapAssist;

internal enum MarkerKind { Health, Mana, Upgrade, Experience, Keepsake, Ability, Arcana, Relic, Weapon, Clue, Challenge, Objective, Puzzle, Lore, Secret, Wall }
internal enum CollectionState { Uncollected, Collected, Unknown }

internal sealed class MarkerCatalog
{
    public int Schema { get; set; }
    public string GameAssemblySha256 { get; set; } = "";
    public List<PickupMarker> Markers { get; set; } = new();
    public Dictionary<string, ItemDefinition> Items { get; set; } = new();
    public List<string> Areas { get; set; } = new();
    public Dictionary<string, int> RoomsAudited { get; set; } = new();
    internal Dictionary<string, List<PickupMarker>> ByRoom = new(StringComparer.Ordinal);
    internal HashSet<string> SaveIds = new(StringComparer.Ordinal);

    internal static MarkerCatalog Load(Stream stream)
    {
        var data = JsonSerializer.Deserialize<MarkerCatalog>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Empty marker catalog.");
        if (data.Schema != 2 || (data.GameAssemblySha256.Length != 64 || !data.GameAssemblySha256.All(Uri.IsHexDigit))) throw new InvalidDataException("Unsupported marker catalog.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var marker in data.Markers)
        {
            if (string.IsNullOrWhiteSpace(marker.Id) || !ids.Add(marker.Id)
                || string.IsNullOrWhiteSpace(marker.World) || !marker.Room.StartsWith(marker.World + "_", StringComparison.Ordinal)
                || !float.IsFinite(marker.X) || !float.IsFinite(marker.Y)
                || !Enum.TryParse<MarkerKind>(marker.Kind, true, out var kind) || !Enum.IsDefined(typeof(MarkerKind), kind)
                || marker.State is not ("activated" or "encounter" or "item" or "secret" or "observed" or "progression" or "unknown")
                || (marker.State == "secret" && marker.SecretIndex < 0)
                || (marker.State == "item" && !data.Items.ContainsKey(marker.ItemId)))
                throw new InvalidDataException("Invalid or duplicate marker.");
            if (!data.ByRoom.TryGetValue(marker.Room, out var room)) data.ByRoom[marker.Room] = room = new();
            room.Add(marker);
            if (!string.IsNullOrEmpty(marker.SaveId)) data.SaveIds.Add(marker.SaveId);
        }
        return data;
    }
}

internal sealed class PickupMarker
{
    public string Id { get; set; } = "";
    public string World { get; set; } = "";
    public string Room { get; set; } = "";
    public string Kind { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public string SaveId { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string CharacterGuid { get; set; } = "";
    public string State { get; set; } = "unknown";
    public int SecretIndex { get; set; } = -1;
    public List<string> ProgressKeys { get; set; } = new();
    public string Name { get; set; } = "";
    public string Source { get; set; } = "game";
    public string Symbol { get; set; } = "";
    public bool Verified { get; set; } = true;
    internal MarkerKind Category => Enum.Parse<MarkerKind>(Kind, true);
}

internal sealed class ItemDefinition
{
    public string Kind { get; set; } = "";
    public string IconGuid { get; set; } = "";
    public string IconName { get; set; } = "";
    public string Name { get; set; } = "";
}

internal static class MarkerRules
{
    internal static bool IsSharedPrefabId(PickupMarker marker, string id) => marker.Category switch
    {
        MarkerKind.Experience => id == "bcc12ecd-555d-461f-a01f-d141c80e4761",
        MarkerKind.Wall => id == "5ca47261-a8c9-4b7a-a5f2-52c00ff595c0",
        _ => false
    };

    internal static string CollectionSaveId(PickupMarker marker, string? alias)
        => !string.IsNullOrEmpty(alias) && !IsSharedPrefabId(marker, alias) ? alias! : marker.SaveId;

    internal static CollectionState ReadCollectionState(string? savedState, string key = "activated")
    {
        if (string.IsNullOrWhiteSpace(savedState)) return CollectionState.Uncollected;
        try
        {
            using var json = JsonDocument.Parse(savedState);
            if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty(key, out var value))
                return value.ValueKind switch { JsonValueKind.True => CollectionState.Collected,
                    JsonValueKind.False => CollectionState.Uncollected, _ => CollectionState.Unknown };
        }
        catch (JsonException) { }
        return CollectionState.Unknown;
    }

    internal static bool ShouldShow(bool enabled, bool hideCollected, CollectionState collected,
        bool showUnexplored, bool explored) => enabled && (!hideCollected || collected != CollectionState.Collected) && (showUnexplored || explored);
}

internal sealed class FirstVisitTracker
{
    private string? save;
    private readonly HashSet<string> visited = new(StringComparer.Ordinal);
    internal bool Enter(string saveId, string room, bool previouslyExplored)
    {
        if (save != saveId) { save = saveId; visited.Clear(); }
        return visited.Add(room) && !previouslyExplored;
    }
}

internal static class MarkerCoordinates
{
    // Catalog positions are relative to RoomInstance.root (the canonical chunk
    // origin). WorldRoomData.GetWorldPosition includes the editor offset used to
    // load the scene container, so it must not be used as this origin.
    internal static (float X, float Y) ToWorld(float localX, float localY,
        int roomChunkX, int roomChunkY, float chunkWidth, float chunkHeight)
        => (roomChunkX * chunkWidth + localX, roomChunkY * chunkHeight + localY);
}
