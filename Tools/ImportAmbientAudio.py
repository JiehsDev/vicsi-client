"""Import the approved CC0 ambience with original downloads and reproducible loop preparation."""
import hashlib
import html
import io
import json
from pathlib import Path
import re
import subprocess
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
LICENSES = ROOT / 'docs/licenses/ambient-audio-2026-10-22'
ASSETS = ROOT / 'Assets/_Project/Audio/Ambience'
CC0 = 'https://creativecommons.org/publicdomain/zero/1.0/'

def fetch(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'}), timeout=60) as response:
        return response.read()

def main():
    import numpy as np
    import soundfile as sf
    ASSETS.mkdir(parents=True, exist_ok=True)
    (LICENSES / 'Originals').mkdir(parents=True, exist_ok=True)
    (LICENSES / 'CC0-1.0.html').write_bytes(fetch(CC0))
    entries = []
    for name, creator, title, page, audio in [
        ('yard-residential', 'mzui', 'Residential Street Ambience Quiet Birds.wav', 'https://freesound.org/people/mzui/sounds/135074/', None),
        ('house-room-tone', 'visionear', 'Room tone Very quiet Small apartment room loopable edlarez vsnr.wav', 'https://freesound.org/people/visionear/sounds/565535/', None),
        ('yard-tree-rustle', 'Joseph SARDIN', 'Wind in the Trees', 'https://bigsoundbank.com/forest-wind-in-the-trees-s0904.html', 'https://bigsoundbank.com/UPLOAD/mp3/0904.mp3'),
    ]:
        if audio is None:
            body = fetch(page).decode('utf-8')
            (LICENSES / (name + '.html')).write_text(body, encoding='utf-8')
            if 'creativecommons.org/publicdomain/zero/1.0' not in body:
                raise ValueError('Item no longer verifies CC0: ' + page)
            audio = html.unescape(re.search(r'data-static-file-url="([^"]+)"', body).group(1))
        # Original WAVs on Freesound require login. Use its public HQ preview, as in the interaction SFX import.
        original = LICENSES / 'Originals' / (name + '.mp3')
        if not original.exists():
            if 'bigsoundbank.com' in audio:
                with urllib.request.urlopen(audio, timeout=60) as response: original.write_bytes(response.read())
            else:
                original.write_bytes(fetch(audio))
        data = original.read_bytes()
        try:
            samples, rate = sf.read(io.BytesIO(data), dtype='float32', always_2d=True)
        except sf.LibsndfileError:
            import imageio_ffmpeg
            decoded = subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), '-v', 'error', '-i', str(original),
                '-f', 'wav', '-acodec', 'pcm_s16le', 'pipe:1'], capture_output=True, check=True)
            samples, rate = sf.read(io.BytesIO(decoded.stdout), dtype='float32', always_2d=True)
        if samples.shape[1] != 2 or len(samples) < 10 * rate:
            raise ValueError('Expected a stereo ambience recording: ' + name)
        if name == 'yard-tree-rustle':
            # Select a quieter 60-second region without manufacturing additional scene events.
            size = 60 * rate
            starts = range(0, len(samples) - size + 1, 5 * rate)
            start = min(starts, key=lambda i: float(np.mean(samples[i:i+size:64] ** 2)))
            excerpt = samples[start:start+size].copy()
        else:
            start = 0; excerpt = samples.copy()
        overlap = int(rate * (1 if name == 'house-room-tone' else 2))
        phase = np.linspace(0, 1, overlap, endpoint=False, dtype='float32')[:, None]
        blend = excerpt[-overlap:] * (1-phase) + excerpt[:overlap] * phase
        loop = np.concatenate((excerpt[overlap:-overlap], blend))
        rms = float(np.sqrt(np.mean(loop ** 2)))
        if rms <= 0: raise ValueError('Silent source: ' + name)
        gain = min(16.0, .04 / rms, .8 / float(np.max(np.abs(loop))))
        loop *= gain
        target = ASSETS / (name + '.wav')
        sf.write(target, loop, rate, subtype='PCM_16')
        boundary = float(np.max(np.abs(loop[-1] - loop[0])))
        entries.append(dict(asset=target.relative_to(ROOT).as_posix(), title=title, creator=creator,
            sourceUrl=page, downloadUrl=audio, license='CC0-1.0', licenseUrl=CC0, attributionRequired=False,
            acquired='2026-10-22', original=original.relative_to(ROOT).as_posix(),
            acquisitionType='Publisher public HQ MP3 preview; original WAV requires login' if 'freesound' in page else 'Publisher public MP3 download',
            originalSha256=hashlib.sha256(data).hexdigest(), assetSha256=hashlib.sha256(target.read_bytes()).hexdigest(),
            sampleRate=rate, channels=2, sourceSeconds=len(samples)/rate, excerptStartSeconds=start/rate,
            excerptSeconds=len(excerpt)/rate, loopSeconds=len(loop)/rate, overlapSeconds=overlap/rate, gain=gain,
            rms=float(np.sqrt(np.mean(loop**2))), peak=float(np.max(np.abs(loop))), loopBoundaryJump=boundary,
            processing='Stereo preserved; tail/head linear overlap crossfade; bounded RMS gain toward .04 (maximum 16x, peak capped at .8); PCM16 WAV.'))
        print(name, round(len(loop)/rate, 3), 'seconds; seam', round(boundary, 6), flush=True)
    (LICENSES / 'manifest.json').write_text(json.dumps(entries, indent=2), encoding='utf-8')

if __name__ == '__main__': main()
