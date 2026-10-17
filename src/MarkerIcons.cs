using RedWine.ResourceManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace MapAssist;

// Borrow the game's art at runtime. Only SpriteAtlas.GetSprite clones belong to us;
// their textures and the eye sprite remain owned by the game's resource manager.
internal sealed class MarkerIcons
{
    private readonly Dictionary<MarkerKind, Sprite[]> icons = new();
    private readonly List<Sprite> atlasClones = new();
    private readonly AssetReference heartsReference = new("24161bd6821b99e409dd9a0a415d16ea");

    private readonly Dictionary<string, Sprite[]> itemIcons = new();
    private readonly List<AssetReference> references = new();

    internal Sprite[] Get(PickupMarker marker)
    {
        if (!string.IsNullOrEmpty(marker.Symbol)) return LoadSprite(marker.Symbol,
            marker.Symbol == "attack" ? "b0e5b33781665c1459403febed86c93b" : "9d6e9631387862842ab4378528d9aa88");
        if (Plugin.Catalog.Items.TryGetValue(marker.ItemId, out var item) && !string.IsNullOrEmpty(item.IconGuid))
        {
            try { return LoadSprite(marker.ItemId, item.IconGuid, item.IconName); }
            catch (Exception e) { Plugin.Logger.LogWarning($"Using category icon for {marker.ItemId}: {e.Message}"); }
        }
        return Get(marker.Category);
    }

    private Sprite[] LoadSprite(string key, string guid, string subName = "")
    {
        if (itemIcons.TryGetValue(key, out var found) && found.All(sprite => sprite)) return found;
        var reference = new AssetReferenceSprite(guid);
        if (!string.IsNullOrEmpty(subName)) reference.SubObjectName = subName;
        var sprite = ResourceManager.LoadAssetByRef<Sprite>(ResourceContext.Always, reference);
        if (!sprite) throw new InvalidOperationException($"Native sprite {key} is unavailable.");
        references.Add(reference);
        return itemIcons[key] = new[] { sprite };
    }

    internal Sprite[] Get(MarkerKind kind)
    {
        if (icons.TryGetValue(kind, out var cached) && cached.All(sprite => sprite)) return cached;
        if (kind is not (MarkerKind.Health or MarkerKind.Mana))
        {
            var representative = Plugin.Catalog.Items.Values.FirstOrDefault(item => item.Kind.Equals(kind.ToString(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(item.IconGuid));
            if (representative != null)
            {
                try { return icons[kind] = LoadSprite("category:" + kind, representative.IconGuid, representative.IconName); }
                catch (Exception e) { Plugin.Logger.LogWarning($"Category artwork fallback for {kind}: {e.Message}"); }
            }
            if (kind is MarkerKind.Upgrade) return icons[kind] = LoadSprite("category:" + kind, "b0e5b33781665c1459403febed86c93b");
            if (kind is MarkerKind.Objective) return icons[kind] = LoadSprite("category:" + kind, "8b6860faeef7ae04f8cfbd9068a1f76c");
            if (kind is MarkerKind.Lore) return icons[kind] = LoadSprite("category:" + kind, "087a8afe09493b642a9f1527133c0a53");
            if (kind is MarkerKind.Clue) return icons[kind] = LoadSprite("category:" + kind, "159f29af9dde48d4c8c51d70cf629042");
            var guid = kind switch {
                MarkerKind.Challenge => "82a3b804368403244b6e81e4ae05edb8",
                MarkerKind.Puzzle => "992929bdfa0b5174999934cccd4d8184",
                MarkerKind.Wall => "0d5b2d246c4094c47baf17ec41de29f5",
                _ => "844d5c930d1997341967b869363176ee" };
            var reference = new AssetReference(guid);
            var display = ResourceManager.LoadAssetByRef<MapMarkerTypeDisplayData>(ResourceContext.Always, reference);
            if (!display || !display.markerIcon) throw new InvalidOperationException($"The game's {kind} map icon is unavailable.");
            references.Add(reference);
            icons[kind] = new[] { display.markerIcon };
        }
        else
        {
            var atlas = ResourceManager.LoadAssetByRef<SpriteAtlas>(ResourceContext.Always, heartsReference);
            if (!atlas) throw new InvalidOperationException("The game's collectible icon atlas is unavailable.");
            // The game assembles these three layers into the framed heart shown in its UI.
            var names = new[] { "T_UI_HPMP_Collectible_FrameBlackBG",
                kind == MarkerKind.Health ? "T_UI_HP_Collectible_Full" : "T_UI_MP_Collectible_Full",
                "T_UI_HPMP_Collectible_Frame" };
            var layers = new List<Sprite>();
            foreach (var name in names)
            {
                var sprite = atlas.GetSprite(name);
                if (!sprite) throw new InvalidOperationException($"The game's collectible sprite {name} is unavailable.");
                atlasClones.Add(sprite);
                layers.Add(sprite);
            }
            icons[kind] = layers.ToArray();
        }
        Plugin.Logger.LogInfo($"Using native game artwork for {kind} markers.");
        return icons[kind];
    }

    internal void Dispose()
    {
        foreach (var sprite in atlasClones)
            if (sprite) Object.Destroy(sprite);
        atlasClones.Clear();
        icons.Clear();
        itemIcons.Clear();
        references.Clear();
    }
}
