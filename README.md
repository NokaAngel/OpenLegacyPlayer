# OpenLegacy Player

**The classic Windows Media Player experience, reborn in full Frutiger Aero.**

OpenLegacy Player is a loving recreation of *Windows Media Player Legacy* — the
glossy blue orbs, the glass panels, the light streaks and floating bubbles of the
Windows Vista/7 era — rebuilt from scratch as a modern, open-source music player
for Windows 10 and 11.

![OpenLegacy Player](docs/screenshot.png)

<p align="center">
  <img src="docs/alchemy.png" width="49%" alt="Alchemy visualization" />
  <img src="docs/bars.png" width="49%" alt="Bars and Waves visualization" />
</p>
<p align="center">
  <img src="docs/purplehaze.png" width="49%" alt="Battery: purple haze" />
  <img src="docs/scope.png" width="49%" alt="Scope visualization" />
</p>

---

## ⬇️ Download — v0.3.0

Grab the latest version from the
[**Releases page**](https://github.com/NokaAngel/OpenLegacyPlayer/releases):

| | |
|---|---|
| **`OpenLegacyPlayer-Setup-x.y.z.exe`** | Installer — Start menu entry, optional desktop icon, clean uninstall |
| **`OpenLegacyPlayer-Portable-x.y.z.zip`** | Portable — unzip anywhere and run, nothing to install |

Both are self-contained: **no .NET installation required.** Windows 10/11, 64-bit.

---

## 🚀 Getting started

1. **Add music** — *Organize ▸ Add folder to library…*, or just click a drive in
   the left pane to browse everything on it without importing.
2. **Play** — double-click any track. Use the transport bar for shuffle, repeat,
   seek and volume.
3. **Now Playing** — click the ⤢ button at the bottom-right (or the album
   thumbnail) for the full-screen Aero scene, then **right-click** to switch
   between Album art, Bars and Waves, and Alchemy.
4. **Playlists** — *Create playlist* on the toolbar, then right-click tracks ▸
   *Add to playlist*.

Your data lives in `%AppData%\OpenLegacyPlayer` — the library index
(`library.json`), playlists (`Playlists\*.m3u`) and settings (`settings.json`).
Delete that folder and the app starts completely fresh.

---

## 📌 Good to know

- Playback uses the system's Media Foundation codecs — mp3, m4a/aac, wma, wav
  and flac all play out of the box on Windows 10/11.
- The **Burn** tab and **Stream** button are cosmetic nods to the original for
  now.
- Settings marked *(coming soon)* — color themes, mini player, crossfade,
  session resume — are planned, not yet functional.

## 🗺 Roadmap

- Color themes (Energy Blue, Emerald, Violet)
- Album / Artist tile views with cover art
- Mini / compact player mode
- Editable ratings and metadata
- More Alchemy and Battery presets
- Video playback surface in Now Playing
- Drag-and-drop playlist reordering

Found a bug or have an idea? [Open an issue](https://github.com/NokaAngel/OpenLegacyPlayer/issues)!

## 📄 License

[MIT](LICENSE). OpenLegacy Player is a fan recreation made for the love of an
era; it is not affiliated with or endorsed by Microsoft. "Windows Media Player"
is a trademark of Microsoft Corporation.

---

> *Built with a healthy dose of nostalgia for the glassy, glossy Aero years.* 💧
