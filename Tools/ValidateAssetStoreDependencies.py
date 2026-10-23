"""Verify downloadable Asset Store binaries and optionally restore their importer settings."""
import argparse
import hashlib
import json
from pathlib import Path
import re


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--restore-import-settings', action='store_true',
                        help='Restore recorded .meta files for binaries with the expected hash.')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    manifest = json.loads((root / 'docs/ASSET_STORE_DEPENDENCIES.json').read_text(encoding='utf8'))
    checked = missing = mismatched = restored = 0
    for package in manifest['packages']:
        for item in package['files']:
            path = (root / item['path']).resolve()
            if not path.is_relative_to(root / 'Assets'):
                raise ValueError('Dependency path must stay inside Assets: ' + item['path'])
            if not path.is_file():
                print('MISSING:', item['path'], '| Import:', package['name'])
                missing += 1
                continue
            with path.open('rb') as source:
                digest = hashlib.file_digest(source, 'sha256').hexdigest()
            if digest != item['sha256']:
                print('CONTENT MISMATCH:', item['path'], '| Expected package:', package['name'])
                mismatched += 1
                continue
            meta = path.with_name(path.name + '.meta')
            if args.restore_import_settings and item.get('importSettings'):
                meta.write_text(item['importSettings'], encoding='utf8')
                restored += 1
            text = meta.read_text(encoding='utf8') if meta.exists() else ''
            if not re.search(r'^guid: ' + re.escape(item['guid']) + r'\s*$', text, re.MULTILINE):
                print('GUID MISMATCH:', item['path'])
                mismatched += 1
                continue
            checked += 1
    print(f'Verified: {checked}; missing: {missing}; mismatched: {mismatched}; import settings restored: {restored}')
    return 1 if missing or mismatched else 0


if __name__ == '__main__':
    raise SystemExit(main())
