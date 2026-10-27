# Reverse engineering notes: CRACK (v1.0, Steam app 4312190)

These are the findings the mod is built on. All analysis was done on a legally owned copy of the game, and **no game code or assets are redistributed in this repository**. The only data taken from the game is the bed geometry, and it is regenerated from *your* install by `tools/extract_bed_mesh.py`.

Tools used:
- `ikdasm` / `monodis` to disassemble `Assembly-CSharp.dll` to IL (~790k lines)
- small **Mono.Cecil** scripts to search the IL: every read of a field, every `ldc.i4.4`, every method touching the player index
- **UnityPy** to read the scenes (`level0` = main menu, `level1` = the lab) and the hierarchy, transforms, colliders and meshes
- **Harmony** dry-runs against the real assemblies (outside Unity) to check that every patch target resolves before shipping

---

## 1. Engine and networking stack

| Piece | Finding |
|---|---|
| Engine | Unity 6, **Mono** backend (`CRACK_Data/Managed/Assembly-CSharp.dll`, `MonoBleedingEdge/`). No IL2CPP, so BepInEx 5 + Harmony work directly |
| Netcode | **Mirror** (`Mirror.dll`) with the **FizzySteamworks** transport over Steam P2P |
| Steam | Steamworks.NET (`com.rlabrecque.steamworks.net.dll`) |
| Voice | Dissonance VoIP |
| Anti-cheat | None. The game has a **mod detector** instead (see §4) |

`MyNetworkManager : Mirror.NetworkManager` drives hosting and joining, and `SteamLobby` drives the Steam lobby.

## 2. Where the 4-player cap lives

### 2.1 `NetworkManager.maxConnections` (lobby and network)

The value is set in the scene (4) and read in 5 places:

| Method | Role |
|---|---|
| `SteamLobby.<HostLobbyRoutine>` | `SteamMatchmaking.CreateLobby(type, maxConnections)` |
| `SteamLobby.UpdateLobbyMemberLimit` | `SetLobbyMemberLimit(...)` while in game |
| `SteamLobby.OnLobbyMatchList` | quick-match skips lobbies with `members >= maxConnections` |
| `MyNetworkManager.SpawnPlayerAndSync` | refuses the spawn if `maxConnections - (players + pending) <= 0` |
| `JoinPanelController.Populate`, `PizzaPartyEvent.MaxCrew` | UI and event scaling (fallback `4` when there is no NetworkManager) |

FizzySteamworks receives the value from Mirror when the server starts. **Patch:** set `maxConnections = MaxPlayers` in `MyNetworkManager.Awake/Start`, `MyNetworkManager.Host` and `SteamLobby.HostLobby`.

### 2.2 The real lock: `GameManager.GetAvailablePlayerIndex`

```
IL_003f: ldloc.0
IL_0040: ldfld   int32 GameManager/'<>c__DisplayClass15_0'::i
IL_0045: ldc.i4.4          // <-- hard-coded 4
IL_0046: blt.s   IL_000f
IL_0048: ldc.i4.m1         // no free slot -> -1 -> player rejected
```

`PlayerController.OnStartServer` assigns `index = GetAvailablePlayerIndex()`. With 5 players, the 5th gets `-1`. **Patch:** a prefix that runs the same search up to `MaxPlayers`.

## 3. Everything indexed by the player index (0-3)

Each player owns a slot `PlayerController.index` that maps onto objects placed in the scene:

| Class | Lookup / check |
|---|---|
| `QuartersRoom` (`_playerIndex`) | `QuartersRoom.Get(i)` → used by `GameManager.GetSpawnPoint(i)` (spawn, respawn, `/unstuck`, kill-box recovery, save reload) |
| `Bed` (`_playerIndex`, static `Dictionary<int,Bed> _byPlayerIndex`) | `Bed.GetForPlayer(i)` (wake-up intro); `Bed.CanInteract` requires `player.index == _playerIndex` |
| `QuartersDoor` (`_assignedPlayerIndex`) | `ReaderHoldsAssignedPlayer`, `HasUnauthorizedBadge`, `ShowWaitForCrewTip` compare `index` to the door's owner |
| `Wardrobe` (`_playerIndex`) | `IsAssignedPlayer`, `ServerCycle`, `CmdEquip` compare `index` |
| `PlayerBlips.ColorForIndex` | already uses `i % playerColors.Length`, so it is safe |

