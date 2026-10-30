# TowerLand

Unity mobile game (portrait, Android + iOS) by A Small Game. A visual reboot of **Tower Buster**
(`D:\ASG\TowerBuster\towerbuster-new`, reference only — do not copy its architecture): the player throws
glossy beveled **cubes** at an auto-spinning cylindrical tower of cubes; hitting a cube of the same colour
destroys that whole colour group, misses bounce off, unsupported cubes fall. Clear the tower before
running out of moves. Business model: cheap installs, ad-monetised, fast payback — game feel and early
retention matter most.

## Tech
- Unity **6000.3.17f1**, URP 17.3 (`Assets/Settings/Mobile_RPAsset` for mobile), Linear colour space.
- New **Input System** only (no legacy `UnityEngine.Input`).
- DOTween (Pro) in `Assets/Plugins/Demigiant`, modules compiled as `DOTween.Modules` asmdef.
- Bundle id `com.asg.towerland`. Player settings are applied by `Tools/TowerLand/Apply Project Settings`
  (`TowerLand.Editor.ProjectSetup`) — change them there, not by hand.

## Layout
- `Assets/TowerLand/Scripts/Runtime` — asmdef `TowerLand`, namespace `TowerLand.<Folder>`
  (`Core`, `Tower`, `Gameplay`, `Input`, `Levels`, `Feel`, `UI`, `Meta`).
- `Assets/TowerLand/Scripts/Editor` — asmdef `TowerLand.Editor`.
- `Assets/TowerLand/Tests/EditMode` — asmdef `TowerLand.Tests.EditMode` (NUnit).
- `Assets/TowerLand/{Art,Prefabs,Scenes,Levels,Data}` — assets by type.
- Third-party packs go in `Assets/ThirdParty/` (Plugins/ only for SDKs that require it).

## Conventions
- Private fields `_camelCase`, `[SerializeField] private` over public fields, PascalCase members.
- Game rules (tower grid, matching, falling, move budget) are **plain C# classes** with no MonoBehaviour
  dependency, so they can be unit-tested in EditMode. MonoBehaviours are thin views/controllers on top.
- Tower = concentric layers (outermost first), each an explicit grid indexed by (column, floor); inner layers lose
  ~2π columns each (`TowerLayout`) so cubes keep the same size. Neighbours and connectivity come from the grid,
  never from physics queries.
- No global singletons for game state; pass references via serialized fields or a small composition root.
  C# events over static UnityEvents; always unsubscribe in `OnDisable`/`OnDestroy`.
- Tuning numbers live in ScriptableObjects, not literals.
- Move/rename assets inside Unity or together with their `.meta` (GUID references).

## Commands
- Compile/validate headless (close the editor first):
  `"D:/Programs/Unity Hub/Editor/6000.3.17f1/Editor/Unity.exe" -batchmode -quit -projectPath D:/ASG/TowerLand -logFile -`
- EditMode tests: add `-runTests -testPlatform EditMode -testResults <file>` (without `-quit`).

## Unity CLI (preferred while the editor is open)
The `com.unity.pipeline` package lets the `unity` CLI drive the running editor (prefix `unity --no-banner --no-pager`):
- Compile: `--json recompile --project-path D:/ASG/TowerLand`
- Tests: `cmd test run --mode EditMode --result-only`
- Menus / code: `cmd editor invoke_menu --path "Tools/TowerLand/Build Core Assets"`, `cmd script eval --code '...'`
- Play mode: `cmd editor.playmode start|stop`; console: `cmd console get --level error`
- Previews: `cmd script eval --code 'TowerLand.Editor.CoreAssetsBuilder.CapturePreviews(@"<dir>", "1,2,3"); return 0;'`
After a recompile, wait for the domain reload before invoking menus.

