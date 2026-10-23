"""Convert embedded WebP images to PNG without changing model geometry or materials.

Usage: python Tools/ConvertGlbWebPToPng.py path/to/Unity-compatible.glb
Requires Pillow. The source export should be kept separately from this import copy.
"""
import argparse
from io import BytesIO
import json
from pathlib import Path
import struct

from PIL import Image


def convert(path):
    data = path.read_bytes()
    magic, version, length = struct.unpack_from('<III', data)
    if (magic, version, length) != (0x46546C67, 2, len(data)):
        raise ValueError('Expected a valid GLB 2.0 file')
    chunks = []
    offset = 12
    while offset < len(data):
        size, kind = struct.unpack_from('<II', data, offset)
        offset += 8
        chunks.append((kind, data[offset:offset + size]))
        offset += size
    if len(chunks) != 2 or chunks[0][0] != 0x4E4F534A or chunks[1][0] != 0x004E4942:
        raise ValueError('Expected JSON and a single embedded binary chunk')
    document = json.loads(chunks[0][1])
    binary = bytearray(chunks[1][1])
    count = 0
    for image in document.get('images', []):
        if image.get('mimeType') != 'image/webp':
            continue
        view = document['bufferViews'][image['bufferView']]
        if view['buffer'] != 0:
            raise ValueError('Image must use the embedded GLB buffer')
        start = view.get('byteOffset', 0)
        payload = bytes(binary[start:start + view['byteLength']])
        with Image.open(BytesIO(payload)) as source:
            pixels = source.convert('RGBA' if 'A' in source.getbands() else 'RGB')
            output = BytesIO()
            pixels.save(output, format='PNG')
            png = output.getvalue()
            # Confirm the format conversion preserves every decoded color pixel.
            with Image.open(BytesIO(png)) as decoded:
                if decoded.tobytes() != pixels.tobytes():
                    raise ValueError('PNG conversion changed decoded image pixels')
            print(f'Converted image {count}: {source.size[0]}x{source.size[1]} WebP -> PNG')
        binary.extend(b'\0' * (-len(binary) % 4))
        image['bufferView'] = len(document['bufferViews'])
        document['bufferViews'].append({
            'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(png)
        })
        image['mimeType'] = 'image/png'
        binary.extend(png)
        count += 1
    if not count:
        print('No embedded WebP images; no changes made')
        return
    document['buffers'][0]['byteLength'] = len(binary)
    binary.extend(b'\0' * (-len(binary) % 4))
    encoded = json.dumps(document, separators=(',', ':')).encode('utf-8')
    encoded += b' ' * (-len(encoded) % 4)
    result = struct.pack('<III', magic, version, 12 + 16 + len(encoded) + len(binary))
    result += struct.pack('<II', len(encoded), 0x4E4F534A) + encoded
    result += struct.pack('<II', len(binary), 0x004E4942) + binary
    path.write_bytes(result)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('model', type=Path)
    convert(parser.parse_args().model)
