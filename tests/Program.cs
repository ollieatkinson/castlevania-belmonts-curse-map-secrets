using MapAssist;
using System.Text;
using System.Text.Json;

var checks = 0;
void Check(bool passed, string message) { checks++; if (!passed) throw new Exception(message); }
Check(MarkerRules.ReadCollectionState(null) == CollectionState.Uncollected, "An untouched pickup must stay visible.");
Check(MarkerRules.ReadCollectionState("{\"activated\":true}") == CollectionState.Collected, "Activated pickup state was missed.");
Check(MarkerRules.ReadCollectionState("{\"activated\":false}") == CollectionState.Uncollected, "An available pickup was hidden.");
Check(MarkerRules.ReadCollectionState("{\"isComplete\":true}", "isComplete") == CollectionState.Collected, "Encounter completion was missed.");
Check(MarkerRules.ReadCollectionState("{\"isComplete\":false}", "isComplete") == CollectionState.Uncollected, "An unfinished encounter was hidden.");
Check(MarkerRules.ReadCollectionState("{\"activated\":true}", "isComplete") == CollectionState.Unknown, "Encounter activation is not completion.");
foreach (var value in new[] { "corrupt", "{}", "[]", "{\"activated\":\"true\"}", "{\"activated\":null}" })
{
    var state = MarkerRules.ReadCollectionState(value);
    Check(state == CollectionState.Unknown, "Invalid state was treated as collected.");
    Check(MarkerRules.ShouldShow(true, true, state, true, false), "Unknown completion must remain visible.");
}
foreach (var state in Enum.GetValues<CollectionState>())
{
    Check(!MarkerRules.ShouldShow(false, false, state, true, true), "Disabled category was shown.");
    Check(!MarkerRules.ShouldShow(true, false, state, false, false), "Unexplored room filter failed.");
    Check(MarkerRules.ShouldShow(true, false, state, true, true), "Show collected must retain every state.");
}
Check(!MarkerRules.ShouldShow(true, true, CollectionState.Collected, true, true), "Collected markers must hide.");
var tracker = new FirstVisitTracker();
Check(!tracker.Enter("slot-A", "visited", true), "Loading an explored room must not ring.");
Check(!tracker.Enter("slot-A", "visited", false), "Exploration rollback must not ring a second time.");
Check(tracker.Enter("slot-A", "hidden", false), "A genuinely new room must notify.");
Check(!tracker.Enter("slot-A", "hidden", false), "More chunks in the same room must not notify again.");
Check(!tracker.Enter("slot-A", "hidden", true), "Revisiting a revealed room must not notify.");
Check(tracker.Enter("slot-B", "hidden", false), "First visits must be scoped to the active save.");

