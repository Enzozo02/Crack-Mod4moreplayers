<p align="center">
  <img src="assets/CrackMorePlayers.png" width="220" alt="More Players for CRACK">
</p>

<h1 align="center">More Players for CRACK</h1>

<p align="center">
  <b>Play <a href="https://store.steampowered.com/app/4312190/CRACK/">CRACK</a> with up to 8 players: double rooms with bunk beds, unique colours and quotas that scale with the crew.</b><br>
  BepInEx 5 mod · by <b>NeZorSpy</b>
</p>

<p align="center">
  <a href="https://github.com/Enzozo02/Crack-Mod4moreplayers/releases/latest"><b>⬇️ Download v1.2.0</b></a> · <a href="CHANGELOG.md">Changelog</a>
</p>

<p align="center">
  <a href="https://github.com/Enzozo02/Crack-Mod4moreplayers/releases/latest"><img src="https://img.shields.io/github/v/release/Enzozo02/Crack-Mod4moreplayers?label=download&style=for-the-badge" alt="Download"></a>
  <img src="https://img.shields.io/badge/BepInEx-5.4.23-blue?style=for-the-badge" alt="BepInEx 5">
  <img src="https://img.shields.io/badge/status-stable-brightgreen?style=for-the-badge" alt="stable">
  <img src="https://img.shields.io/badge/players-up%20to%208-red?style=for-the-badge" alt="up to 8 players">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-lightgrey?style=for-the-badge" alt="MIT"></a>
</p>

---

## 🆕 What's new in v1.2.0

