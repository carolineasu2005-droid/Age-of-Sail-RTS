# Player Follow Trail v1 — manual gate

Status: **Player Follow Trail = Experimental / Playtest-bound**.
**Formation Slot / InSuccession = Active**. Any deprecation needs a separate
architecture decision after developer A/B playtest.

Unity Test Runner: **NOT RUN — developer will execute manually in Unity Editor.**
All four scenarios below are pending developer execution. Compilation and source
inspection do not constitute an automated-test pass.

## Setup

Use the already-open Unity Editor with
`Assets/Scenes/Prototype/SailingPrototype_01_Speed.unity`. Enter Play Mode and
enable `FollowPlaytest_EnableForManualGate`. Its TeamId 1 ships are `Follow_A`,
`Follow_B`, `Follow_C` and optional `Follow_D`, without Combat AI controllers.
They begin near X = -350 m, with Z = 200, 80, -40 and -160 m.

Pan/zoom, left-click to select, Shift-click for a group and Home to focus the
selection. Use ordinary unarmed right-clicks for these scenarios. The Movement
Status area scrolls and reports Follow target, state, entry, progress/head, gap
and cap. Speeds are m/s; distances are meters. Gap is along the recorded trail
after entry, rather than physical separation during entry.

Start Follow **before** the leader travels the route; a later subscription starts
at its current head and does not acquire earlier history. Restart Play Mode when
a fresh scenario is useful. Existing Speed +/- and Stop controls remain available.
Hold a CW/CCW button for roughly 0.6 s and release for roughly 60 degrees; inspect
the existing preview first. No dedicated Tack/Wear control is required.

For isolated movement, existing AI controllers may be temporarily disabled
during Play Mode. Retain a Hostile Team 2 ship E for Gate 1. Do not save temporary
Play Mode changes to scenes or prefabs.

## Gate 1 — Input + Combat Coexistence

- Select B and right-click Friendly A: B starts Follow A.
- While following, right-click Hostile E: Manual Target becomes E.
- B keeps following A and can fire through existing controls when normal range,
  broadside, reload and obstruction eligibility allow. Manual Target retains its
  existing Auto Fire OFF rule; assigning E must not steal Movement ownership.
- Right-click sea: Follow ends and normal Destination takes over.

**PASS:** Friendly/Hostile/world routing is obvious, and Manual Target does not
interrupt Follow movement.

## Gate 2 — Core Trail Replay

- Establish B -> A, then move A a meaningful straight distance.
- A performs one roughly 60-degree CW **or** CCW turn and continues forward.
- B reaches approximately the same **world-space turn region** before turning,
  without an obvious corner-cut directly toward A.
- Observe spacing stabilize without collision. B uses its own planner; current
  wind/yaw may independently produce NormalTurn, Tack or Wear.
- Stop A: B settles behind it with Follow retained. Resume A: B resumes
  automatically without another Follow click.

**PASS:** Following the leader's actual trail is visually clear, including
stop/resume. Only one turn direction is mandatory manually; the opposite
direction is covered by automated tests awaiting developer execution.

## Gate 3 — Chain Follow

- Establish B -> A and C -> B. D -> C is optional.
- Command A through a straight segment and one turn.
- B follows A's actual trail; C follows B's actual trail. Observe no obvious
  chain collapse or collision.
- With the chain established, select A and attempt to Follow C: reject the
  cycle-producing command without issuing a world Destination.

**PASS:** A practical three-ship line-ahead chain works and rejects a cycle.
Four ships are not mandatory for this manual gate.

## Gate 4 — Formation A/B + Replacement

- Briefly observe existing Slot Formation / InSuccession. Group-select, use L
  for the Line Ahead template as appropriate, sea-click/right-drag placement,
  and X for the requested InSuccession style. F toggles the hovered lead.
- Switch an appropriate ship or group into Follow mode through individual
  Friendly right-clicks. Compare operation and line-ahead readability.
- Issue one explicit replacement: valid Formation, Stop, or manual CW/CCW.
  Follow relinquishes its Movement commands and speed authority cleanly.

**PASS:** Formation remains intact, Follow is independently usable, and ownership
replacement is understandable. Record brief Mode A / Mode B observations;
Formation remains Active regardless of first impressions.

These are the only four mandatory manual scenarios. Detailed speed boundaries,
rejection reasons, target-loss cases, AI exclusion, memory lifecycle and the
opposite turn direction remain automated-test responsibilities unless a
regression calls for a focused manual investigation.
