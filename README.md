# The Hallowed Summoner

A 2D boss-encounter vertical slice for SWE402 (Game Development), built in Unity and inspired by *Hollow Knight*.
The Knight enters a Deepnest arena, the gates slam shut, and the fight ends in victory or defeat, with retry.

- Phase 1 proposal: [`docs/phase1/proposal.md`](docs/phase1/proposal.md)
- Phase 2 checkpoint: [`docs/phase2/checkpoint.md`](docs/phase2/checkpoint.md)
- Asset credits: [`docs/credits.md`](docs/credits.md)

## Requirements

- Unity **6000.5.11f1** (Unity 6.5) with the Windows Build Support module
- Packages (installed automatically): Universal RP (2D Renderer), Input System 1.20, Cinemachine 3.1.7

## Setup

1. Clone the repository: `git clone https://github.com/WackAjmi/SWE402-Game-Project.git`
2. Open the folder in Unity Hub with Unity 6000.5.11f1. The first import takes a few minutes.
3. Open `Assets/_Project/Scenes/Title.unity` and press **Play**.

## Controls

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Move | A / D or arrow keys | Left stick |
| Aim nail up / down | W / S or arrow keys | Left stick |
| Jump (press again in the air to double jump) | Space | A |
| Dash | Left Shift | Right trigger |
| Nail | Left mouse button or Enter | X |
| Focus (heal 1 mask for 33 soul) | F | - |
| Pause | Esc | Start |
| Menus | Arrow keys + Enter, or mouse | Left stick + A |

Down-slash an enemy or a closed gate to pogo off it.

## Building (Windows)

1. **File > Build Profiles**, then select **Windows**.
2. Check that the scene list is `Scenes/Title` (index 0) followed by `Scenes/Arena` (index 1).
3. Click **Build**, then choose an empty folder outside `Assets` (for example `Builds/Phase2`).
4. Run `The Hallowed Summoner.exe` (or `SWE402-Game-Project.exe`) from that folder.

## Project layout

| Folder | Contents |
| --- | --- |
| `Assets/_Project/Core` | Shared contracts: `GameEvents`, `Hitbox` / `Hurtbox`, `Services`, `GameSettings` |
| `Assets/_Project/PlayerSide` | The Knight, camera and feedback, HUD, menus and game flow (`GameFlow/`) |
| `Assets/_Project/BossSide` | Arena (gates, trigger, music, parallax, builder tool) and the placeholder boss (`Testing/`) |
| `Assets/_Project/Scenes` | `Title`, `Arena`, and the two sandbox scenes |

The player side and boss side are separate assembly definitions that never reference each other. They communicate only through `Core`.

Editor tools (under **Tools > Hollow Warden**): **Build Deepnest Arena**, **Add Arena Music**, **Make Gates Tinkable**, **Add Dummy Boss**, **Build Menus**.

## Team

| Member | Role |
| --- | --- |
| Mohammed Alajmi (202065020) | All Phase 2 development: player, arena |
| Ayman Al Johani (202016260) | Phase 2 development: game flow, UI, documentation |

## Known issues

See section 4.3 of the [Phase 2 checkpoint](docs/phase2/checkpoint.md). The boss is currently a placeholder; the full boss is scheduled for Phase 3 (Perhaps sooner).