## Art pipeline (environments)
- Source: `ArtSource/Blender/World<N>_Environment.blend` (outside `Assets/`), built procedurally via the Blender MCP.
  Scene `TowerLand_World<N>`; collections Ground / Props / Background / Sky are exported, PropLibrary and Reference are not.
  Reference holds `GameCam` = the Unity game camera (pos (0.4,9,-35.5) looking at (0.4,4.7,0) in Unity → Blender (0.4,-35.5,9), vFOV 45, 9:16 like the mockup).
- Low-poly look = flat-shaded meshes + **one palette texture** (`Art/Textures/World<N>_Palette.png`, 8×8 swatches,
  point-filtered, no mips); each face's UVs sit on a swatch centre. One material (`World<N>_Palette`, URP Simple Lit).
- Export: FBX to `Art/Models/World<N>/` (`*_Environment.fbx` = laid-out scene, `*_Props.fbx` = prop library at origin).
- Unity: `Tools/TowerLand/Setup World1 Environment` (`EnvironmentBuilder`) configures imports, remaps the material,
  rotates the root 180° (Blender baked-space FBX lands flipped), and sets sky gradient / tri-light ambient / fog.
  `Build Core Assets` calls it automatically.
- `TowerPlinth` is scaled radially by `EnvironmentBuilder` to sit `PlinthInset` (Data/TowerVisualSettings, 0.35 m) inside
  the tower's outer cube faces for the scene's level, so the bottom row overhangs and shadows the base.
- The Blender layout is centred on the tower: `EnvironmentBuilder` places the Environment root under the Tower object, which
  sits `TowerDepth` (3 m, `CoreAssetsBuilder`) behind the origin. Camera, shooter and Backdrop stay put, so in Blender
  space the game camera is effectively 3 m further back than `GameCam` (y -38.5).
- Keep the shooter lane (bottom centre, |x| < 2.2 m, z < -5 m in Unity) free of props.

## Matching the art mockup
- Reference: `Assets/TowerLand/Art/Reference/Mockup_Gameplay.png` (941×1672, 9:16). The Game scene has an EditorOnly
  `ReferenceOverlay` (starts hidden; Tab: corner thumbnail → onion skin → hidden). Game view size: `Tools/TowerLand/Game View/1080x1920`.
- Compare loop: `Tools/VisualCompare/shot.sh <out_dir> <name>` renders the main camera at mockup resolution and writes
  side-by-side/blend and region-crop comparisons (needs Python + Pillow).
- Cube look lives in `Art/Materials/Cube.mat` (`TowerLand/Candy Cube` shader) + mesh constants in `CoreAssetsBuilder`
  (bevel 0.15, 4 segments, pillow 0.12); cube scale 0.93 / spacing 1.02 in `Data/TowerVisualSettings` (opens the wedge gaps so each cube reads distinctly). Scene light/sky/fog/post in `EnvironmentBuilder`.
- Ground uses `TowerLand/Meadow Ground` (`Art/Materials/World1_Ground.mat`): palette colour × tiling detail texture
  `Art/Textures/Ground_Detail.png`, generated by `Tools/TextureGen/ground_detail.py` (R fine, G patches, B medium).
  Clouds use `TowerLand/Soft Cloud`. Distance fog (45 → 420 m) provides the mockup's atmospheric haze.
- Live tuning: `TowerView` (ExecuteAlways, editor-only sync) pushes Inspector edits of `Cube.mat` and `CubePalette` to
  all runtime cube material variants every frame they change - works in Play mode. Asset edits made in Play mode persist.
- Backdrop: mountains + clouds are pre-rendered through the game camera into `Art/Textures/World1_Backdrop_*.png` and
  shown on far camera-facing planes (`Backdrop` object, `TowerLand/Backdrop` shader); the 3D originals stay in the
  Environment but are deactivated. `BackdropBaker` (menu `Tools/TowerLand/Bake Backdrop`, also run by Build Core
  Assets) - re-bake after changing the camera, lighting/fog or the Blender mountains/clouds.
