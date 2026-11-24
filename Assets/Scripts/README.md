# Scripts reference

What every script in this folder does, grouped by role. The project overview is in the [root README](../../README.md).

```
Data (assets)         CharacterData, AttackData, CharacterRoster, ColorPalette
Fighter               BaseFighter → Fighter, FighterAnimator, Health, SpriteRecolor
Control               FighterCommand / IFighterController, PlayerController, TrainingDummy, ScriptedCPU
Match                 MatchSetup, RoundManager, MatchSettings, FrameRateLock
UI / menus            HealthBar, RoundHUD, MatchEndMenu, MainMenu, CharacterSelectMenu
```

---

## Data

### `CharacterData.cs` (ScriptableObject)
One asset per character, made with **Create → Fighting Game → Character**. It holds everything that makes a character different:

| Field | Meaning |
|---|---|
| `displayName`, `portrait` | Shown on the select screen and the health bar |
| `prefab` | What gets spawned. Must have a `Fighter` on its root |
| `maxHealth`, `forwardSpeed`, `backwardSpeed` | Stats |
| `hurtboxOffset`, `hurtboxSize` | Body hurtbox, in sprite units, measured from the feet, facing right |
| `crouchHurtboxOffset`, `crouchHurtboxSize` | Hurtbox while crouching |
| `attacks` | `List<AttackData>` used while standing. Index 0, 1, 2… is what controllers ask for |
| `crouchAttacks` | `List<AttackData>` used while crouching (the same index, e.g. 0 = the low attack) |
| `followUp`, `followUpWindowTicks` | The follow-up attack (target combo) and how many ticks after a hit it can still be started (8). Active Ticks 0 = no follow-up |
| `knockdownFallTicks`, `knockdownDownTicks`, `knockdownGetUpTicks` | How long this character takes to fall, lie down (default, an attack can override it) and get up when knocked down (20 / 20 / 18) |
| `altPalette` | Colours for player 2 in a mirror match |

The code defaults only apply when a new asset is created. The values saved in the asset are what the game uses.

### `AttackData.cs` (`[System.Serializable]` class)
One attack, edited inside the CharacterData's attack list.