With no changes, player 5 would spawn at `(0,0,0)` ("No QuartersRoom registered for player index 4") and could not use any bed, door or wardrobe.

**Patches:**
- `QuartersRoom.Get(i)`: `i → i % 4` (prefix)
- a transpiler inserts `Plugin.Home(index)` (= `index % 4`) after every `ldfld PlayerController::index` in the 6 door and wardrobe methods
- `Bed` is not folded: every player 5-8 gets their **own** bed (§5)

## 4. The game's mod detection (respected, not bypassed)

`ModDetection` scans the loaded assemblies for these prefixes: `0Harmony`, `HarmonyLib`, `BepInEx`, `MelonLoader`, `IPA.Loader`, `MonoMod.RuntimeDetour`, `UnityExplorer`. It also scans the game folder for `BepInEx`, `MelonLoader` and `doorstop_config.ini`.

When mods are found:
- the Steam lobby gets the data `modded = true`
- `SteamLobby.OnLobbyEntered` refuses to mix modded and vanilla players (`Error_ModdedMismatch`)
- `RunRecord.Unrank("mods or cheats")` takes the run off the leaderboards
- `ModDetection.Blocked` turns off Steam achievements and leaderboards

**This mod does not touch any of it.** That's why every player needs the mod, and why modded runs don't count for achievements or leaderboards. That is the developer's call, and we keep it.

## 5. Why "double rooms" and not 8 rooms

The quarters (`Quarters/` in `level1`) form a **half-octagon lobby** centred at world `(-33.5, 0, 0)`. The 4 `PF_QuartersRoom` prefabs (≈ 6 × 5 m inside, 4.3 m ceiling) are arranged in a fan at yaw 23°, 67°, 113° and 158°. The 2 sides left are plain walls (`PF_QuartersLobby_Wall`, at z = ±7.7), and on the other side of them the **Shop** and the **Deposit Room** start around |z| ≈ 14. There is no room for 4 extra quarters without cutting into other rooms.

Inside a room, the only free floor space is the walkway to the desk and computer. A second bed on the floor would block the PC or the door. So the solution is a **bunk bed**.

### 5.1 The static batching trap

The first attempt (`Instantiate(bed)`) produced an **invisible** bed. In the scene, the bed's `MeshFilter` points to `Combined Mesh (root: scene) 11` with `m_StaticBatchInfo = {firstSubMesh: 12, subMeshCount: 1}`. Unity merged it at build time, its vertices are stored **in world space**, and the mesh is `isReadable = false`. A clone renders nothing, and the geometry can't be read at runtime.

**Fix:** `tools/extract_bed_mesh.py` reads submesh 12 from `level1` (1145 vertices, 1012 triangles in 6 pieces: frame, headboard, footboard, slats, blanket, pillow) and moves it back into the bed's local space. It writes the result as base64 to `src/BedMeshData.cs`. At runtime the plugin:
1. clones the `Bed` (to keep the interaction and the `_sleepPoint`) 1.55 m higher and gives it `_playerIndex = i + 4`
2. removes the dead renderer and draws the rebuilt mesh with the bed's original material
3. adds 4 posts on the legs (x = ±1.53, z = ±0.81). Their UVs all point at **a single texel of the frame** in the texture atlas, so they come out exactly the colour of the wood (with plain cube UVs they showed the whole atlas, which looked like a rainbow)
4. fixes the `Bed._byPlayerIndex` registry, which the clone's `Awake` had overwritten

`GameManager.GetSpawnPoint` is shifted up by 1.55 m for players 5-8, so they wake up on their bunk. `QuartersRoom.Update` shows "NAME1 & NAME2" on the door plate.

## 6. Main menu ("MODS" entry)

Scene `level0`, `MenuCanvas`:
- `MainMenuController : PanelMenu` holds the panels (`_mainPanel`, `_hostPanel`, `_joinPanel`, `_settingsPanel`, `_controlsPanel`, `_leaderboardsPanel`) in `CanvasGroup[] _panels`. `PanelMenu.Show(CanvasGroup)` fades them.
- Buttons are `PF_MenuButton` (`UIShatter + Selectable`). The click fires `UIShatter._onShatter` (a `UnityEvent` with a persistent call to `ShowLeaderboards`, etc.). Labels use `LocalizedText`, which overwrites the text.
- The `Leaderboards` panel = `List` (frame) / `Title` / `Scroll View/Viewport/Content` (VerticalLayoutGroup + ContentSizeFitter) / back button.

