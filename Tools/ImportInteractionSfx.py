"""Download the approved interaction SFX from their publishers; retain provenance.

Run with PYTHONPATH pointing to a temporary soundfile installation outside Unity's Temp.
Requires soundfile/numpy; no account/private downloads.
"""
import hashlib
import html
import io
import json
from pathlib import Path
import re
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / "docs/evidence/interaction-sfx-2026-10-22"
LICENSES = ROOT / "docs/licenses/interaction-sfx-2026-10-22"
ASSETS = ROOT / "Assets/_Project/Audio/InteractionSfx"
HEADERS = {"User-Agent": "Mozilla/5.0"}


def fetch(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=HEADERS), timeout=45) as response:
        return response.read()


def page(url, name):
    target = LICENSES / (name + ".html")
    if not target.exists():
        target.write_bytes(fetch(url))
    return target.read_text(encoding="utf-8")


def main():
    for folder in (EVIDENCE, LICENSES, ASSETS, LICENSES / "Originals"):
        folder.mkdir(parents=True, exist_ok=True)
    manifest = []
    import numpy as np
    import soundfile as sf

    def save(name, data, source, title, creator, license_url, acquisition, archive_member=None):
        original = LICENSES / "Originals" / (name + Path(acquisition.split("?")[0]).suffix)
        original.write_bytes(data)
        samples, rate = sf.read(io.BytesIO(data), dtype="float32", always_2d=True)
        samples = samples.mean(axis=1)
        # Remove leading/trailing dead air without cutting transients; short fades prevent clicks.
        audible = np.flatnonzero(np.abs(samples) > .001)
        if len(audible):
            samples = samples[max(0, audible[0]-int(rate*.008)):min(len(samples), audible[-1]+int(rate*.025))]
        limit={"pen-writing":1.1,"paper-package":.9,"tape-peel":.9,"tape-measure":.65}.get(name)
        if limit:
            samples=samples[:int(rate*limit)]
        peak = float(np.max(np.abs(samples)))
        if peak == 0:
            raise ValueError("Silent recording: " + name)
        samples *= min(1.0, .8 / peak)
        fade = min(int(rate*.004), len(samples)//2)
        samples[:fade] *= np.linspace(0, 1, fade)
        samples[-fade:] *= np.linspace(1, 0, fade)
        path = ASSETS / (name + ".wav")
        sf.write(path, samples, rate, subtype="PCM_16")
        manifest.append(dict(asset=str(path.relative_to(ROOT)).replace("\\", "/"), title=title, creator=creator,
            sourceUrl=source, licenseUrl=license_url, attributionRequired=False, acquired="2026-10-22",
            downloadUrl=acquisition, archiveMember=archive_member, original=str(original.relative_to(ROOT)).replace("\\", "/"),
            originalSha256=hashlib.sha256(data).hexdigest(), assetSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
            seconds=len(samples)/rate, sampleRate=rate, channels=1,
            processing="Mono downmix, dead-air trim with margins, peak capped at 0.8 without gain boost, 4 ms edge fades, PCM16 WAV.",
            maximumExcerptSeconds=limit))
        print(name, round(len(samples)/rate, 3), "s", flush=True)

    page("https://creativecommons.org/publicdomain/zero/1.0/", "CC0-1.0")
    page("https://kenney.nl/support", "Kenney-support")
    packs = {}
    for slug in ("interface-sounds", "impact-sounds"):
        url = "https://kenney.nl/assets/" + slug
        body = page(url, "Kenney-" + slug)
        download = re.search(r"href=['\"]([^'\"]+\.zip)['\"]", body).group(1)
        archive_path = EVIDENCE / (slug + ".zip")
        if not archive_path.exists():
            archive_path.write_bytes(fetch(download))
        archive = zipfile.ZipFile(archive_path)
        packs[slug] = (archive, url, download)
        for filename in archive.namelist():
            if "license" in filename.lower() and filename.lower().endswith(".txt"):
                (LICENSES / (slug+"-LICENSE.txt")).write_bytes(archive.read(filename))
        print(slug, archive.namelist()[:18], flush=True)

    # Exact archive candidates are selected from the publisher's actual file listing.
    (EVIDENCE / "pack-files.json").write_text(json.dumps({key:a.namelist() for key,(a,_,_) in packs.items()}, indent=2))
    ui, ui_page, ui_download = packs["interface-sounds"]
    impacts, impact_page, impact_download = packs["impact-sounds"]
    for name, archive, source, download, pattern in [
        ("ui-select", ui, ui_page, ui_download, r"(?:^|/)click_001\.ogg$"),
        ("ui-confirm", ui, ui_page, ui_download, r"(?:^|/)click_002\.ogg$"),
        ("ui-blocked", ui, ui_page, ui_download, r"(?:^|/)error_001\.ogg$"),
        ("object-place", impacts, impact_page, impact_download, r"(?:^|/)impactSoft_medium_000\.ogg$"),
    ]:
        matches = [s for s in archive.namelist() if re.search(pattern, s)]
        if not matches:
            print("NEEDS SELECTION", name, flush=True)
            continue
        member = matches[0]
        save(name, archive.read(member), source, Path(member).name, "Kenney", "https://creativecommons.org/publicdomain/zero/1.0/", member, member)
        manifest[-1]["downloadUrl"] = download
    for surface in ("wood", "concrete", "grass"):
        for index in range(3):
            matches = [s for s in impacts.namelist() if s.endswith(f"footstep_{surface}_{index:03d}.ogg")]
            if matches:
                member = matches[0]
                save(f"footstep-{surface}-{index}", impacts.read(member), impact_page, Path(member).name, "Kenney",
                     "https://creativecommons.org/publicdomain/zero/1.0/", member, member)
                manifest[-1]["downloadUrl"] = impact_download

    for name, sound_id in (("camera-shutter",170229),("tape-measure",565501),("chalk-contact",398491)):
        source = f"https://freesound.org/s/{sound_id}/"
        body = page(source, "Freesound-"+str(sound_id))
        if "creativecommons.org/publicdomain/zero/1.0" not in body:
            raise ValueError("CC0 not verified: " + source)
        preview = html.unescape(re.search(r'data-static-file-url="([^"]+)"', body).group(1))
        title = html.unescape(re.search(r"<title>(.*?)</title>", body, re.S).group(1)).strip()
        save(name, fetch(preview), source, title, title.split(" by ")[-1], "https://creativecommons.org/publicdomain/zero/1.0/", preview)
        manifest[-1]["downloadType"] = "Publisher's publicly offered HQ preview under the same CC0 sound license; original download requires login."

    page("https://mixkit.co/license/modal/sfxFree/", "Mixkit-SFX-Free")
    page("https://mixkit.co/terms/", "Mixkit-terms")
    page("https://mixkit.co/free-sound-effects/", "Mixkit-no-attribution")
    for category, choices in {
        "paper": {"pen-writing":"Signing on a paper", "paper-package":"Paper crinkle", "paper-page":"Page turn single", "tape-peel":"Duct tape bandage"},
        "click": {"flashlight-switch":"On or off light switch tap", "container-close":"Plastic bubble click"},
    }.items():
        source = "https://mixkit.co/free-sound-effects/"+category+"/"
        body = page(source, "Mixkit-"+category)
        cards = re.split(r'<div class="item-grid__item">', body)
        for name, title in choices.items():
            card = next((s for s in cards if re.search(r'<h2[^>]*>\s*'+re.escape(title)+r'\s*</h2>',s)), None)
            if not card:
                raise ValueError("Publisher item not found: " + title)
            item_id = re.search(r'data-audio-player-item-id-value="(\d+)"', card).group(1)
            modal_url = "https://mixkit.co/free-sound-effects/download/"+item_id+"/"
            modal = page(modal_url, "Mixkit-download-"+item_id)
            links = re.findall(r'(?:href|data-download-url|data-download--modal-url-value)=[\"\x27]([^\"\x27]+)',modal)
            download = next((html.unescape(s) for s in links if '.wav' in s or '.mp3' in s), None)
            if not download:
                raise ValueError("Official download link missing for " + title)
            save(name, fetch(download), source, title, "Mixkit", "https://mixkit.co/license/modal/sfxFree/", download)
    (LICENSES / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