- Environment props use `TowerLand/Soft Environment` (`World1_Palette.mat`): wrapped diffuse, sky/ground bounce
  ambient, tinted lifted shadows, and per-vertex AO baked in Blender (colour attribute `AO`, raycast vs mesh + ground
  plane; helpers `bake_ao`, `soft_facets`, `chamfer_mesh` in the Blender session). Normals: foliage = half-smoothed
  "soft facets", ground/grass smooth, rocks chamfered with hardened normals. Every palette mesh must carry `AO`
  (white = unoccluded).
- Art direction: every environment prop gets **bevelled (chamfered) edges with hardened facet normals** - flat facets,
  soft rounded edges catching light. Vertex colour convention (Soft Environment shader): RGB = colour tint (blends
  across bevel strips; facets carry one flat tone, strip vertices are relaxed between neighbouring facets), A = baked
  AO. Base UV swatch is white (`petal`) when the colour lives in the tint. Never colour bevel strips with palette
  swatches (they can't blend -> bright/dark bands). Pines: `pine_bevel` in the Blender session.
- Far background = painted image `Art/Textures/World1_Background.png` (9:16, sky + clouds + mountains) on one plane
  (`Backdrop/Background`, 700 m, native scale, `BackgroundOffset` 0.17 screen heights up) created by `BackdropBaker`;
  the 3D Mountains_* / Clouds props are deactivated. Its material must use the shader's default queue (after the
  skybox) - a Background-queue plane with ZWrite off gets painted over by the sky.

## Gameplay (shooting)
Rules and feel follow the Tower Buster web playable (`D:\ASG\TowerBuster\Playables\TB_Playable3D\src`), which has
better physics/input than the base game - check it before changing shooting behaviour.
- Rules are plain C# in `Tower/`: `TowerRaycast` (segment or swept sphere vs the cylinder-of-cubes grid, no
  colliders; each layer is a band between its cube-front and cube-back cylinders: hits from outside on fronts, and -
  through a hole into the hollow core - from inside on backs (`TowerHit.Inside`), first crossing along the segment wins;
  plus a volume march (0.2 m samples inside the tower) for paths that stay inside a band or enter a cube through its
  top / side, which no cylinder crossing catches; `TowerView` passes `CubeScale` as the depth, so aim, projectile and
  colour picking all see the far side). Aiming at the far side through a hole, the throw (`AimPath` via) curves onto the
  aim ray `HoleEntryMargin` in front of the tower (`TowerView.EntryPoint`), then flies straight along it, `TowerRules` (BFS colour group, `Rescue` second chance, `FindUnsupported`, exposed
  cubes; `TowerLayout.IsCovered` = an outer layer fills every column overlapping the cube's angular span by >= 20%
  (`MinOverlapShare`), so a partly revealed cube is lit and playable). `TowerView` applies results (`RemoveCubes` circular ripple, one-knock `Shockwave`, aim `SetHighlight`
  pulse, `DropUnsupported` -> `Feel/FallingCube` that shatters on the scenery, max 24 per frame).
- Collapse rule (`TowerRules.FindUnsupported`, Tower Land - not Tower Buster's base-up support): a central pole runs
  up the tower's axis (`TowerView` builds it: `PoleRadius` in Data/TowerVisualSettings, `Art/Materials/Pole.mat`;
  twisted 6-lobe segments stacked to the tower height + golden collar/ball cap `PoleCap.mat`, modelled in
  `ArtSource/Blender/Pole.blend` (radius-1 units, analytic normals so segments tile seamlessly; export with
  bake_space_transform) -> `Art/Models/Pole/*.fbx`; `Tools/TowerLand/Apply Pole` re-imports and wires them).
  Anchors = every innermost-layer cube (fixed to the pole, any height) + every bottom-floor cube (on the base). A cube
  stays while any chain of touching cubes links it to an anchor (4 neighbours in its layer + overlapping cells of the
  adjacent layers on the same floor, `TowerLayout.TouchingColumns`); no sideways limit. A single-layer tower never
  collapses (all of it is on the pole).