| Group | Fields |
|---|---|
| Identity | `name`, `animationState` (Animator state played when it starts) |
| Height | `height`: `AttackHeight.Mid` (blocked standing or crouching) or `AttackHeight.Low` (blocked crouching only) |
| Frame data (ticks) | `startupTicks`, `activeTicks`, `recoveryTicks` |
| Hitbox | `hitboxOffset` (centre), `hitboxSize` |
| Extended hurtbox | `extraHurtboxOffset`, `extraHurtboxSize` (size 0 = none), `extraHurtboxStartTick`, `extraHurtboxEndTick` (0 = until the attack ends) |
| Movement | `forwardMove` (world units the attacker slides toward the opponent; it pushes the opponent's body), `moveStartTick`, `moveEndTick` (0 = until the active ticks end) |
| On the receiver | `damage`, `chipDamage`, `hitStunTicks`, `blockStunTicks` |
| Pushback | `hitPushback`, `blockPushback` (total distance, spread over the stun) |
| Counter / punish | `counterDamageMultiplier`, `counterHitBonusStun`, `punishDamageMultiplier`, `punishCounterBonusStun` |
| Target combo / knockdown | `canFollowUp` (connecting, hit or blocked, opens the follow-up window), `knockdown` (a clean hit knocks the receiver down), `knockdownDownTicks` (how long this attack keeps the receiver lying down; 0 = the receiver's default) |

`isValid` is true when `activeTicks > 0`. `hasExtraHurtbox` is true when the extended hurtbox has a size.

### `CharacterRoster.cs` (ScriptableObject)
`List<CharacterData> characters`: the selectable characters, in button order.

### `ColorPalette.cs` (ScriptableObject)
Colour-swap rules for `SpriteRecolor`, made with **Create → Fighting Game → Color Palette**. A new palette starts with the Myrmillo gold rules (`Reset()`).

- **Materials**: colours that count as one kind of stuff. Touching pixels of the same material form a body part.
- **Swaps**: `from` → `to` with a `where` (Everywhere, TopPart, MiddlePart, BottomPart, NextToColor + `with`). They're checked in order, and the first match wins.
- `tolerance` (± per channel, 0–255), and `topLimit` / `bottomLimit` / `bottomReach` (part position, as a fraction of the sprite height from the top).
- `Close(a, b)`: colour comparison within tolerance.

---

## Fighter

### `BaseFighter.cs` (abstract): shared rules
What happens **to** a fighter. Every fighter type inherits these rules.

| Member | Purpose |
|---|---|
| `SetOpponent`, `SetStage` | Set by MatchSetup |
| `Hurtbox`, `IsHitBy(Rect)` | The body box in world space. A hit = rectangle overlap |
| `LocalBoxToWorld(offset, size)` | Converts a sprite-space box to world space (follows position, scale and facing) |
| `IsHitstun`, `IsBlockstun`, `IsStunned`, `StunTicksLeft` | Stun state (`StunTicksLeft` includes knockdown) |
| `IsKnockedDown`, `KnockdownTicksLeft`, `KnockdownTotalTicks`, `CurrentKnockdownPhase` (None / Fall / Down / GetUp) | Knockdown: started by a clean hit from an attack with `knockdown`. Replaces the hitstun. The fighter can't act and can't be hit (`IsHitBy` is false, `ReceiveHit` returns None) until it has got up |
| `CanAct` | Not stunned and not knocked down |
| `OnHitLanded(type, atk)` | Called on the attacker by `ReceiveHit` after its attack reached the opponent (Fighter opens the follow-up window here) |
| `ReadCommand`, `Advance`, `CollectHits`, `Move` | Tick phases, empty here and filled in by `Fighter` |
| `PlannedX`, `LastX`, `ShiftPlanned`, `CommitMove`, `BodyHalfWidth` | Body collision: `Move` only plans the new x, RoundManager pushes overlapping bodies apart with `ShiftPlanned` (never through a wall), `CommitMove` applies it |
| `ReceiveHit(atk, attackerX, attacker)` | Block / normal / counter / punish counter, damage, stun, pushback. Fires `HitReceived` |
| `TickStun()` | Counts stun and knockdown down. The hit tick itself doesn't count |
| `ApplyPushback`, `IsAgainstWall`, `ConsumePush`, `ClampToStage` | Pushback (sent to the attacker if the defender is cornered) and wall limits |
| `SetFacing`, `UpdateFacing`, `IsHoldingBack` | Facing and "holding back = block" |
| `FixedUpdate` | Fallback that steps the fighter alone when no RoundManager drives it |
| Abstract / virtual | `OnResetFighter`, `WantsToBlock`, `AllowBlock`, `OnInterrupted`, `CurrentAttackPhase`, `RecoveryTicksLeft`, `LastCommand` (the command used this tick, read by MatchRecorder) |
| ⋮ menu **Debug: Receive Hit** | Hits this fighter with `debugHit` in Play mode |

### `Fighter.cs`: the standard fighter
What a fighter **does**, using its `CharacterData`.

| Member | Purpose |
|---|---|
| `Setup(data)` | Copies speeds, hurtbox and max health from the data |
| `SetStart(pos, faceRight)` | Spawn position and facing, also used for round resets |
| `SetController`, `FindController` | Who controls it. The first enabled `IFighterController` is used if none is set |
| `GetAttack(i)`, `AttackHitbox(atk)`, `AttackWouldReach(i)` | Attack lookup, hitbox rectangle, "would it hit right now?" |
| `ReadCommand` | Gets this tick's `FighterCommand` |
| `Advance` | Ends a finished attack, starts the follow-up if `cmd.followUp` and it is available (cancelling the attack that hit), otherwise starts a requested attack if idle and free, steps the attack tick |
| `HasFollowUp`, `FollowUpAvailable`, `FollowUpTicksLeft`, `IsFollowUp`, `FollowUpIndex` (-2) | Target combo: for `followUpWindowTicks` after a `canFollowUp` attack connects (hit or blocked, not a whiff). Closed by starting any attack or being hit |
| `CollectHits` | Records a hit (once per swing) when the active hitbox overlaps the opponent |
| `Move` | Stun / knockdown countdown, facing, walking (locked while attacking, stunned or knocked down), the attack's forward slide (`AttackStep`), pushback, wall clamp. Sets `PlannedX`; `CommitMove` then calls `rb.MovePosition` |
| `CurrentAttackPhase`, `HitboxActive`, `RecoveryTicksLeft` | Attack timing information |
| `ExtraHurtboxActive`, `ExtraHurtbox`, `IsHitBy` | Extended sword-arm hurtbox |
| `AttackSerial` | Goes up by one each time an attack starts (used by the animator and the input buffer) |
| `OnAttackStarted`, `OnAttackTick` | Empty hooks for characters with unique mechanics (index `FollowUpIndex` for the follow-up) |
| `OnDrawGizmos` | Green hurtbox, light-green extended hurtbox, yellow / red hitbox |

### `FighterAnimator.cs`
Drives the Animator from the fighter's state (`MoveDir`, `IsHitstun`, `IsBlockstun`, `IsAttacking`).
It plays the attack's `animationState` whenever `AttackSerial` changes, plays `hit` / `block` on `HitReceived`,
and draws the attacker above the opponent (`attackSortingOrder`).
When knocked down it plays `knockdown`, then `down`, then `getup`, each when its phase starts (placeholders until those states exist: `hit`, then `crouch_hit` / `crouching`), and `idle` after.
These states need no transitions: the script plays them. Loop off for `knockdown` and `getup`, on for `down`.
A missing attack state logs one warning instead of failing.

### `Health.cs`
`Current`, `Max`, `IsKO`, `TakeDamage`, `ResetHealth`, `SetMax` (called by `Fighter.Setup`).

### `SpriteRecolor.cs`
Palette swap at runtime. It makes a recoloured copy of every animation frame and shows it instead of the original.

| Member | Purpose |
|---|---|
| `SetPalette(palette)` | MatchSetup gives P2 the alt palette in a mirror match. `null` = off |
| `Prewarm()` | Recolours every frame of every clip up front, so nothing slow happens mid-fight |
| `LateUpdate()` | Swaps the sprite the Animator picked for its copy |
| `MakeSprite(s)` | Cuts the frame out of its texture (handles atlas packing and trimming), recolours it, and makes a sprite with the same pivot |
| `Recolor(px, w, h)` | Labels materials, flood-fills body parts, then applies the first matching rule per pixel |
| `Matches`, `TouchesColor`, `MaterialOf` | Rule checks |
| `ReadPixels(texture)` | Reads pixels directly, or copies them back from the GPU (slow) if Read/Write is off |
| `ClearCache()` | Destroys the copies (never the originals) |

Tight-packed atlas sprites can't be recoloured. It logs a warning and leaves them as they are.

---

## Control

### `FighterCommand.cs`
```csharp
public struct FighterCommand { public float moveX; public int attack; public bool crouch; public bool guard; public bool followUp; }   // attack -1 = none
public interface IFighterController { FighterCommand GetCommand(BaseFighter self); }
```
Every controller implements `GetCommand`, which `Fighter.ReadCommand` calls once per tick.
Controllers only ask. `Fighter` decides what is allowed.
`guard` blocks in place without holding back. Only the training dummy uses it; players block by holding back.
`followUp` starts the follow-up when it is available and is ignored otherwise (so a controller can send it freely; the RL agent masks it instead).

### `PlayerController.cs`
Keyboard / gamepad input through the Input Actions asset (`Walk`, `Attack`). `MatchSetup` adds it and calls `Init(actions)`.

- **Walk** is polled every tick (`ReadValue<Vector2>().x`).
- **Attack** is an event (`performed`) that sets a flag. The flag starts a **buffer** of `bufferTicks` (6).
  The buffer is cleared when the fighter's `AttackSerial` changes (one press = one attack).
- A buffered press sends both `attack = 0` and `followUp`, so **pressing attack again after a hit** gives the follow-up.

### `TrainingDummy.cs`
Practice opponent, set up with `Init(opponent, startState)`. Number keys **1 / 2 / 3** switch the state during the fight,
and `OnGUI` shows the current state in the top-left corner.

| State | Behaviour |
|---|---|
| Idle | nothing |
| Attack | requests a random standing attack every tick (it comes out as soon as the fighter is free), and always the follow-up when a hit or block allows it |
| Block | sends `guard` (blocks in place, no walking back) and crouches when the opponent's current attack is a low |

### `ScriptedCPU.cs`
Rule-based AI and the RL baseline, set up with `Init(opponent)`. `Decide` runs every `reactionTicks`:
block (chance, crouching against lows) if `IsThreatened` → attack (chance) if one reaches → back off / wait → otherwise walk in.

- `PickReachableAttack` looks through the **standing and crouching** attack lists together (each attack once, from a
  random start) using `AttackWouldReach`. A crouching attack is done by sending `crouch = true` and the attack on the
  same tick, so the fighter crouches and takes the crouching list.
- **Going low:** the CPU remembers for `rememberBlockTicks` (60) that the opponent blocked standing. Then, with
  `lowVsStandingBlockChance` (70%), it tries the crouching attacks first, because a low gets past a standing block.
- **Follow-up:** checked every tick. When `FollowUpAvailable`, it decides once (`followUpChance`, 70%) and presses after `followUpDelayTicks` (2).
- A knocked-down opponent can't be hit, so `AttackWouldReach` is false and the CPU just walks in while it gets up.
- `Roll(chance)` makes a weighted random yes/no.
- All randomness comes from `System.Random(seed)`, so runs are repeatable.

---

## Match

### `MatchSetup.cs` (FightingScene)
Builds the match in `Awake`:
1. Picks the mode and characters (`MatchSettings`, or `defaultMode` / `defaultP1` / `defaultP2`).
2. Spawns both prefabs (`Setup`, `SetStage`, `SetStart`).
3. Links the opponents.
4. P1 = `PlayerController`. P2 = `TrainingDummy` (starting in `dummyStartState`) in Training, `ScriptedCPU` in Versus. The unused controllers are disabled.
5. Gives P2 the alt palette in a mirror match.
6. Sets up the health bars, calls `RoundManager.SetTrainingMode` (Training or not), then `RoundManager.SetFighters`.

**Escape** loads `menuScene` (CharacterSelect). Helpers: `GetOrAdd<T>`, `SetEnabled<T>`.

### `RoundManager.cs`
The referee and the clock. `fighters[0]` is P1 and `fighters[1]` is P2.

| Member | Purpose |
|---|---|
| `roundTimeSeconds` (100), `roundsToWin` (2) | The rules: 100 s rounds, first to 2 rounds wins the match |
| `introDelay`, `roundOverDelay`, `matchOverDelay` | Pauses in seconds. During them the fighters' input is locked (`InputLocked`). Set `introDelay` to 0 for training |
| `Phase` | `Intro` (ROUND n / FIGHT!) → `Fighting` → `RoundOver` (K.O. / TIME) → next round, or `MatchOver` (P1 WINS) → Retry / Quit menu |
| `autoRestartMatch` | Off: after `matchOverDelay` the match waits (`WaitingForRetry`) for MatchEndMenu. On: a new match starts by itself (for training) |
| `MatchWinnerIndex` | 0 = P1, 1 = P2, -1 = draw, -2 = match not over |
| `StepFighters()` | One tick, phase by phase for all fighters: ReadCommand → Advance → CollectHits → apply hits → Move → `SeparateBodies` → CommitMove. Hits are only applied while `Fighting` |
| `SeparateBodies(a, b)` | Body collision. Sides come from the positions before the tick (so nobody walks through). The overlap is shared by how far each moved toward the other: the faster one pushes the slower. A fighter stopped by a wall makes the other give back the rest. Body width = the fighter's Collider2D |
| Round end | KO → the other fighter wins; both KO on the same tick → draw. Time out → more health (as a fraction of max) wins, equal → draw. **A draw gives both a round**, so a match always ends |
| `StartMatch()`, `StartRound()` | Reset the score / reset the fighters and the clock (`StartRound` is what the RL training loop will call). `ResetRound()` = `StartRound()` |
| `TimerTicksLeft`, `TimeLeftSeconds`, `Wins(i)`, `RoundNumber`, `CurrentPhase`, `Announcement`, `LastRoundWinner` | Read by `RoundHUD` (and later the agent) |
| `RoundEnded`, `MatchEnded` (events) | Fire with the winner: 0 = P1, 1 = P2, -1 = draw |
| `MatchStarted`, `RoundStarted`, `CommandsRead`, `HitApplied`, `TickFinished`, `ManualRestart` (events) | For watchers such as `MatchRecorder`: they only read the game. `CommandsRead` fires every tick after all commands are read; `HitApplied` gives the hit, its result and the damage |
| `MatchTick` | Ticks since the match started. Match logs stamp inputs and events with it |
| `LastRoundReason`, `IntroTicks`, `RoundOverTicks`, `MatchOverTicks`, `GetFighter(i)`, `FighterCount` | Read by the recorder |
| `Drives(f)` (static) | True if this manager steps fighter `f`, so the fighter skips its own fallback |
| `maxTicksPerFrame` | Caps catch-up after a hitch via `Time.maximumDeltaTime` (slow down instead of jumping) |
| `logFrameAdvantage` | Logs who acts first after each hit or block |
| `trainingMode`, `SetTrainingMode`, `IsTrainingMode`, `refillDelayTicks` | Training: no intro, no timer, no KO or rounds; `RefillHealth` refills a fighter after `refillDelayTicks` (60) free ticks, or right away after a KO |
| **R** / **Shift + R** | Restart the round / restart the match |

The timer counts **ticks** (6000 for 100 s), not real time, so it's exact and the same on every run.

### `MatchRecorder.cs`
Writes a JSON log per match (put it on the RoundManager's GameObject): setup, input changes, events and stats.

| Member | Purpose |
|---|---|
| `record`, `recordTraining`, `saveUnfinished`, `logAttackStarts`, `folderName` | Options. Training mode isn't logged unless `recordTraining`. Turn `record` off for RL training |
| `OnMatchStarted` | New log: rules, players (character, controller, `DataHash`), and saves an unfinished previous match (Shift + R) |
| `OnCommandsRead` | Every tick: each fighter's `LastCommand`, written only when it changes (`Pack` compares them) |
| `OnHitApplied`, `OnTickFinished` | Stats and events: hits, blocks, damage, knockdowns; attacks started (via `AttackSerial`) |
| `OnRoundEnded`, `OnMatchEnded` | Round results (health, positions), then the summary and `Save()` |
| `OnManualRestart` | R / Shift + R become events, so a replay restarts at the same tick |
| `OnDestroy` | Leaving the scene saves an unfinished match (`endedBy` = quit) |
| `DataHash(data)` | Fingerprint of a character's gameplay numbers: shows if a log was recorded with different data |

`moveX` is stored as -1 / 0 / +1. The fighter only uses its sign past ±0.1, so this replays exactly.

### `MatchEndMenu.cs`
Shows `panel` (with `retryButton` and `quitButton`) when `RoundManager.WaitingForRetry`, and selects Retry so the keyboard works.
**Retry** = `RoundManager.StartMatch()` (same fighters, 0 - 0). **Quit** loads `quitScene` (Menu).
Put the script on the Canvas, **not** on the panel: a hidden object stops running its scripts.

### `RoundHUD.cs`
Shows the RoundManager's state. All fields are optional.
`timerText` (turns `timerLowColor` at `lowTimeSeconds` and below), `p1RoundMarkers` / `p2RoundMarkers`
(one Image per round needed to win, gold when won), `announcementText` (the big centre text, hidden when empty). In Training mode the timer and markers are hidden.

### `MatchSettings.cs` (static)
`mode` (`GameMode.Training` / `Versus`), `p1`, `p2`, `HasPicks`, `ClearPicks()`. Carries the menu's mode and the select-screen picks into the
fight scene, because static values survive scene loads.

### `FrameRateLock.cs` (static)
Runs automatically before the first scene loads (`RuntimeInitializeOnLoadMethod`). It turns VSync off,
sets `targetFrameRate = 60`, and keeps `fixedDeltaTime = 1/60`.

---

## UI / menus

### `HealthBar.cs`
Shows a `Health` as a Filled image, with a lighter damage trail that waits `trailDelay` and then slides down
over `slideDuration`. `SetTarget(health, portrait)` is called by MatchSetup. Set **Fill Origin = Right**
to mirror the bar for player 2.

### `MainMenu.cs` (Menu scene)
**Play** opens the mode page (`modePage`, hiding `mainPage`); **Back** or Escape returns. **Versus** / **Training** set `MatchSettings.mode`, clear the picks and load CharacterSelect. **Exit** quits (it stops Play mode in the Editor).

### `CharacterSelectMenu.cs` (CharacterSelect scene)
- Builds one button per roster character (it fills a child Image named **Portrait**).
- Flow: P1 pick → opponent pick (CPU in Versus, dummy in Training) → **Start**, which writes the picks to `MatchSettings` and loads FightingScene. The instruction text shows the mode.
- **Back** undoes the last pick, or returns to the Menu when nothing is picked.
- `Refresh()` updates the text, previews and faded / usable buttons (`SetUsable`, using a CanvasGroup).

---

## Conventions

- **Ticks, not seconds:** all gameplay timing is in ticks of 1/60 s.
- **Boxes** are written in sprite units, facing right, measured from the feet (the pivot). `LocalBoxToWorld` handles position, scale and facing.
- **Controllers never change the game directly.** They only return a `FighterCommand`.
- **Names that code looks up:** the input actions `Walk` / `Attack`, the button child `Portrait`, Animator states
  (`hit`, `block`, each attack's `animationState`), and the scene names in the build list.
