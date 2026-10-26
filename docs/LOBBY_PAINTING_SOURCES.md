# Lobby painting sources

The lobby's four geometric placeholder prints have been replaced with these museum paintings:

- Vincent van Gogh, *Self-Portrait with a Straw Hat*, 1887: https://www.metmuseum.org/art/collection/search/436532
- Vincent van Gogh, *Roses*, 1890: https://www.metmuseum.org/art/collection/search/436534
- Vincent van Gogh, *Cypresses*, 1889: https://www.metmuseum.org/art/collection/search/437980
- Edouard Manet, *The Monet Family in Their Garden at Argenteuil*, 1874: https://www.metmuseum.org/art/collection/search/436965

Source: The Metropolitan Museum of Art. All four image records returned `isPublicDomain: true` through the museum's official collection API when downloaded on 2026-10-26. Open Access policy: https://www.metmuseum.org/policies/image-resources (CC0).

Downloaded museum web-large JPEG files are stored unmodified in `Assets/_Project/Art/Decor/LobbyPaintings`; `sources.json` contains direct image URLs and item metadata. Combined image download size: 902,957 bytes. Texture imports use mipmaps, clamp wrapping, compression and a 1024 maximum size. Materials use URP/Lit with a matte surface.

The existing oak frames and cream backing remain in place. Each painting is fitted at its original aspect ratio inside its frame. The twenty geometric line objects were removed. This change is applied to Main in both research and competitive projects. The lobby builder now uses the same paintings when restyling; the focused editor action `ViCSI > Scene Setup > Replace Lobby Paintings` can reapply them without rebuilding the room.

Checked in the Unity editor: four painting surfaces, zero placeholder lines, saved Main scene, successful script compilation. No APK was rebuilt or installed.
