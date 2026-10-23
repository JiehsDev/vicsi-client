# CC0 scene ambience

Three edited stereo loops for the CSI_Environment house and yard:

- `yard-residential.wav`: Residential Street Ambience Quiet Birds.wav by mzui, Freesound 135074. Approximately 73.33 seconds after a two-second overlap crossfade.
- `house-room-tone.wav`: Room tone Very quiet Small apartment room loopable edlarez vsnr.wav by visionear, Freesound 565535. Approximately 20.31 seconds after a one-second overlap crossfade.
- `yard-tree-rustle.wav`: Wind in the Trees by Joseph SARDIN, BigSoundBank 0904. A quieter 60-second excerpt, approximately 58 seconds after a two-second overlap crossfade.

All three source items explicitly list CC0 1.0; attribution is not required. The two Freesound files were acquired from their publicly offered HQ MP3 previews because original WAV downloads require login. The tree recording uses the publisher's public MP3 download. These derived PCM16 WAV files are not the original lossless recordings.

Source URLs, creators, original downloads, license evidence, exact edits and SHA-256 hashes are retained in `docs/licenses/ambient-audio-2026-10-22/`. Preparation can be reproduced with `Tools/ImportAmbientAudio.py` using numpy, soundfile and imageio-ffmpeg in a temporary Python dependency folder outside the project.

The saved **Scene ambience - CC0 house and yard** object owns the sources. Adjust volumes, transition timing, indoor leakage, exterior muffling and dialogue ducking on its SceneAmbience component. The three sources preserve stereo and import as streaming Vorbis. There is no lobby bootstrap or added collision geometry.
