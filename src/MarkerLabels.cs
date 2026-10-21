namespace MapAssist;

internal static class MarkerLabels
{
    internal static string Completion(PickupMarker marker, CollectionState state) => state switch
    {
        CollectionState.Collected => "Collected / found",
        CollectionState.Unknown when marker.State == "unknown" => "Completion not tracked",
        CollectionState.Unknown when marker.State == "observed" => "Read to mark as found",
        CollectionState.Unknown => "Completion unknown",
        _ => ""
    };

    internal static string Category(MarkerKind kind) => kind switch
    {
        MarkerKind.Health => "Health upgrade", MarkerKind.Mana => "Mana upgrade",
        MarkerKind.Upgrade => "Permanent upgrade", MarkerKind.Experience => "Experience source", MarkerKind.Keepsake => "Keepsake",
        MarkerKind.Ability => "Ability", MarkerKind.Arcana => "Arcana",
        MarkerKind.Relic => "Relic", MarkerKind.Weapon => "Weapon", MarkerKind.Clue => "Clue",
        MarkerKind.Challenge => "Challenge room", MarkerKind.Objective => "Objective",
        MarkerKind.Puzzle => "Puzzle", MarkerKind.Lore => "Lore",
        MarkerKind.Secret => "Hidden passage", MarkerKind.Wall => "Breakable wall",
        _ => "Map marker"
    };

    internal static string Title(PickupMarker marker, IReadOnlyDictionary<string, ItemDefinition> items)
    {
        if (marker.Category == MarkerKind.Health) return "Health Heart";
        if (marker.Category == MarkerKind.Mana) return "Mana Heart";
        if (marker.Category == MarkerKind.Experience) return "Experience Source";
        if (marker.Category == MarkerKind.Upgrade)
            return marker.Symbol switch { "attack" => "Attack Upgrade", "defence" => "Defence Upgrade", _ => "Permanent Upgrade" };
        if (Readable(marker.Name)) return marker.Name.Trim();
        if (items.TryGetValue(marker.ItemId, out var item) && Readable(item.Name)) return item.Name.Trim();
        return Category(marker.Category);
    }

    private static bool Readable(string value) => !string.IsNullOrWhiteSpace(value)
        && !value.Contains('_') && !value.Contains("(Clone)", StringComparison.Ordinal)
        && !value.Equals("Lorem Ipsum", StringComparison.OrdinalIgnoreCase);
}
