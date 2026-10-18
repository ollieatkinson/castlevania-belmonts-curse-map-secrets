"""Verify every icon's catalog type and native sprite names against a local game install.

Requires UnityPy. Reads installed assets; does not extract or distribute artwork.
"""
import argparse
import base64
import json
from pathlib import Path
import struct

import UnityPy

from catalog_index import Catalog


def asset_lookup(catalog):
    keys = base64.b64decode(catalog.cat["m_KeyDataString"])
    buckets = base64.b64decode(catalog.cat["m_BucketDataString"])
    lookup = {}
    offset = 4
    for _ in range(struct.unpack_from("<i", buckets)[0]):
        key, count = struct.unpack_from("<2i", buckets, offset)
        offset += 8
        indexes = struct.unpack_from("<" + "i" * count, buckets, offset)
        offset += 4 * count
        if keys[key] == 0:  # Addressables ASCII key, including asset GUIDs.
            length = struct.unpack_from("<i", keys, key + 1)[0]
            lookup[keys[key + 5:key + 5 + length].decode()] = indexes
    return lookup


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game", type=Path)
    parser.add_argument("--data", type=Path, default=Path(__file__).resolve().parent.parent / "data")
    args = parser.parse_args()
    catalog = Catalog(args.game)
    lookup = asset_lookup(catalog)
    assets = json.loads((args.data / "icons.json").read_text())
    items = json.loads((args.data / "markers.json").read_text())["items"]
    for key, item in items.items():
        if item.get("iconGuid"):
            assets["item:" + key] = dict(guid=item["iconGuid"], type="Sprite",
                                        names=[item["iconName"]] if item.get("iconName") else [])
    # The controls also borrow these game assets independently of marker loading.
    assets["menu:checkbox"] = dict(guid="17b199ad7998c5b4fb66a03f1d8c8a14", type="GameObject")
    assets["menu:font"] = dict(guid="015129531cf17024cbbd13de5ca7e951", type="TMP_FontAsset")
    loaded = {}
    failures = []
    for key, asset in assets.items():
        entries = [i for i in lookup.get(asset["guid"], [])
                   if catalog.cat["m_resourceTypes"][catalog.entries[i][6]]["m_ClassName"].split(".")[-1] == asset["type"]]
        if not entries:
            failures.append(f"{key}: {asset['guid']} has no {asset['type']} catalog entry")
            continue
        index = entries[0]
        bundle = catalog.bundle(index)
        if bundle not in loaded:
            loaded[bundle] = UnityPy.load(str(catalog.base / bundle))
        env = loaded[bundle]
        if asset["type"] == "Sprite":
            names = {o.read().m_Name for o in env.objects if o.type.name == "Sprite"}
            for name in asset.get("names", []):
                if name not in names:
                    failures.append(f"{key}: sprite {name} is missing")
        elif asset["type"] == "SpriteAtlas":
            atlases = [o.read_typetree() for o in env.objects if o.type.name == "SpriteAtlas"]
            names = {name for atlas in atlases for name in atlas.get("m_PackedSpriteNamesToIndex", [])}
            for name in asset["names"]:
                if name not in names:
                    failures.append(f"{key}: atlas member {name} is missing")
        elif asset["type"] == "Texture2D":
            target = Path(catalog.path(index)).stem
            textures = [o.read() for o in env.objects if o.type.name == "Texture2D"]
            if not any(t.m_Name == target and t.m_Width > 0 and t.m_Height > 0 for t in textures):
                failures.append(f"{key}: texture {target} is missing or empty")
        elif asset["type"] == "MapMarkerTypeDisplayData":
            target = Path(catalog.path(index)).stem
            displays = [o.read_typetree() for o in env.objects if o.type.name == "MonoBehaviour"]
            if not any(d.get("m_Name") == target and d.get("markerIcon", {}).get("m_PathID") for d in displays):
                failures.append(f"{key}: map symbol {target} has no sprite reference")
        elif asset["type"] == "GameObject":
            images = [o.read_typetree() for o in env.objects if o.type.name == "MonoBehaviour"]
            if sum(bool(i.get("m_Sprite", {}).get("m_PathID")) for i in images) < 2:
                failures.append(f"{key}: checkbox prefab lacks native image references")
        elif asset["type"] == "TMP_FontAsset":
            fonts = [o.read_typetree() for o in env.objects if o.type.name == "MonoBehaviour"]
            if not any(f.get("m_Name") == Path(catalog.path(index)).stem and f.get("m_AtlasTextures") for f in fonts):
                failures.append(f"{key}: native menu font lacks its atlas")
        else:
            failures.append(f"{key}: unsupported asset type {asset['type']}")
    if failures:
        for failure in failures:
            print("FAIL:", failure)
        raise SystemExit(1)
    print(f"Verified {len(assets)} native icon/menu references across {len(loaded)} bundles: catalog types, textures and sprite names exist.")


if __name__ == "__main__":
    main()
