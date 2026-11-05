### [Русский](./README_RU.md) | [> English <](./README.md) | [简体中文](./README_ZH.md)

---

# Cinematic Camera Plus

### Plugin author: [Mr_Dodep](https://t.me/MrDodep_1337)

A mod for cinematic camera adjustments and visual effects. All changes are local and visible only to you.

---

## Installation

1. Copy `CinematicCameraPlus.dll` to `BepInEx/plugins/`.
2. Launch the game.
3. Press **Alt + C** to open the mod menu.

---

## Menu tabs

### Camera

- **FOV** — camera field of view (20°–120°), with shortcuts for 60°, 75°, 90°, 45° Cinematic and 35° Portrait

- **Camera roll** — left/right tilt (-45° to +45°), with -15° / 0° / +15° buttons

- **Letterbox** — cinematic black bars in 2.39:1 / 2.35:1 / 1.85:1 aspect ratios

### Color

- **Brightness** — -1.0 to +1.0

- **Contrast** — 0.5 to 2.0

- **Saturation** — 0.0 to 2.0 (0 = black and white)

- **Gamma** — 0.5 to 2.0

### Effects

- **Vignette** — darkened screen edges

- **Chromatic aberration** — color shifts around the edges

- **Scanlines** — horizontal lines resembling an old monitor

- **Film grain** — random noise each frame

- **Night vision** — green tint and darkening

- **Sepia** — brown and yellow tones

- **Infrared** — red tint simulating an IR camera

- **Fog / haze** — a milky-white overlay

- **Old TV** — interference lines, a rolling scanline and yellowing

- **Sunset** — a warm orange → gold → purple gradient

### Presets

Built-in presets change FOV, color and effects together:

| Preset | Description |
|---|---|
| Cinematic | FOV 50, increased contrast, vignette, letterbox |
| Noir | Black and white, high contrast, grain |
| Sunset | Warm tones, increased saturation |
| Cold Night | Blue tint, darkening, chromatic aberration |
| Vintage | Grain, scanlines, old-film look |
| Horror | Dark green tint, vignette |
| Vivid | Bright colors, increased saturation |
| Default | Reset all settings |

---

## Notes

- FOV adjustments work even if another plugin tries to overwrite them.

- All effects can be disabled with the "Disable all effects" button.

- Press **Alt + C** again or right-click to close the menu.