- Exposure shading: each tower cube's mesh variant (32, open-sides mask in UV1) says which sides have no neighbour
  (left/right/top/bottom in its layer, back = a gap in the next inner layer behind any part of it / innermost layer); the
  `Candy Cube` shader drops the seam crevice on open sides, and on an open back (seen from inside through a hole) also
  the covered darkening and the outside form shadow. Probe: `TowerLand.Editor.PlaytestHole.Punch(@"<dir>", halfAngle,
  floorFrom, floorTo, new float[] { 0.6f }, layers)` punches a window through the outermost `layers` (default all) facing
  the camera and screenshots.
- `Gameplay/Shooter`: input via `Pointer.current`; aim = finger + 30% screen height (`FingerOffset`), upward drag x2.
  Only an exact colour match counts (no near-miss); the patch it would break pulses. Projectile colour = weighted
  pick from a 15x14 aim-ray grid over the aimable screen (colours with < 2 rays ignored), fixed once dealt.
  While aiming, the waiting cube turns part of the way towards the aim point (`AimTurn` share, capped `AimTurnMax`,
  damped by `AimTurnSpeed`) and settles back when released.
  The waiting cube sits right of centre (`LaunchViewport` x 0.6, under the thumb). Colour swap (playable's next ball): a
  smaller queued cube (`NextScale`, `NextOffset` bottom-left) of another reachable colour; tapping it (`NextTapRadius`,
  min `NextTapMinPixels` at 1920 px height) swaps the two with a hop (main arcs over, queued dips under; `HopDuration`);
  after a throw the queued one hops into the main slot and a new one is queued. A spinning ring of two curved arrows
  (runtime-drawn sprite, `SwapRingSize`/`Spin`/`Alpha`) sits behind the queued cube as the swap hint. Colours are
  picked from reachable ones (queued != main) and then never change - except a colour wiped off the tower entirely
  (re-dealt; checked every 1 s); no queued cube when only one colour is left. Waiting cubes cast no shadow. Probe:
  `TowerLand.Editor.PlaytestSwap.Tap(@"<dir>")`.
- `Projectile`: scripted arc (`ThrowDuration`), shrinking to tower-cube size (ease-out `FlightShrinkPower`), collider =
  80% of its size, swept in the tower's rotating frame (2 sub-steps/frame); keeps flying under gravity after the arc.
  Wrong colour -> `Rescue` (matching exposed cube within `SecondChanceRadius` of a point `SecondChanceAhead` ahead,
  never a deeper layer), else shockwave + ricochet (out/up/toward camera) and it ignores the tower. Static scenery
  (Ground, TowerPlinth, Rock_*) is sphere-cast; landing there is a miss (restitution bounce). Tuning:
  `Data/ShooterSettings.asset`.
- Broken cubes split into 4 half-size `Feel/CubeChunk` rigidbodies that land on MeshColliders added to Ground,
  TowerPlinth and Rock_* by `EnvironmentBuilder` (physics gravity is doubled: -19.62).
- Debris no-go zone (`Feel/DebrisSweeper` on the Tower + plain-C# `Feel/DebrisZone`, tested): every piece is kept and
  breaks/falls naturally. A chunk that lands (floor or other debris) in the zone - sector facing the camera, reach 5 m
  past the cubes, +-35 deg - bounces out of it: each contact sets a hop round the tower towards its back
  (`EscapeDirection`), until it is `EscapeMargin` deg past the edge (or gives up after `EscapeMaxTime`); a random
  `IgnoreZoneChance` share of pieces (10%) ignores the zone so a few still land there naturally. While escaping it
  is on layer `DebrisEscaping` (8 = Debris, 9 = DebrisEscaping; set by `ProjectSetup.ConfigureLayers`), which ignores
  debris so it passes through piles. Chunks lock (kinematic) once at rest. Tuning: Debris section of
  `Data/TowerVisualSettings`. Bot check: `Playtest.AutoShoot`, full level, then count chunks `InNoGoZone`.
  Stress probe: `TowerLand.Editor.PlaytestDebris.Rain(@"<dir>", waves, cubesPerWave)`.

## Levels & debug scene
- Levels: `Levels/LevelConfig` (columns, floors, layers, colours, seed, pattern, baked `Thumbnail`) listed in play order
  by `Levels/LevelList.asset` (Level_01..10; all 26 columns - the plinth and camera are fitted to that). Pattern variants
  in `Levels/Patterns/` (Voronoi_Large/Default/Wide/Tall/Round/Busy). `Tools/TowerLand/Build Level List`
  (`Editor/DebugSceneBuilder`) (re)creates them from its table and bakes thumbnails through the Game scene's camera
  (cropped to the tower) into `Art/UI/LevelThumbnails/` - re-run after changing levels or the game's look.
- Scene hand-off: `Data/LevelSession.asset` (`Levels/LevelSession`, non-serialized pick, kept loaded across the switch)
  - `LevelBootstrap` plays the picked level with its own seed (= its thumbnail), else the scene's default level.
- Debug scene `Scenes/Debug.unity` (build index 1, after Game): `UI/DebugMenu` builds a tabbed overlay UI at runtime;
  each `DebugCategory` component on it is a tab (`Order`, `Title`, `Build(menu, content)`, `Tick`), using the menu's
  widget helpers (`Text`, `Button`, `Box`, `Row`). Tabs: `LevelsCategory` (thumbnail grid, tap = play) and
  `InfoCategory` (build / device / FPS). Add a tab = add a component. `Tools/TowerLand/Build Debug Scene` rebuilds it,
  updates build scenes (`ProjectSetup.ApplyBuildScenes`) and wires the Game scene; "Back to game" returns.
- Secret access (all builds): `UI/DebugCorner`, an invisible hotspot in the HUD's top-right safe-area corner. 5 quick
  taps (`TapSequence`, <= 0.5 s apart) -> `UI/PasswordPrompt` (password "debaser", case-insensitive; device keyboard
  opens) -> `Core/PlayerProgress.IsDebugger` saved (PlayerPrefs `progress.debugger`) -> debug scene; afterwards 2 quick
  taps open it. Editor: no password (5 taps unlock directly), backquote also opens it; `Tools/TowerLand/Reset Debugger
  Unlock` forgets the flag. UI taps need the Game scene's EventSystem + HUD GraphicRaycaster (`AddDebugCorner`); the
  `Shooter` ignores presses on any UI raycast target. Probe: `TowerLand.Editor.PlaytestCorner.Tap(count, interval)`.
- Aiming through a hole probe: `TowerLand.Editor.PlaytestBehind.Run(@"<dir>", floorFrom, floorTo, halfAngle)` punches a
  window through all layers, aims at the far wall through it, throws and logs whether the far cube broke;
  `PlaytestBehind.Sweep(@"<file>")` runs 20 such throws on fresh towers and flags any frame with the projectile inside a cube.

## Win sequence
- `TowerView.Cleared` (last cube gone) -> `Gameplay/WinSequence`: stops the `Shooter`, waits for debris to settle (max 1.5 s),
  then a glass vacuum hose lowers in from above the screen, hovers to settle (`GatherDuration`), spins its suction up
  (`SpinUpDuration`: capture reach + air streaks ramp), sweeps once round the tower over every debris pile (waypoints
  = occupied 20-deg sectors). Each grabbed piece is simulated on its own (world space, so it lags behind the moving
  nozzle): staggered lift-off (`PullDelay`), kick, then dragged by an air flow (never a sideways force -> no orbits) into
  its own spot across the mouth opening (`IntakeSpread`): `AirSpeed` at the mouth falling off over `AirFalloff`, floor
  `AirMin`, x(1 + `AirRamp` * flight time), followed with `Grip` against `Weight`, `Vortex`, `Flutter`; within `Funnel` m of its spot it is committed and heads straight in (no hovering round the nose); once in the mouth it flows up the hose from that spot, accelerating and
  slowly corkscrewing (one loop over all pieces). The nozzle's facing is an under-damped spring on its horizontal speed
  (`Swing`), so it leans into moves and sways when it stops;
  final gulp of stragglers, anticipation dip + yank out, then `UI/WinScreen` ("TOWER LAND"; camera rises while the
  scene fades to black behind the title; tap -> reload scene).
  **D** (editor / development builds) = `TowerView.BreakAll` -> straight to it.
- `Feel/VacuumHose`: procedural corrugated tube rebuilt each step through spring-driven joints (rest curve = Bezier from
  the off-screen anchor + corkscrew `Twist`), swells where pieces travel (`AddLoad`), joint rings + nozzle squash (`Gulp`).
  Nozzle and joint ring are modelled in `ArtSource/Blender/Vacuum.blend` -> `Art/Models/Vacuum/*.fbx` (nozzle: hose
  connects at y=0, mouth plane at y=-1.6, faces -Y). Glass = `TowerLand/Frosted Glass` (`VacuumGlass.mat`: blurred +
  refracted camera opaque texture, which `WinSequence` enables on the camera only while the hose is on screen), accents =
  `VacuumCuff.mat`. Tuning: `Data/VacuumSettings`. Setup: `Tools/TowerLand/Setup Win Sequence` (Build Core Assets runs it).
- Flip-book probe: `TowerLand.Editor.PlaytestWin.Record(@"<dir>", seconds, interval)` presses D and saves small JPEG frames.
- The title auto-continues after `_titleTime` (1.4 s, on `WinSequence`) into the build scene, preloaded during the outro;
  without a Build scene in the build list the level reloads.
- Title text (win screen) = TextMeshPro (essentials imported to `Assets/TextMesh Pro/`), font Lilita One (SIL OFL,
  `Assets/ThirdParty/Fonts/LilitaOne/`) -> `Art/UI/LilitaOne SDF.asset` + `Title_*` materials (TMP mobile SDF), styled by
  `Data/TitleStyle` (`UI/TitleStyle`: gradient face, extrusion depth/layers, colours). Per letter, back to front: dark
  outlined silhouette stack (back-most copy casts the underlay shadow), coloured stack without outline, outlined face.
- Loose cubes (projectile, chunks, falling cubes) use `TowerView.LooseMaterial` - tower-only shading is off.
- `PlaytestTrace.ThrowAndTrace(@"<file>", x, y)` logs a thrown cube's path per frame (+ screenshot at 0.5 s).
- Play-mode bot: `TowerLand.Editor.Playtest.AutoShoot(@"<dir>", n)` (Run In Background on) aims at matching cubes via
  a virtual mouse and writes autoshot.txt + screenshots. Full-res screenshots are ~10 MB each and C: is nearly full:
  write them under `D:/ASG/TowerLand/Temp/`.

## Meta game (build scene)
- Loop: Game (win) -> `Scenes/Build.unity` (build index 2) -> tap -> Game (next level). Each side preloads the other
  (`LoadSceneAsync`, activation held) and switches under black (`UI/ScreenFade`; `LevelSession.FadeIn` tells the Game
  scene to fade in). Keep it snappy: a visit is a few seconds; holding a finger speeds it up (`HoldSpeedUp`).
- Economy: 1 brick per tower cube, in its colour. `LevelBootstrap` records a win (`TowerView.Cleared`): `PlayerProgress.Level`
  +1 and the level's cubes (`TowerView.BuiltColours`) deposited as bricks, sorted into colour bands by `Meta/BrickSorting`
  (order from chaos: rainbow order, each haul running the way that starts nearest the colour on top, so hauls ping-pong
  into gradients and the tower grows in stripes like coloured sand). Progression plays
  `Levels/LevelList.asset` in order (a debug pick plays that level, then continues after it); past the end its second
  half repeats with new seeds.
- `Core/PlayerProgress` (PlayerPrefs): `Level`, `Construction`, `Bricks` (one base-36 char per brick, placing order;
  may exceed the construction - the rest carries over on `CompleteConstruction`), `BricksShown` (already animated).
- `Meta/TowerShape` (plain C#, tested): round castle tower of bricks - footing ring, hollow 2-thick wall, corbel,
  crenellated battlements; staggered rows; slots in placing order (row by row, round by angle). `TowerBlueprint` assets in
  `Data/Constructions/` listed by `ConstructionList` (last repeats). Sizes set by `MetaSceneBuilder.Constructions`
  (Tower_01 completes on level 10: 7162 bricks <= levels 1-10 = 7182 cubes; then ~1.5x each).
- `Meta/BuildSequence` (composition root): shows the bricks already standing, then the vacuum hose (`Feel/VacuumHose`,
  same parts as the win) spits - a stiff, mechanical feel unlike the sucking: its own rigid tuning
  (`Data/VacuumSettings_Spit`: stiff/damped joints, no twist), drops in fast and locks centred high over the construction
  (`MouthHeight` above the top, never below `MinMouthHeight`); the nozzle swivels towards the bricks it is shooting
  (`AimShare` of the way from straight down, critically damped `AimSpeed`: no swing); bricks are fired in shots
  (`ShotInterval`; each shot kicks the nozzle up on a stiff spring and squashes it), race down the glass and fly a short,
  fast arc onto their slots (land pop, sparkle every 25); hose yanks out; camera rises straight up (background stays valid) to keep the top under `TopMargin`.
  Finished construction -> "TOWER BUILT!" (title at the top) and the next visit starts the next one. `Meta/BuildHud` =
  name + progress bar + "Tap to continue". Bricks are GPU-instanced (`Meta/BrickRenderer`, chamfered `Art/Meshes/Brick.asset`,
  108 tris, loose-cube material per colour via `Tower/CubeMaterials`) - no GameObjects. Tuning: `Data/BuildSettings`.
- Environment = the world's **sky variant** (each world has two: `EnvironmentBuilder.Variant.Ground` for gameplay, `Sky`
  for the build scene - why the camera flies up after a win): the same meadow as a floating island. Source
  `ArtSource/Blender/World1_Sky.blend` (a copy of World1_Environment; scene `TowerLand_World1_Sky`) ->
  `Art/Models/World1/World1_Sky_Environment.fbx`. Ground = island top cut to a contour (back rim ~25 m behind the tower),
  `IslandCliff` = rim walls (grass lip, soil, rock strata) + underside; `World1_Islets` = floating islets with trees whose
  cliffs/undersides face the camera (a convex island never shows its own walls from this low camera - the islets carry
  the "cliff" read); `Clouds_Sea` (far below, `World1_CloudSea.mat`, bluer sides) + `Clouds_Wisps` at island height +
  `SkyPeaks_Far` poking through. All stay 3D (no painted background); thinner fog (`SkyFogStart/End`), far clip 2500;
  `Soft Cloud` now takes fog. Generators live in the .blend's text blocks (`sky_helpers.py`: palette swatches, mesh/AO
  helpers; `sky_island.py`: `make_contour` / `make_top` / `make_cliff`; `sky_export.py`: `export_sky()`).
- `Tools/TowerLand/Build Meta Scene` (`Editor/MetaSceneBuilder`) rebuilds blueprints, brick mesh, the scene and the build
  list, and wires the Game scene's `LevelBootstrap._levels`.
- Development: in the Build scene, B (after the show) pours 700 random bricks. Probes: `PlaytestBuild.Prepare(built, pending,
  construction)` sets the meta progress (edit or play mode), `PlaytestBuild.Record(@"<dir>", seconds, interval)` flip-book
  (its JPEGs look washed out - judge colour from `ScreenCapture.CaptureScreenshot`). In probes, break the tower with
  `TowerView.BreakAll` directly: a virtual D press doesn't reach an unfocused Game view.
