### [Русский](./README_RU.md) | [> English <](./README.md) | [简体中文](./README_ZH.md)

---

# RadioReplacer

### Plugin author: [Mr_Dodep](https://t.me/MrDodep_1337)

A mod that replaces the game's radio with your own music. It works locally: other players still hear the original radio.

---

## Installation

1. Copy `RadioReplacer.dll` to `BepInEx/plugins/`.
2. Launch the game. The mod creates the track folders automatically.

---

## Music folders

After the first launch, these folders appear inside the game directory:

```text
BepInEx/plugins/RadioReplacer/
├── tracks_1/
├── tracks_2/
├── tracks_3/
├── tracks_4/
└── tracks_5/
```

Each folder corresponds to one in-game radio station.
Supported formats: `.ogg`, `.wav`.

Place your music files in the appropriate folder; the mod detects them automatically.

---

## Usage

1. Add tracks to `tracks_1` through `tracks_5`.
2. Enter the game and get into a vehicle.
3. Press **Alt + M** to open RadioReplacer.
4. Select a radio station and replace its tracks with your own.
5. Close the menu. The music changes immediately.

---

## Notes

- Replacements are local and audible only to you.

- More files in a folder mean more tracks on that station.

- An empty folder leaves the station's original music playing.

- Long tracks are supported without a file-size limit.
