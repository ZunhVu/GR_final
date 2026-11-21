# Scripts reference

What every script in this folder does, grouped by role. The project overview is in the [root README](../../README.md).

```
Data (assets)         CharacterData, AttackData, CharacterRoster, ColorPalette
Fighter               BaseFighter → Fighter, FighterAnimator, Health, SpriteRecolor
Control               FighterCommand / IFighterController, PlayerController, TrainingDummy, ScriptedCPU
Match                 MatchSetup, RoundManager, MatchSettings, FrameRateLock
UI / menus            HealthBar, RoundHUD, MainMenu, CharacterSelectMenu
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
| On the receiver | `damage`, `chipDamage`, `hitStunTicks`, `blockStunTicks` |
| Pushback | `hitPushback`, `blockPushback` (total distance, spread over the stun) |
| Counter / punish | `counterDamageMultiplier`, `counterHitBonusStun`, `punishDamageMultiplier`, `punishCounterBonusStun` |

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
| `IsHitstun`, `IsBlockstun`, `IsStunned`, `StunTicksLeft` | Stun state |
| `ReadCommand`, `Advance`, `CollectHits`, `Move` | Tick phases, empty here and filled in by `Fighter` |
| `ReceiveHit(atk, attackerX, attacker)` | Block / normal / counter / punish counter, damage, stun, pushback. Fires `HitReceived` |
| `TickStun()` | Counts stun down. The hit tick itself doesn't count |
| `ApplyPushback`, `IsAgainstWall`, `ConsumePush`, `ClampToStage` | Pushback (sent to the attacker if the defender is cornered) and wall limits |
| `SetFacing`, `UpdateFacing`, `IsHoldingBack` | Facing and "holding back = block" |
| `FixedUpdate` | Fallback that steps the fighter alone when no RoundManager drives it |
| Abstract / virtual | `OnResetFighter`, `WantsToBlock`, `AllowBlock`, `OnInterrupted`, `CurrentAttackPhase`, `RecoveryTicksLeft` |
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
| `Advance` | Ends a finished attack, starts a requested one if idle and not stunned, steps the attack tick |
| `CollectHits` | Records a hit (once per swing) when the active hitbox overlaps the opponent |
| `Move` | Stun countdown, facing, walking (locked while attacking or stunned), pushback, wall clamp |
| `CurrentAttackPhase`, `HitboxActive`, `RecoveryTicksLeft` | Attack timing information |
| `ExtraHurtboxActive`, `ExtraHurtbox`, `IsHitBy` | Extended sword-arm hurtbox |
| `AttackSerial` | Goes up by one each time an attack starts (used by the animator and the input buffer) |
| `OnAttackStarted`, `OnAttackTick` | Empty hooks for characters with unique mechanics |
| `OnDrawGizmos` | Green hurtbox, light-green extended hurtbox, yellow / red hitbox |

### `FighterAnimator.cs`
Drives the Animator from the fighter's state (`MoveDir`, `IsHitstun`, `IsBlockstun`, `IsAttacking`).
It plays the attack's `animationState` whenever `AttackSerial` changes, plays `hit` / `block` on `HitReceived`,
and draws the attacker above the opponent (`attackSortingOrder`).

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
public struct FighterCommand { public float moveX; public int attack; public bool crouch; public bool guard; }   // attack -1 = none
public interface IFighterController { FighterCommand GetCommand(BaseFighter self); }
```
Every controller implements `GetCommand`, which `Fighter.ReadCommand` calls once per tick.
Controllers only ask. `Fighter` decides what is allowed.
`guard` blocks in place without holding back. Only the training dummy uses it; players block by holding back.

### `PlayerController.cs`
Keyboard / gamepad input through the Input Actions asset (`Walk`, `Attack`). `MatchSetup` adds it and calls `Init(actions)`.

- **Walk** is polled every tick (`ReadValue<Vector2>().x`).
- **Attack** is an event (`performed`) that sets a flag. The flag starts a **buffer** of `bufferTicks` (6).
  The buffer is cleared when the fighter's `AttackSerial` changes (one press = one attack).

### `TrainingDummy.cs`
Practice opponent, set up with `Init(opponent, startState)`. Number keys **1 / 2 / 3** switch the state during the fight,
and `OnGUI` shows the current state in the top-left corner.

| State | Behaviour |
|---|---|
| Idle | nothing |
| Attack | requests a random standing attack every tick (it comes out as soon as the fighter is free) |
| Block | sends `guard` (blocks in place, no walking back) and crouches when the opponent's current attack is a low |

### `ScriptedCPU.cs`
Rule-based AI and the RL baseline, set up with `Init(opponent)`. `Decide` runs every `reactionTicks`:
block (chance, crouching against lows) if `IsThreatened` → attack (chance) if one reaches → back off / wait → otherwise walk in.

