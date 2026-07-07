# OpenLegacy Player

**The classic Windows Media Player experience, reborn in full Frutiger Aero.**

OpenLegacy Player is a loving recreation of *Windows Media Player Legacy* — the
glossy blue orbs, the glass panels, the light streaks and floating bubbles of the
Windows Vista/7 era — rebuilt from scratch as a modern, open-source music player
for Windows 10 and 11.

![OpenLegacy Player](docs/screenshot.png)

<p align="center">
  <img src="docs/nowplaying.png" width="49%" alt="Now Playing with visualizer" />
  <img src="docs/playlist.png" width="49%" alt="Playlists" />
</p>

---

## ⬇️ Download — v0.2.0

Grab the latest version from the
[**Releases page**](https://github.com/NokaAngel/OpenLegacyPlayer/releases):

| | |
|---|---|
| **`OpenLegacyPlayer-Setup-x.y.z.exe`** | Installer — Start menu entry, optional desktop icon, clean uninstall |
| **`OpenLegacyPlayer-Portable-x.y.z.zip`** | Portable — unzip anywhere and run, nothing to install |

Both are self-contained: **no .NET installation required.** Windows 10/11, 64-bit.

---

## ✨ What's in v0.2.0

### The Aero experience
- Full **Frutiger Aero interface**: glass window chrome, gradient toolbars, the
  glossy blue play orb, floating glass bubbles, diagonal light streaks, and
  liquid-fill sliders with glass-ball thumbs.
- A redesigned **Now Playing** view — deep-blue glass backdrop, glow halo behind
  the cover art, a mirrored reflection beneath it, and a **WMP-style "Bars"
  visualizer** with peak caps and water reflections that dances while music plays.
- Custom chrome that still behaves like a real window: snapping, resizing,
  maximizing to the work area (your taskbar stays visible), and a red-glow
  close button straight out of 2007.

### Your music
- **Library** — add folders or files; title, artist, album, genre, year, duration
  and cover art are read automatically from the tags.
- **Drive browsing** — your fixed and USB drives appear right in the navigation
  pane. Select one and it finds every audio file on it, live-streamed into a list
  grouped by folder — no freezing, even with tens of thousands of files.
- **Playlists** — create playlists and add tracks with a right-click. Stored as
  standard **`.m3u`** files, so they work in any other player too.
- **Views** — All music, Artist, Album, Genre, Videos and Pictures, with the
  classic group headers, blue title links and gold star ratings.

### Quality of life
- Everything **persists between sessions**: your library, playlists, volume,
  shuffle/repeat, and window size all come back the way you left them.
- **Settings** dialog (*Organize ▸ Settings…*) with quick access to your data
  folders and an update-check toggle.
- Built-in **update checker** — the app quietly checks this repository's releases
  on startup and lets you know when a new version is out. It never downloads
  anything without you.
- Working **back / forward** navigation, live search, and a streaming scan
  progress bar in true WMP style.

---

## 🚀 Getting started

1. **Add music** — *Organize ▸ Add folder to library…*, or just click a drive in
   the left pane to browse everything on it without importing.
2. **Play** — double-click any track. Use the transport bar for shuffle, repeat,
   seek and volume.
3. **Now Playing** — click the ⤢ button at the bottom-right (or the album
   thumbnail) for the full-screen Aero scene with the visualizer.
4. **Playlists** — *Create playlist* on the toolbar, then right-click tracks ▸
   *Add to playlist*.

Your data lives in `%AppData%\OpenLegacyPlayer` — the library index
(`library.json`), playlists (`Playlists\*.m3u`) and settings (`settings.json`).
Delete that folder and the app starts completely fresh.

---

## 📌 Good to know

- The Now Playing visualizer is **decorative** (like the classic skins) rather
  than a true spectrum analyzer — a real one is on the roadmap.
- The **Burn** tab and **Stream** button are cosmetic nods to the original for
  now.
- Settings marked *(coming soon)* — color themes, mini player, crossfade,
  session resume — are planned, not yet functional.

## 🗺 Roadmap

- Color themes (Energy Blue, Emerald, Violet)
- Album / Artist tile views with cover art
- Mini / compact player mode
- Editable ratings and metadata
- More visualizer styles, true audio-reactive rendering
- Video playback surface in Now Playing
- Drag-and-drop playlist reordering

Found a bug or have an idea? [Open an issue](https://github.com/NokaAngel/OpenLegacyPlayer/issues)!

## 📄 License

[MIT](LICENSE). OpenLegacy Player is a fan recreation made for the love of an
era; it is not affiliated with or endorsed by Microsoft. "Windows Media Player"
is a trademark of Microsoft Corporation.

---

> *Built with a healthy dose of nostalgia for the glassy, glossy Aero years.* 💧
