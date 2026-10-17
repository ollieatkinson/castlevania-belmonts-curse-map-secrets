using BepInEx.Configuration;

namespace MapAssist;

internal static class Categories
{
    internal static readonly (MarkerKind Kind, string Label, string Key)[] All = {
        (MarkerKind.Health, "Health hearts", "F7"), (MarkerKind.Mana, "Mana hearts", "F8"),
        (MarkerKind.Upgrade, "Other upgrades", ""), (MarkerKind.Keepsake, "Keepsakes", ""),
        (MarkerKind.Ability, "Abilities", ""), (MarkerKind.Arcana, "Arcana", ""),
        (MarkerKind.Relic, "Relics", ""), (MarkerKind.Weapon, "Weapons", ""), (MarkerKind.Clue, "Clues", ""),
        (MarkerKind.Challenge, "Challenge rooms", ""), (MarkerKind.Objective, "Objectives", ""),
        (MarkerKind.Puzzle, "Puzzles", ""), (MarkerKind.Lore, "Lore", ""),
        (MarkerKind.Secret, "Hidden paths", "F10"), (MarkerKind.Wall, "Breakable walls", "") };
    internal static readonly Dictionary<MarkerKind, ConfigEntry<bool>> Settings = new();
    internal static void Bind(ConfigFile config)
    {
        foreach (var entry in All)
        {
            var key = entry.Kind switch { MarkerKind.Health => "HealthHearts", MarkerKind.Mana => "ManaHearts",
                MarkerKind.Secret => "SecretAreas", _ => entry.Kind.ToString() };
            Settings[entry.Kind] = config.Bind("Markers", key, entry.Kind is MarkerKind.Health or MarkerKind.Mana,
                $"Show {entry.Label.ToLowerInvariant()}. Each category has its own native game icon.");
        }
    }
    internal static bool Enabled(MarkerKind kind) => Settings[kind].Value;
}
