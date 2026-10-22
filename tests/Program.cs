using MapAssist;

var checks = 0;
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new Exception(message);
}

using var input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "markers.json"));
var catalog = MarkerCatalog.Load(input);
var spiritClue = catalog.Markers.Single(m => m.ItemId == "Clue_Ferryman_MissingCoins");
Check(spiritClue.Category == MarkerKind.Clue && spiritClue.Source == "game" && spiritClue.Verified,
    "The Hospital spirit must be a verified Clue marker at its interaction point.");
Check(Math.Abs(spiritClue.X - -15.11f) < 0.01f && Math.Abs(spiritClue.Y - 35f) < 0.01f,
    "Stolen Oboles must be at the spirit trigger, not the room centre.");
Check(spiritClue.Id.EndsWith(":clue:Clue_Ferryman_MissingCoins:36:19.5") && spiritClue.State == "item",
    "Moving the spirit clue must preserve marker identity and inventory completion tracking.");
Check(catalog.Markers.Where(m => m.Category == MarkerKind.Clue).All(m => m.Source == "game"),
    "Clues must use native interaction positions rather than room-centre placeholders.");
Check(catalog.Markers.Count(m => m.ItemId == "Clue_Brauner_NotreDamePainting") == 2,
    "Both interactions for the Notre-Dame painting clue must remain available.");
const string wallPrefab = "5ca47261-a8c9-4b7a-a5f2-52c00ff595c0";
const string statuePrefab = "bcc12ecd-555d-461f-a01f-d141c80e4761";
var walls = catalog.Markers.Where(m => m.Category == MarkerKind.Wall && m.Source == "game").ToArray();
Check(walls.Length == 42, "Native wall coverage changed.");
Check(walls.Select(m => m.SaveId).Distinct().Count() == walls.Length, "Walls must use individual context IDs.");
foreach (var wall in walls)
{
    Check(wall.SaveId.Length > 0 && wall.SaveId != wallPrefab && wall.State == "activated", "Wall completion ID is missing or shared.");
    Check(wall.Id.Contains(wallPrefab), "Existing wall marker identity must survive the save-ID migration.");
    Check(MarkerRules.CollectionSaveId(wall, wallPrefab) == wall.SaveId, "An old shared alias overrode a wall context ID.");
}
foreach (var statue in catalog.Markers.Where(m => m.Category == MarkerKind.Experience))
    Check(MarkerRules.CollectionSaveId(statue, statuePrefab) == statue.SaveId && statue.SaveId != statuePrefab,
        "The experience statue fix regressed.");

// A pre-existing save with only one broken wall must hide only that wall,
// even if observation files still contain the old shared prefab alias.
var first = walls.Single(m => m.SaveId == "34da7d6c-db1f-4bba-8588-fe1246762e75");
var second = walls.Single(m => m.SaveId == "563c82cc-d453-4c66-b0b1-60e5bb41e2df");
var savedStates = new Dictionary<string, string>
{
    [first.SaveId] = "{\"activated\":true}",
    [wallPrefab] = "{\"activated\":true}"
};
CollectionState Saved(PickupMarker marker) => MarkerRules.ReadCollectionState(
    savedStates.GetValueOrDefault(MarkerRules.CollectionSaveId(marker, wallPrefab)));
Check(Saved(first) == CollectionState.Collected, "A previously broken wall must be recognised before revisiting.");
Check(Saved(second) == CollectionState.Uncollected, "Breaking one wall must not hide another.");
Check(!MarkerRules.ShouldShow(true, true, Saved(first), true, true), "Hide found must hide the broken wall.");
Check(MarkerRules.ShouldShow(true, true, Saved(second), true, true), "Hide found must retain the intact wall.");
Check(MarkerRules.CollectionSaveId(first, "runtime-context") == "runtime-context", "Valid runtime aliases must still work.");
Check(MarkerRules.CollectionSaveId(first, null) == first.SaveId, "A missing alias must use the catalog context ID.");

var unsupported = catalog.Markers.First(m => m.State == "unknown");
var lore = catalog.Markers.First(m => m.State == "observed");
Check(MarkerLabels.Completion(unsupported, CollectionState.Unknown) == "Completion not tracked", "Unsupported tracking needs an explicit label.");
Check(MarkerLabels.Completion(lore, CollectionState.Unknown) == "Read to mark as found", "Lore needs a read-history label.");
Check(MarkerLabels.Completion(first, CollectionState.Unknown) == "Completion unknown", "Unreadable supported state must stay distinct.");
Check(MarkerLabels.Completion(lore, CollectionState.Collected) == "Collected / found", "Observed lore must show as found.");
foreach (var json in new[] { "{}", "corrupt", "{\"activated\":null}", "{\"activated\":\"true\"}" })
{
    var state = MarkerRules.ReadCollectionState(json);
    Check(state == CollectionState.Unknown && MarkerRules.ShouldShow(true, true, state, true, true),
        "Missing or invalid completion evidence must keep markers visible.");
}
Check(MarkerRules.ReadCollectionState("{\"isComplete\":true}", "isComplete") == CollectionState.Collected,
    "Encounter completion must still use its own flag.");
Console.WriteLine($"Passed {checks} checks; {walls.Length} native walls use individual save IDs.");
