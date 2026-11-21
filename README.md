# GR_final: 2D Fighting Game with a Reinforcement Learning CPU

A 2D one-on-one fighting game made in Unity, built for the bachelor thesis
**"Building a Fighting Game with a Reinforcement Learning CPU"** (9 Sep 2026 – 12 Jan 2027).

The game has the core features of a traditional fighting game: walking, attacks with frame data,
blocking, hitstun, blockstun, health and rounds. It also has a rule-based **scripted CPU**, which is
the baseline and the training partner for an **RL agent** trained with Unity ML-Agents.

---

## Status

| Area | State |
|---|---|
| Movement, facing, stage walls | Done |
| Attack with startup / active / recovery frame data | Done (1 attack: Stab) |
| Blocking, chip damage, hitstun, blockstun, pushback | Done |
| Counter hit and punish counter | Done |
| Corner pushback (attacker is pushed when the defender is cornered) | Done |
| Fair trades (both hits on the same tick land) | Done |
| Input buffer (6 ticks) | Done |
| Extended hurtbox (the sword arm can be hit during an attack) | Done |
| Health bars with a sliding damage trail | Done |
| Rounds: 100 s timer, first to 2, KO / time out / draws, round HUD | Done |
| Data-driven characters (CharacterData assets) | Done |
| Palette swap for mirror matches | Done |
| Menu → Character Select → Fight scene flow | Done |
| Opponents: training Dummy (Idle / Attack / Block) and scripted CPU | Done |
| Crouching, crouch hurtbox, crouching block, attack heights (mid / low) | Done (the low attack needs its data and animation) |
| Target combo, knockdown | Planned |
| ML-Agents agent, training scene, evaluation | Planned |

---

## Requirements

- **Unity 6000.6.3f1** (Unity 6), project set up for **URP 2D**
- Packages (already in `Packages/manifest.json`): Input System, TextMeshPro, 2D Aseprite Importer
- Later, for training: the ML-Agents Unity package plus Python with `mlagents` and PyTorch

## Getting started

1. Open the project folder in Unity Hub with Unity **6000.6.3f1**.
2. Check **File → Build Profiles → Scene List**. The order should be:
   1. `Assets/Scenes/Menu`
   2. `Assets/Scenes/CharacterSelect`
   3. `Assets/Scenes/FightingScene`
3. Open **Menu** and press **Play**.

You can also press Play in **FightingScene** on its own. `MatchSetup` then uses its default characters and CPU mode.

## Controls