**Patch (`MainMenuController.Awake` postfix):**
- clone `Holder (Leaderboards)`, remove its `LocalizedText`, replace `_onShatter` with a new event that opens our panel
- clone the `Leaderboards` panel, strip `LeaderboardPanel/Row/TabButton`, and fill `Content` with one card per BepInEx plugin
- append the panel to `_panels`

## 7. UI built for 4 players

| Where | Finding | Fix |
|---|---|---|
| `ShiftCompleteUI._playerColors` (Color[4]) | cards **and** graph lines use `_playerColors[i % Length]`, so player 5 gets player 1's colour | prefix on `BuildRows`: extend the array with distinct colours (purple, yellow, pink, teal, then more) |
| `PlayerBlips.playerColors` (Color[4]) | `ColorForIndex(i)` → `i % Length` | prefix on `ColorForIndex`: same extension |
| `ShiftCompleteUI._rowContainer` = `Sleep Content/Team/Panel` (VerticalLayoutGroup, 150 px cards) | only 4 cards fit, the 5th goes off-screen. The cursor is locked during this screen, so a mouse-wheel `ScrollRect` would not work | `RectMask2D` (top inset = header) + `AutoScroll`, which slides the content through `padding.top`: it follows new rows while they pop in, then ping-pongs with pauses |
| `DreamUI._lifetimeContent` (495×269) | same overflow at 8 | same `AutoScroll` |
| `EfficiencyDisplayApp` (*Employee Productivity Ranking*, `PF_App_Productivity`) and `BalanceDisplayApp` (world-space monitors, 320×250 canvas, 29 px rows in a 131 px list, `maxRows`) | the 5th row runs into the footer | `maxRows` raised; `RankingPager` turns on only the rows of the current page (4 per page, 6 s), with a `1/2` label |

## 8. Work order: quotas and shop unlocks

The daily task is a `WorkOrderTrack` (`Resources/Tasks`, 25 `WorkOrderStep`s) driven by `DailyTaskManager`, which has 3 SyncVars: `_stepIndex`, `_progress` and `_stepTarget`.

**Targets** (`DailyTaskManager.TargetFor(step)`):
- `amountByCrew` set (4 steps: ScanCalcium, CalciumShred `[2,3,3,4]`, FindCrystals `[1,2,3,3]`, ShredGrubs `[2,2,3,4]`) → `CrewAmount(players)` **clamps** the index to the last entry, so 5-8 players get the 4-player value. **Patch:** a `CrewAmount` postfix extends the table with its average step per player (ceil).
- `ScalesWithCrew` (verb in {Peel, DepositValue, DepositBuckets, Shred*, Scaffold, Bolts, Drive, Detonate, ScanDeposits, BurstArterySacks, FindDeposits, MineLayer}) → `ceil(amount × (1 + (players − 1) × crewScaleSlope))`, with `crewScaleSlope = 0.5` in the scene: already linear and unbounded.
- Everything else (arcade score, slot win, upgrades, Yolk) is a fixed personal goal.
- **Patch:** a `TargetFor` postfix multiplies crew-scaled targets by `1 + (QuotaMultiplier − 1) × (players − 4) / 4` when there are more than 4 players.

**Shop unlocks:** `WorkOrderTrack.IsCatalogUnlocked(cat, stepIndex)` = some step with `index < _stepIndex` lists the catalog in `unlockCatalogs`. The data is consistent everywhere, but `ShopApp.Start()` subscribes `OnShopStateChanged` to `DailyTaskManager.OnTaskStateChanged` / `ShopStateManager.OnStateChanged` **only if both instances exist at that moment**, and never retries (unlike `DailyTaskUI` / `UnlockWaypointAnnouncer`, which rebind in `LateUpdate`). **Patch:** a `ShopApp.Update` postfix watches `StepIndex` and calls `OnShopStateChanged` whenever it changes.

## 9. Checking patches against a game update

Every patch is a named method. If an update renames something, BepInEx logs a Harmony error or `Method X not found (game updated?)`. Run the Cecil and Harmony dry-run again (see `CONTRIBUTING` in the README) to find what moved.
