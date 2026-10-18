using RedWine.ResourceManagement;
using System.Text.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace MapAssist;

// Assets stay owned by the game. We own only atlas clones and Sprite wrappers
// around the game's Texture2D-only book artwork, never their textures.
internal sealed class MarkerIcons
{
    private sealed class NativeIcon
    {
        public string Guid { get; set; } = "";
        public string Type { get; set; } = "";
        public string[] Names { get; set; } = Array.Empty<string>();
    }

    private readonly Dictionary<string, NativeIcon> assets;
    private readonly Dictionary<string, Sprite[]> cache = new();
    private readonly List<Sprite> ownedSprites = new();
    private readonly List<AssetReference> references = new();
    private readonly HashSet<string> failures = new();

    internal MarkerIcons()
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream("MapAssist.icons.json")
            ?? throw new InvalidDataException("Native icon references are missing.");
        assets = JsonSerializer.Deserialize<Dictionary<string, NativeIcon>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Native icon references are invalid.");
    }

    internal Sprite[] Get(PickupMarker marker)
    {
        try
        {
            if (!string.IsNullOrEmpty(marker.Symbol)) return Load(marker.Symbol, assets[marker.Symbol]);
            if (Plugin.Catalog.Items.TryGetValue(marker.ItemId, out var item) && !string.IsNullOrEmpty(item.IconGuid))
                return Load("item:" + marker.ItemId, new NativeIcon { Guid = item.IconGuid, Type = "Sprite", Names = string.IsNullOrEmpty(item.IconName) ? Array.Empty<string>() : new[] { item.IconName } });
        }
        catch (Exception e) { WarnOnce("item:" + marker.ItemId + ":" + marker.Symbol, e); }
        return Get(marker.Category);
    }

    internal Sprite[] Get(MarkerKind kind)
    {
        var key = "category:" + kind;
        if (cache.TryGetValue(key, out var found) && found.All(sprite => sprite)) return found;
        try
        {
            var name = kind == MarkerKind.Upgrade ? "attack" : kind.ToString().ToLowerInvariant();
            if (assets.TryGetValue(name, out var asset)) return cache[key] = Load(name, asset);
            var representative = Plugin.Catalog.Items.Values.FirstOrDefault(item => item.Kind.Equals(kind.ToString(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(item.IconGuid));
            if (representative != null)
                return cache[key] = Load(key, new NativeIcon { Guid = representative.IconGuid, Type = "Sprite", Names = string.IsNullOrEmpty(representative.IconName) ? Array.Empty<string>() : new[] { representative.IconName } });
        }
        catch (Exception e) { WarnOnce(key, e); }
        // Missing artwork must never prevent the map or its filter menu opening.
        // The native eye is a last resort; if it too fails, omit only the artwork.
        try { return cache[key] = Load("secret", assets["secret"]); }
        catch (Exception e) { WarnOnce("fallback:secret", e); return Array.Empty<Sprite>(); }
    }

    private Sprite[] Load(string key, NativeIcon asset)
    {
        if (cache.TryGetValue(key, out var found) && found.All(sprite => sprite)) return found;
        if (failures.Contains("load:" + key)) throw new InvalidOperationException($"Native icon {key} previously failed to load.");
        try
        {
            var layers = new List<Sprite>();
            if (asset.Type == "SpriteAtlas")
            {
                var atlas = Borrow<SpriteAtlas>(asset.Guid);
                if (!atlas) throw new InvalidOperationException($"Native atlas {key} is unavailable.");
                foreach (var name in asset.Names)
                {
                    var sprite = atlas.GetSprite(name);
                    if (!sprite) throw new InvalidOperationException($"Native atlas sprite {name} is unavailable.");
                    ownedSprites.Add(sprite);
                    layers.Add(sprite);
                }
            }
            else if (asset.Type == "Texture2D")
            {
                // These books are catalogued only as Texture2D, not as Sprite.
                var texture = Borrow<Texture2D>(asset.Guid);
                if (!texture) throw new InvalidOperationException($"Native texture {key} is unavailable.");
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                if (!sprite) throw new InvalidOperationException($"Could not display native texture {key}.");
                ownedSprites.Add(sprite);
                layers.Add(sprite);
            }
            else if (asset.Type == "MapMarkerTypeDisplayData")
            {
                var display = Borrow<MapMarkerTypeDisplayData>(asset.Guid);
                if (!display || !display.markerIcon) throw new InvalidOperationException($"Native map symbol {key} is unavailable.");
                layers.Add(display.markerIcon);
            }
            else if (asset.Type == "Sprite")
            {
                var reference = new AssetReferenceSprite(asset.Guid);
                if (asset.Names.Length > 0) reference.SubObjectName = asset.Names[0];
                references.Add(reference);
                var sprite = ResourceManager.LoadAssetByRef<Sprite>(ResourceContext.Always, reference);
                if (!sprite) throw new InvalidOperationException($"Native sprite {key} is unavailable.");
                layers.Add(sprite);
            }
            else throw new InvalidDataException($"Unsupported native icon type: {asset.Type}");
            return cache[key] = layers.ToArray();
        }
        catch { failures.Add("load:" + key); throw; }
    }

    private T Borrow<T>(string guid) where T : Object
    {
        var reference = new AssetReference(guid);
        references.Add(reference);
        return ResourceManager.LoadAssetByRef<T>(ResourceContext.Always, reference);
    }

    private void WarnOnce(string key, Exception error)
    {
        if (failures.Add("warning:" + key)) Plugin.Logger.LogWarning($"Using fallback artwork for {key}: {error.Message}");
    }

    internal void Dispose()
    {
        foreach (var sprite in ownedSprites) if (sprite) Object.Destroy(sprite);
        ownedSprites.Clear();
        cache.Clear();
        references.Clear();
        failures.Clear();
    }
}