| Key | Action |
|---|---|
| **A / D** | Walk left / right |
| Hold **away from the opponent** | Block (standing block: stops mid attacks) |
| Hold **S** | Crouch (can't walk; **S + back** = crouching block, stops mids and lows) |
| **J** | Attack: the Stab while standing, the crouching attack (e.g. a low) while crouching |
| **R** / **Shift + R** | Restart the round / the whole match (testing) |
| **1 / 2 / 3** | Against the Dummy: switch it to Idle / Attack / Block |
| **Escape** | In a fight: back to Character Select |

The bindings live in `Assets/Input Systems/PlayerInput.inputactions`. The code looks up the actions by name
(**Walk**, **Attack**), so you can change the keys there freely.

## Game flow

```
Menu ──Play──► CharacterSelect ──Start──► FightingScene
 ▲   ◄──Back (nothing picked)──┘      ▲         │
 │                                    └─Escape──┘
Exit → quits the game (stops Play mode in the Editor)
```

On **Character Select**, player 1 picks a fighter, then the opponent's fighter is picked, then the opponent type
(**Dummy** or **CPU**) is chosen, then Start.
The picks are carried to the fight scene in `MatchSettings`.

---

## Gameplay rules

The game runs on a fixed tick of **1/60 s**. All timing is counted in ticks (1 tick = 1 frame at 60 fps).

### Attacks
Each attack has three phases, set per attack in its `CharacterData`:

```
Stab:  startup 4 | active 3 | recovery 7   (14 ticks total)
```

- **Startup**: winding up, no hitbox yet
- **Active**: the hitbox can hit (once per swing)
- **Recovery**: pulling back, the fighter is vulnerable

The fighter can't walk, block or turn around until the attack ends.

### Hits and blocks
| Situation | Result |
|---|---|
| Defender holding back, idle, not in hitstun | **Blocked**: chip damage, blockstun, block pushback |
| Defender hit while not attacking | **Normal hit**: damage, hitstun, hit pushback |
| Defender hit during its startup or active ticks | **Counter hit**: damage × multiplier, extra hitstun |
| Defender hit during its recovery | **Punish counter**: bigger multiplier and extra stun |

- **Crouching:** hold down to crouch. A crouching fighter can't walk, uses the shorter crouch hurtbox, and its attack
  button uses the character's **crouching attacks** list. Posture can change while idle or in blockstun, not mid-attack or in hitstun.
- **Attack heights:** **Mid** attacks are blocked standing or crouching. **Low** attacks are only blocked crouching.
- **Trades:** both fighters' hits are collected before either is applied, so two attacks landing on the same tick both hit.
- **Corner pushback:** if the defender is already touching the wall behind it, the attacker is pushed back instead.
- **Rounds:** each round lasts **100 seconds**. A KO wins the round. At time out, the fighter with more health
  (as a fraction of max) wins it. A double KO or equal health is a draw, and a draw gives **both** fighters the round.
  The first to **2 rounds** wins the match, then a new match starts. Fighters can't act during "ROUND n / FIGHT!" or after the round ends.
- **Input buffer:** an Attack press is remembered for 6 ticks, so pressing slightly early (for example during blockstun) still works. One press gives one attack.

### Frame advantage log
With **Log Frame Advantage** on (RoundManager), every hit or block prints who can act first, for example:

```
P1 Myrmillo Stab Blocked: P1 Myrmillo -1 (defender busy 8, attacker busy 9)
```

`+` means the attacker is free first. `-` means the defender is free first and can punish.

---

## Opponents

Player 2 is either the **Dummy** or the **CPU**. Choose on the select screen, or with **Default P2 Type** on `MatchSetup`.

### Dummy (`TrainingDummy`)
A practice opponent with a fixed behaviour. Switch its state during the fight with the number keys.
The current state is shown in the top-left corner.

| Key | State | Behaviour | Good for |
|---|---|---|---|
| **1** | **Idle** | Stands still | Testing damage and combos |
| **2** | **Attack** | Stands still and attacks again as soon as it can | Testing counter hits, trades and punishes |
| **3** | **Block** | **Guards in place** (no walking back), crouching against lows, so every attack is blocked | Testing blockstun, chip damage and frame advantage on block |

### CPU (`ScriptedCPU`)
The rule-based AI and the baseline for the RL agent. Every 8 ticks it decides: block if a nearby attack is in startup
or active (50%, crouching against lows), otherwise attack if one reaches (60%), step back or wait, otherwise walk in.
It blocks the way a player does (holding back). Its random choices come from a **seed**, so an evaluation run can be repeated exactly.

---

## Project structure

```
Assets/
  Scenes/          Menu, CharacterSelect, FightingScene
  Scripts/         All game code (see Assets/Scripts/README.md)
  Data/
    Roster.asset             List of selectable characters
    Character/Myrmillo/      Myrmillo.asset (CharacterData), Myrmillo_Gold (ColorPalette)
  Prefabs/         Myrmillo (fighter), CharacterButton, ModeButton
  Animations/      Animator controllers and clips
  Sprites/         Characters (Aseprite files), stage and UI art
  Input Systems/   PlayerInput.inputactions (Walk, Attack)
```

## Architecture

```
CharacterRoster ──► CharacterData (one asset per character: stats, hurtbox, attacks, prefab, portrait, alt palette)

FightingScene
  MatchSetup ── spawns both prefabs, calls Fighter.Setup(data), links opponents, walls,
                controllers, health bars, and hands the fighters to RoundManager
  RoundManager ── steps every fighter each tick, applies hits, runs the round timer, KO / time out, first to 2
  RoundHUD     ── timer, rounds won, ROUND / FIGHT / K.O. / WINS text

Fighter (inherits BaseFighter)
  BaseFighter : shared rules. Receiving hits, block / counter / punish, stun, pushback, walls, facing
  Fighter     : actions. Reads commands, walks, runs attacks, finds hits, uses CharacterData

Controllers (IFighterController → FighterCommand { moveX, attack, crouch, guard })
  PlayerController : keyboard input + 6-tick buffer
  TrainingDummy    : fixed Idle / Attack / Block behaviour for testing
  ScriptedCPU      : rule-based CPU
  (planned) FighterAgent : ML-Agents policy
```

### One tick
`RoundManager.StepFighters` runs each phase for **all** fighters before starting the next phase:

```
1. ReadCommand   every controller decides (moveX, attack)
2. Advance       attacks end / start / step forward (the rules decide what's allowed)
3. CollectHits   active hitboxes touching a hurtbox are recorded, not applied
4. apply hits    ReceiveHit for every recorded hit (damage, stun, block, counter, push)
5. Move          stun countdown, facing, walking, pushback, wall clamp
```

This keeps the simulation **fair** (trades work, update order doesn't matter) and **deterministic**,
which repeatable RL training and evaluation rely on.

Controllers can only **ask**, through `FighterCommand`. The fighter enforces the rules, so the player,
the scripted CPU and the RL agent all play under exactly the same constraints.

---

## Adding a character

1. Make a prefab with: SpriteRenderer, Animator, Rigidbody2D, a BoxCollider2D (body width for walls),
   **Fighter**, **FighterAnimator**, **Health** (and **SpriteRecolor** if it needs palette swaps).
2. Right-click in `Assets/Data/Character` → **Create → Fighting Game → Character**. Fill in the name, portrait,
   prefab, stats, hurtbox and the **Attacks** list (frame data, hitbox, damage, stun, pushback, animation state name).
3. Add the new asset to `Assets/Data/Roster.asset`. A button appears on the select screen automatically.
4. Optional: **Create → Fighting Game → Color Palette** for mirror matches, and set it as the character's **Alt Palette**.

A character with a unique mechanic (projectile, dash…) can subclass `Fighter` and override
`OnAttackStarted`, `OnAttackTick` or `CollectHits`.

## Debugging tools

| Tool | How |
|---|---|
| Hitbox gizmos | Turn on **Gizmos** in the Game view. Green = hurtbox, light green = extended hurtbox, yellow = hitbox, red = hitbox that can hit now |
| Frame advantage log | RoundManager → **Log Frame Advantage** |
| Hit a fighter by hand | Fighter component ⋮ menu → **Debug: Receive Hit** (fill in **Debug Hit** first; hold back to test blocking) |
| Reset the round | **R** |
| Lag handling | RoundManager → **Max Ticks Per Frame** (default 3): after a hitch the game slows down instead of jumping ahead |

The game is capped at 60 fps (`FrameRateLock`, no setup needed).

---

## Roadmap

1. **Moves:** the low attack's data and animation, then a target combo (Stab / Low → follow-up) that causes a knockdown
2. **ML-Agents:** install the package, write `FighterAgent` (observations, actions, rewards), build a
   training scene without menus, support several arenas at once, switch off logging for training
3. **Experiments:** train a 1-move agent and a 3-move agent against the scripted CPU, then compare win rates over fixed seeds

## Known limitations

- RoundManager keeps a single static "active" manager, so only **one arena per scene** is supported for now.
- Several scripts print to the Console on every hit. That's useful while testing, but it needs turning off before training.
- Sprite atlases with **Tight** packing can't be recoloured (SpriteRecolor warns and leaves those frames unchanged).