- `PickReachableAttack` checks each attack once, starting at a random index, using `AttackWouldReach`.
- `Roll(chance)` makes a weighted random yes/no.
- All randomness comes from `System.Random(seed)`, so runs are repeatable.

---

## Match

### `MatchSetup.cs` (FightingScene)
Builds the match in `Awake`:
1. Picks the characters (`MatchSettings`, or the defaults).
2. Spawns both prefabs (`Setup`, `SetStage`, `SetStart`).
3. Links the opponents.
4. P1 = `PlayerController`. P2 = `TrainingDummy` (starting in `dummyStartState`) or `ScriptedCPU`, depending on the opponent type. The unused controllers are disabled.
5. Gives P2 the alt palette in a mirror match.
6. Sets up the health bars and calls `RoundManager.SetFighters`.

**Escape** loads `menuScene` (CharacterSelect). Helpers: `GetOrAdd<T>`, `SetEnabled<T>`.

### `RoundManager.cs`
The referee and the clock. `fighters[0]` is P1 and `fighters[1]` is P2.

| Member | Purpose |
|---|---|
| `roundTimeSeconds` (100), `roundsToWin` (2) | The rules: 100 s rounds, first to 2 rounds wins the match |
| `introDelay`, `roundOverDelay`, `matchOverDelay` | Pauses in seconds. During them the fighters' input is locked (`InputLocked`). Set `introDelay` to 0 for training |
| `Phase` | `Intro` (ROUND n / FIGHT!) → `Fighting` → `RoundOver` (K.O. / TIME) → next round, or `MatchOver` (P1 WINS) → new match |
| `StepFighters()` | One tick, phase by phase for all fighters: ReadCommand → Advance → CollectHits → apply hits → Move. Hits are only applied while `Fighting` |
| Round end | KO → the other fighter wins; both KO on the same tick → draw. Time out → more health (as a fraction of max) wins, equal → draw. **A draw gives both a round**, so a match always ends |
| `StartMatch()`, `StartRound()` | Reset the score / reset the fighters and the clock (`StartRound` is what the RL training loop will call). `ResetRound()` = `StartRound()` |
| `TimerTicksLeft`, `TimeLeftSeconds`, `Wins(i)`, `RoundNumber`, `CurrentPhase`, `Announcement`, `LastRoundWinner` | Read by `RoundHUD` (and later the agent) |
| `RoundEnded`, `MatchEnded` (events) | Fire with the winner: 0 = P1, 1 = P2, -1 = draw |
| `Drives(f)` (static) | True if this manager steps fighter `f`, so the fighter skips its own fallback |
| `maxTicksPerFrame` | Caps catch-up after a hitch via `Time.maximumDeltaTime` (slow down instead of jumping) |
| `logFrameAdvantage` | Logs who acts first after each hit or block |
| **R** / **Shift + R** | Restart the round / restart the match |

The timer counts **ticks** (6000 for 100 s), not real time, so it's exact and the same on every run.

### `RoundHUD.cs`
Shows the RoundManager's state. All fields are optional.
`timerText` (turns `timerLowColor` at `lowTimeSeconds` and below), `p1RoundMarkers` / `p2RoundMarkers`
(one Image per round needed to win, gold when won), `announcementText` (the big centre text, hidden when empty).

### `MatchSettings.cs` (static)
`p1`, `p2`, `p2Type` (`OpponentType.Dummy` / `CPU`), `HasPicks`, `Clear()`. Carries the select-screen picks into the
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
**Play** clears `MatchSettings` and loads CharacterSelect. **Exit** quits (it stops Play mode in the Editor).

### `CharacterSelectMenu.cs` (CharacterSelect scene)
- Builds one button per roster character (it fills a child Image named **Portrait**) and one button per CPU mode.
- Flow: P1 pick → CPU pick → mode → **Start**, which writes `MatchSettings` and loads FightingScene.
- **Back** undoes the last pick, or returns to the Menu when nothing is picked.
- `Refresh()` updates the text, previews, faded / usable buttons (`SetUsable`, using a CanvasGroup) and the gold highlight on the chosen mode.

---

## Conventions

- **Ticks, not seconds:** all gameplay timing is in ticks of 1/60 s.
- **Boxes** are written in sprite units, facing right, measured from the feet (the pivot). `LocalBoxToWorld` handles position, scale and facing.
- **Controllers never change the game directly.** They only return a `FighterCommand`.
- **Names that code looks up:** the input actions `Walk` / `Attack`, the button child `Portrait`, Animator states
  (`hit`, `block`, each attack's `animationState`), and the scene names in the build list.