- 📈 **Quotas that scale with big crews**: work order targets keep growing up to 8 players, plus a new `QuotaMultiplier` setting (×1.25 at 8 players by default, ramped in from 5; 1-4 players keep the vanilla targets). See [How quotas scale](#how-quotas-scale).
- 🛒 **Shop unlocks in sync**: shop terminals now refresh as soon as a work order step is completed, even for players who joined mid-game.
- 📺 **Employee Productivity Ranking fixed**: the lobby monitor no longer overflows with 5+ players. It shows pages of 4 with a `1/2` indicator.

Full history in the [CHANGELOG](CHANGELOG.md).

## ✨ Features

| | |
|---|---|
| 👥 **Up to 8 players** | The Steam lobby, the Mirror network limit and the in-game player slots all follow one setting, `MaxPlayers` (default and maximum: 8) |
| 🛏️ **Double rooms** | The lab only has 4 crew quarters, so players 5-8 get a **bunk bed** above players 1-4, with posts that match the bed frame |
| 🚪 **Shared room access** | Your roommate's **badge door** and **wardrobe** work for you too, and each player keeps their own outfit |
| 🏷️ **Door plate** | Shows both roommates: `ENZO & PAUL` |
| 🌅 **Proper spawn** | Players 5-8 wake up on their own bunk instead of at the origin of the map |
| 🎨 **Unique colours** | Each player has their own colour (cards and graph on the end-of-shift screen, markers on the egg). Players 5-8: **purple, yellow, pink, teal** |
| 📜 **Lists that fit** | End-of-shift *Team Performance* scrolls by itself (follows each new card, then goes up and down). The lobby ranking monitors (*Employee Productivity Ranking* and the balance board) show **pages of 4** (1-4, then 5-8, every 6 s, with a `1/2` indicator) |
| 📈 **Quotas that scale** | Work order targets keep growing past 4 players (vanilla stops at the 4-player value for some steps), plus `QuotaMultiplier` (default ×1.25 at 8 players, ramped in from 5) |
| 🛒 **Shop in sync** | Shop terminals always match the current work order step, so catalogs unlock as soon as the step is completed, even for players who joined mid-game |
| 🧩 **MODS menu** | A new **MODS** button in the main menu lists every installed mod with its image, title, version, author and description (works for any BepInEx mod, see [For modders](#-for-modders-mods-menu-metadata)) |

Room assignment: player 5 → room 1 (upper bunk), 6 → room 2, 7 → room 3, 8 → room 4. Every player has their own bed and colour, so 8 is the maximum.

## ⚠️ Read before installing

- **Every player in the lobby must install the mod**, not just the host. CRACK detects BepInEx, flags the lobby as *modded*, and refuses to mix modded and vanilla players (`Error_ModdedMismatch`).
- **In modded mode the game turns off Steam achievements and leaderboards** (the run is "unranked"). That is the developer's choice, and **this mod does not bypass it**.
- The game is balanced for 4. With 8 players, the egg cracks faster.
- Every CRACK update can break the mod. Check `BepInEx/LogOutput.log` and the [Issues](https://github.com/Enzozo02/Crack-Mod4moreplayers/issues).

## 📥 Installation

1. Download **`CRACK_MorePlayers_v1.2.0.zip`** from the [**Releases**](https://github.com/Enzozo02/Crack-Mod4moreplayers/releases/latest) page.
2. In Steam: right-click **CRACK** → *Manage* → *Browse local files*.
3. Open the zip and copy **everything inside the `CRACK_MorePlayers` folder** (`winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and the `BepInEx` folder) **next to `CRACK.exe`**.
   ⚠️ Don't drop the `CRACK_MorePlayers` folder itself into the game folder, or BepInEx won't load.
   Your game folder should look like this:
   ```
   CRACK/
   ├── CRACK.exe
   ├── winhttp.dll            ← BepInEx loader
   ├── doorstop_config.ini
   └── BepInEx/
       ├── core/              ← BepInEx 5.4.23.3
       ├── config/nezorspy.crack.moreplayers.cfg
       └── plugins/
           ├── CrackMorePlayers.dll
           ├── CrackMorePlayers.png
           └── CrackMorePlayers.json
   ```
4. Launch the game. The main menu should now have a **MODS** button, and `BepInEx/LogOutput.log` should contain:
   ```
   [Info   :MorePlayers] MorePlayers 1.2.0 loaded, MaxPlayers = 8, QuotaMultiplier = 1.25
   [Info   :MorePlayers] Bunk bed created: room 1 -> player 5      (×4, once in the lab)
   ```

> **Steam Deck / Linux:** the zip contains the Windows build of BepInEx, so run the Windows version through Proton (*Properties → Compatibility → Force Proton*) and add `WINEDLLOVERRIDES="winhttp=n,b" %command%` to the launch options. macOS is not supported.

> **Already have BepInEx?** Only copy the files from `BepInEx/plugins/`.

> **Updating from an older version?** Replace `BepInEx/plugins/CrackMorePlayers.dll` and `CrackMorePlayers.json` (every player). Your config is kept, and new settings such as `QuotaMultiplier` are added to it on the next launch. If you used v1.0.0 before the GUID change, you can delete the old `enzo.crack.moreplayers.cfg`.

## ⚙️ Configuration

`BepInEx/config/nezorspy.crack.moreplayers.cfg` (created on the first launch):

```ini
[General]
## Maximum players in a lobby (host's value is the one that counts).
# Acceptable value range: From 1 to 8
MaxPlayers = 8

## Extra difficulty for big crews: multiplier on crew-scaled work order targets,
## ramped in from 5 players (x1 at 4) to its full value at 8. 1.0 = vanilla scaling.
# Acceptable value range: From 0.5 to 3
QuotaMultiplier = 1.25
```

Only the **host's** values matter: `MaxPlayers` decides how many players can join, and `QuotaMultiplier` sets the work order targets. Clients can keep any value, because bunk beds, colours and lists are always built for up to 8 players.

### How quotas scale

CRACK already scales most work order targets with the crew: `target = ceil(base × (1 + (players − 1) × 0.5))`. With more than 4 players, the mod adds two things:
- the steps that use a fixed per-crew table (scan / shred calcium, find crystals, shred grubs) keep growing past 4 players (for example `[2,3,3,4]` becomes 5, 6, 6, 7 for 5-8 players), where vanilla stays at the 4-player value;
- `QuotaMultiplier` is applied on top, ramped in from 5 players so there is no jump between 4 and 5.

| Step | 4 players | 5 | 6 | 7 | 8 (vanilla) | 8 (mod, ×1.25) |
|---|---|---|---|---|---|---|
| Peel shell | 75 | 96 | 119 | 143 | 135 | **169** |
| Deposit buckets | 5 | 7 | 8 | 10 | 9 | **12** |
| Shell / Membrane value | 7,500 | 9,563 | 11,813 | 14,250 | 13,500 | **16,875** |
| Flesh value | 17,500 | 22,313 | 27,563 | 33,250 | 31,500 | **39,375** |
| Calcite value | 62,500 | 79,688 | 98,438 | 118,750 | 112,500 | **140,625** |
| Hatchite value | 75,000 | 95,625 | 118,125 | 142,500 | 135,000 | **168,750** |

Personal goals (arcade score, slot win, upgrades, the Yolk) don't depend on the crew size, in vanilla or with the mod. Set `QuotaMultiplier = 1.0` to keep the vanilla curve.

## 🗑️ Uninstall

- Turn mods off without uninstalling: delete or rename `winhttp.dll`.
- Remove only this mod: delete the 3 `CrackMorePlayers.*` files in `BepInEx/plugins/`.
- Remove everything: delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and the `BepInEx/` folder.

## ❓ FAQ / Troubleshooting

<details><summary><b>"Error: modded mismatch" when joining</b></summary>
The host or the joining player doesn't have the mod (or BepInEx). Every player needs the same install.
</details>

<details><summary><b>Nothing changes and there's no MODS button</b></summary>
BepInEx isn't loading. Check that <code>winhttp.dll</code> is <b>directly</b> next to <code>CRACK.exe</code> (if you see a <code>CRACK_MorePlayers</code> folder in the game folder, move its contents up one level and delete it) and that a <code>BepInEx/LogOutput.log</code> file shows up after launching. On Linux/Steam Deck, make sure you run the Windows version through Proton with the launch option above.
</details>

<details><summary><b>The 5th player can't join</b></summary>
Check <code>MaxPlayers</code> in the <b>host's</b> config and that the log says <code>maxConnections 4 -> 8</code>. The host has to create a new lobby after changing the value.
</details>

<details><summary><b>"not found (game updated?)" in the log</b></summary>
CRACK was updated and renamed something the mod patches. Open an issue with your <code>LogOutput.log</code>.
</details>

<details><summary><b>Quotas feel too hard / too easy with a big crew</b></summary>
Change <code>QuotaMultiplier</code> in the <b>host's</b> config: <code>1.0</code> = vanilla scaling, <code>1.25</code> = default, up to <code>3.0</code>. It only applies with more than 4 players and takes effect from the next work order step.
</details>

<details><summary><b>The shop still shows a catalog as locked</b></summary>
Catalogs unlock when the work order step that lists them is <b>completed</b> (not when it starts). If a terminal still looks wrong after the step is done, open an issue with your <code>LogOutput.log</code> and say whether you were the host or a client.
</details>

<details><summary><b>Can I go above 8?</b></summary>
No. 8 is the maximum: the lab has 4 rooms with 2 beds each, and every player gets their own bed and colour. You can set a lower value (for example 6).
</details>

## 🔬 How it works

The full write-up is in [**docs/REVERSE_ENGINEERING.md**](docs/REVERSE_ENGINEERING.md). Short version:

- CRACK is **Unity 6 / Mono** with **Mirror + FizzySteamworks** networking, so BepInEx 5 and Harmony patch it directly.
- **Two locks** on the player count:
  - `NetworkManager.maxConnections` (Steam lobby size and Mirror). The mod raises it before the host starts.
  - A **hard-coded `4`** in `GameManager.GetAvailablePlayerIndex()`. A prefix replaces the search so it goes up to `MaxPlayers`.
- **Per-player objects** (`QuartersRoom`, `Bed`, `QuartersDoor`, `Wardrobe`) are matched on `PlayerController.index` (0-3):
  - `QuartersRoom.Get` folds the index (`i % 4`).
  - A **transpiler** adds `% 4` after each read of `index` in the 6 door and wardrobe checks.
- **Bunk beds:** the beds are **static-batched** (part of a `Combined Mesh` with world-space vertices that can't be read at runtime), so a simple clone is invisible. The bed geometry (1145 vertices) is extracted offline with `tools/extract_bed_mesh.py` and rebuilt at runtime. The posts use a single texel of the bed texture, so they match the frame colour.
- **Work order:** `WorkOrderStep.CrewAmount` clamps per-crew tables at 4 players, so a postfix extends them. A `DailyTaskManager.TargetFor` postfix applies `QuotaMultiplier` above 4 players. `ShopApp` only subscribes to step changes once at start, so a `ShopApp.Update` postfix refreshes it whenever `DailyTaskManager.StepIndex` changes.
- **UI built for 4:** the colour arrays (`ShiftCompleteUI`, `PlayerBlips`) are extended, the end-of-shift list gets a mask and auto-scroll, and both lobby ranking monitors (`EfficiencyDisplayApp`, `BalanceDisplayApp`) page their rows.
- **MODS menu:** the main menu's *Leaderboards* button and panel are cloned, their localization and events are rewired, and the panel is filled from `BepInEx.Bootstrap.Chainloader.PluginInfos`.
- The game's own **mod detection** (`ModDetection`, `modded` lobby flag, `RunRecord.Unrank`, achievements turned off) is **left untouched**.

## 🧩 For modders: MODS menu metadata

Any BepInEx plugin shows up in the **MODS** menu. To give yours an image and a description, put these next to your DLL:

```
BepInEx/plugins/MyMod.dll
BepInEx/plugins/MyMod.png      ← square image (256-512 px)
BepInEx/plugins/MyMod.json
```
```json
{ "title": "My Mod", "author": "You", "description": "What it does.\nSupports line breaks." }
```

The Thunderstore layout works too (a mod in its own folder with `icon.png` and `manifest.json`; the `name`, `author` and `description` keys are read). Without these files, the plugin's name and version from `[BepInPlugin]` are shown.

## 🛠️ Building from source

Requirements: the [.NET SDK](https://dotnet.microsoft.com/download) (6 or later) and CRACK with BepInEx 5 installed.

```bash
git clone https://github.com/Enzozo02/Crack-Mod4moreplayers.git
cd Crack-Mod4moreplayers/src
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\CRACK"
# -> bin/Release/CrackMorePlayers.dll  (copy it to BepInEx/plugins/ along with assets/CrackMorePlayers.png & .json)
```

The game's DLLs are referenced from **your** install through `GameDir` and are **never** committed here.

To regenerate the bed mesh after a game update:

```bash
pip install UnityPy numpy
python tools/extract_bed_mesh.py "C:\...\steamapps\common\CRACK" src/BedMeshData.cs
```

## 📁 Repository layout

```
src/        C# sources
            MorePlayers.cs  = player cap, slots, rooms, bunk beds
            Progression.cs  = work order quotas + shop unlock sync
            UiFixes.cs      = player colours, auto-scroll lists, ranking monitor pages
            ModMenu.cs      = MODS menu
            BedMeshData.cs  = generated bed mesh, ModsIconData.cs = button icon
assets/     mod image + metadata JSON shipped next to the DLL, MODS button icon
tools/      extract_bed_mesh.py (regenerates BedMeshData.cs from the game files)
docs/       REVERSE_ENGINEERING.md: full technical analysis
```

## 🤝 Contributing

Issues and PRs are welcome. A useful bug report includes your `BepInEx/LogOutput.log`, the game version (bottom-right of the main menu), the number of players, and who was hosting.

## 📜 Credits & license

- Mod: **NeZorSpy**, released under the [MIT license](LICENSE)
- [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1) and [HarmonyX](https://github.com/BepInEx/HarmonyX) (MIT), included unmodified in the release zip
- **CRACK** is made by **Rafoli**. This is an unofficial fan mod, not affiliated with or endorsed by the developer. It contains no code or assets from the game. Please support the game on [Steam](https://store.steampowered.com/app/4312190/CRACK/).
