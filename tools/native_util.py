#!/usr/bin/env python3
"""Low-level local Addressables and transform readers used by the room audit."""
import argparse
import base64
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import struct
import sys
import UnityPy


def catalog_entries(game):
    catalog = json.loads((game / "Castlevania Belmont's Curse_Data/StreamingAssets/aa/catalog.json").read_text())
    data = base64.b64decode(catalog['m_EntryDataString'])
    bucket_data = base64.b64decode(catalog['m_BucketDataString'])
    entries = [struct.unpack_from('<7i', data, 4 + 28 * i) for i in range(struct.unpack_from('<i', data)[0])]
    buckets, offset = [], 4
    for _ in range(struct.unpack_from('<i', bucket_data)[0]):
        _, count = struct.unpack_from('<2i', bucket_data, offset)
        offset += 8
        buckets.append(struct.unpack_from('<' + 'i' * count, bucket_data, offset))
        offset += 4 * count
    paths = {}
    for entry in entries:
        path = catalog['m_InternalIds'][entry[0]]
        if path.startswith('Assets/Generated/Scenes/') and path.endswith('.unity'):
            bundle = catalog['m_InternalIds'][entries[buckets[entry[2]][0]][0]]
            paths[path] = bundle.split('\\')[-1]
    return paths


def transformed(point, transform):
    p, q, scale = (transform[k] for k in ('m_LocalPosition', 'm_LocalRotation', 'm_LocalScale'))
    x, y, z = (point[i] * scale[axis] for i, axis in enumerate('xyz'))
    # Quaternion-vector multiplication, including parent rotation and scale.
    tx, ty, tz = 2 * (q['y'] * z - q['z'] * y), 2 * (q['z'] * x - q['x'] * z), 2 * (q['x'] * y - q['y'] * x)
    return (p['x'] + x + q['w'] * tx + q['y'] * tz - q['z'] * ty,
            p['y'] + y + q['w'] * ty + q['z'] * tx - q['x'] * tz,
            p['z'] + z + q['w'] * tz + q['x'] * ty - q['y'] * tx)