using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "markers.json"));
var catalog = MarkerCatalog.Load(stream);
Check(catalog.Areas.Count == 10, "A playable area is missing.");
foreach (var kind in Enum.GetValues<MarkerKind>()) Check(catalog.Markers.Any(m => m.Category == kind), $"Category {kind} is empty.");
foreach (var area in catalog.Areas) Check(catalog.RoomsAudited.TryGetValue(area, out var rooms) && rooms > 0, $"Area {area} was not audited.");
Check(catalog.RoomsAudited.Values.Sum() == 384, "A playable room was missed.");
Check(catalog.Markers.Where(m => m.Category is MarkerKind.Health or MarkerKind.Mana).Select(m => m.SaveId).Distinct().Count() == 48, "Expanded heart audit changed unexpectedly.");
Check(catalog.Markers.Count(m => m.Category == MarkerKind.Ability) == 5, "Unused reward templates leaked into ability markers.");
Check(catalog.Markers.Count(m => m.Category == MarkerKind.Puzzle) == 16, "Puzzle reference coverage changed.");
Check(catalog.Markers.Count(m => m.Category == MarkerKind.Objective) == 6, "Objective reference coverage changed.");
Check(catalog.Markers.Count(m => m.Category == MarkerKind.Secret) == 102, "Hidden-path reference coverage changed.");
Check(catalog.Markers.Count(m => m.Category == MarkerKind.Lore) == 16, "Lore reference coverage changed.");
Check(catalog.Markers.Any(m => !m.Verified), "Uncertain locations must retain their confidence flag.");
Check(catalog.Markers.Any(m => m.State == "unknown"), "Unknown completion states must not be invented.");
Check(catalog.Markers.Where(m => m.State == "secret").All(m => m.SecretIndex >= 0), "A secret has no native discovery ID.");
Check(catalog.Markers.Where(m => m.State == "item").All(m => catalog.Items.ContainsKey(m.ItemId)), "An item marker has no native definition.");
Check(catalog.ByRoom.Values.Sum(room => room.Count) == catalog.Markers.Count, "Room index lost markers.");
void Reject(MarkerCatalog value)
{
    try
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));
        MarkerCatalog.Load(input); throw new Exception("Invalid catalog was accepted.");
    }
    catch (InvalidDataException) { checks++; }
}
Reject(new MarkerCatalog { Schema = 3, GameAssemblySha256 = new string('a', 64) });
Reject(new MarkerCatalog { Schema = 2, GameAssemblySha256 = new string('a', 64), Markers = new() { catalog.Markers[0], catalog.Markers[0] } });
Reject(new MarkerCatalog { Schema = 2, GameAssemblySha256 = new string('a', 64), Markers = new() { new PickupMarker { Id = "test", Kind = "other", World = "World", Room = "World_Room" } } });
Reject(new MarkerCatalog { Schema = 2, GameAssemblySha256 = new string('a', 64), Markers = new() { new PickupMarker { Id = "test", Kind = "secret", State = "secret", World = "World", Room = "World_Room" } } });
var titles = catalog.Markers.Select(marker => MarkerLabels.Title(marker, catalog.Items)).ToArray();
Check(titles.All(label => !string.IsNullOrWhiteSpace(label) && !label.Contains('_') && !label.Contains("(Clone)")), "A tooltip exposes an internal object name.");
Check(catalog.Markers.Count(m => m.Category == MarkerKind.Upgrade) == 28, "Only permanent attack/defence books belong under upgrades.");
Check(catalog.Markers.Where(m => m.Category == MarkerKind.Upgrade).All(m => m.Symbol is "attack" or "defence"), "Experience sources leaked into permanent upgrades.");
Check(catalog.Markers.Count(m => m.Category == MarkerKind.Experience) == 147, "Experience-source coverage changed.");
Check(catalog.Markers.Where(m => m.Category == MarkerKind.Experience).All(m => m.SaveId == "" && m.State == "unknown"), "Shared prefab IDs must never be used to infer experience collection.");
Check(MarkerLabels.Title(new PickupMarker { Kind = "mana", Name = "GPE_UpgradeCollectibleMana (1)" }, catalog.Items) == "Mana Heart", "Mana hover name must be readable.");
Check(MarkerLabels.Title(new PickupMarker { Kind = "upgrade", Symbol = "attack" }, catalog.Items) == "Attack Upgrade", "Attack book hover name is incorrect.");
Check(MarkerLabels.Title(new PickupMarker { Kind = "upgrade", Symbol = "defence" }, catalog.Items) == "Defence Upgrade", "Defence book hover name is incorrect.");
Check(MarkerLabels.Title(new PickupMarker { Kind = "experience", Name = "GPE_DestructibleSourceExperience(Clone)" }, catalog.Items) == "Experience Source", "Experience hover name is incorrect.");
Check(MarkerLabels.Title(new PickupMarker { Kind = "secret", Name = "Illusory Wall" }, catalog.Items) == "Illusory Wall", "Passage tooltip lost its type.");
Check(MarkerLabels.Title(new PickupMarker { Kind = "ability", ItemId = "DoubleJump", Name = "GPE_Reward" }, catalog.Items) == catalog.Items["DoubleJump"].Name, "Tooltip should fall back to the game item name.");
Check(MarkerLabels.Title(new PickupMarker { Kind = "lore", Name = "" }, catalog.Items) == "Lore", "Unnamed lore must have a category label.");
Console.WriteLine($"Passed {checks} checks: {catalog.Markers.Count} markers, {catalog.Areas.Count} areas, {Enum.GetValues<MarkerKind>().Length} categories.");
