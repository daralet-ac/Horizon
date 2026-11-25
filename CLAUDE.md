# Working on this plugin

Horizon (`Horizon.dll`, `Horizon` as its namespace) is a Decal plugin for Asheron's Call that draws the world further
and lights it better, built from the game's own art: draw distance past the game's, far land, billboards and 3D far trees,
far buildings, fog, shading, sun and lamp shadows, water reflections, grass, anti-aliasing and glow, a frame rate and
memory window, and the test runner used to check builds in the game. Nothing in it talks to the server, so it should work
on any server. It was Daralet Toolkit's Graphics tab (`../daralet-toolkit`, the Daralet server's player plugin) until
November 2026, when it was split out into this repo: much of what's below was written there, and names a Toolkit class
(`ClientElements`, `ItemIcons`, `OptionsPanel`, the Config tab...) where Horizon has its own or none. The Toolkit's
CLAUDE.md is the fuller record of the client's internals (UI elements, layouts, the game's panels).

**The guiding rule for all graphics work is to show Asheron's Call's own art better, never to add effects for their own
sake** (the user's call): every feature takes its light and colours from the game, is subtle, and is its own switch, off
to start.

## The stack

- SDK-style project, `net48`, `PlatformTarget` x86, `LangVersion` 12. No C# records: net48 has no `IsExternalInit`.
- UI: [UtilityBelt.Service](https://gitlab.com/utilitybelt/utilitybelt.service) 2.2.12 from NuGet, with
  `ExcludeAssets="runtime; build; contentFiles"`, because it's installed on the client. Windows are
  `UBService.Huds.CreateHud(name)` with `OnRender`, drawn with ImGui (`ImGuiNET`), through `FloatingWindow`.
- `deps/` holds `Decal.Adapter.dll` and `Decal.Interop.Core.dll` from Decal 2.9.8.3, and `UtilityBelt.Common.dll` from
  UtilityBelt.Service 2.2.12 (ACE's dat readers, `ACE.DatLoader`), all referenced with `Private=False`. It's a .NET
  Standard dll, so a throwaway console app (net48, x86) can reference it and open a game folder's dats to try dat code
  outside the game.
- Managed DirectX (`Microsoft.DirectX`, `Microsoft.DirectX.Direct3D`) from `$(WINDIR)\Microsoft.NET\DirectX for Managed
  Code\1.0.2902.0\`: the game's device, its camera, textures and buffers. UtilityBelt installs it.
- MSB3277 is suppressed: UtilityBelt.Service was built against Decal.Adapter 2.9.8.0 and Decal installs 2.9.7.5.
- The loader (`src/Horizon.Loader`, `Horizon.Loader.dll`) is a Decal network filter that loads `Horizon.dll` from bytes
  and reloads it on every rebuild (UtilityBelt's HotDecalPluginTemplate). Enable it under Network Filters and disable
  Horizon under Plugins.
- Shaders are HLSL in `src/Horizon/Shaders`, compiled ahead by `Shaders/build.cmd` (the Windows SDK's fxc) into
  `Resources/shaders/*.cso`, which are checked in and embedded.
- Horizon's guid is `fa1952c0-b6cd-42a1-ae01-ad12f0120a4c` (`Properties/AssemblyInfo.cs`), the loader's
  `06c6724d-e867-4206-b060-e692e2003066`: never change them, and never reuse them in another plugin.

Build and test:

```bash
dotnet build Horizon.sln -c Release
dotnet test tests/Horizon.Tests/Horizon.Tests.csproj
```

## The plugin's own pieces

- **The Horizon window** (`Ui/GraphicsPanel*.cs`, window `horizon`, hidden to start, shown and hidden with `/horizon` or
  `@horizon`): every graphics setting, the Budget section (presets, the two bars, Apply) pinned at the top and the
  sections scrolling under it, then a Horizon section (Text size, crash records). `GraphicsPanel` also makes and owns
  every graphics piece (`DrawDistance`, `FrameRateWindow`, `Grass`, `FarLand`, `DepthShading`, `TestRunner`), which run
  whether or not the window shows, and lets each go on its own at shutdown.
- **Settings** (`Settings.cs`, `SettingsData.cs`, `WindowPlacement.cs`): one `settings.json` in `Documents\Decal
  Plugins\Horizon`, for every character (graphics belong to the computer, not the character; no profiles). The graphics
  settings keep the names the Toolkit saved them under, so the first time Horizon starts it reads them from the Toolkit's
  `settings.json` (`Settings.ImportFromToolkit`, once, `toolkitImported`), with the Frame Rate window's placement. A
  DataContract reader skips field initialisers, so new settings get their defaults in `SetDefaults`. Saved two seconds
  after the last change. A renamed setting keeps its old `DataMember` name, or players lose it.
- **Everything the plugin writes** goes in `Documents\Decal Plugins\Horizon` (`DataFolder`): `settings.json`, `log.txt`,
  `Frame Rate Logs`, `Test Runs`.
- **The installer** (`installer/Horizon.iss`, Inno Setup 6, built and released by `.github/workflows/release.yml` on a
  `v*` tag that must match the csproj's `<Version>`; run by hand from the Actions tab it only builds, the files under the
  run's artifacts): Daralet Toolkit's installer with Horizon's names. It copies `Horizon.dll` and writes Decal's entry
  itself (`(Default)`, `Enabled`, `Object` `Horizon.PluginCore`, `Assembly`, `Path`, and `Surrogate`
  `{71A69713-6593-47EC-0002-0000000DECA1}`, Decal's .NET plugin host) under 32-bit `HKLM\SOFTWARE\Decal\Plugins\{guid}`.
  Its default folder is the `Path` of an entry already there. It stops while `acclient.exe` or `DenAgent.exe` runs,
  warns when .NET 4.8 or Decal 2.9.8 isn't found, and runs UtilityBelt.Service's installer (from its NuGet package's
  `content/`) when Decal has no Services entry for it. Uninstalling removes the entry and offers to delete the data
  folder. Its `AppId` (`f209cff6-7c4a-465f-ba2f-6e90964f3cc7`) is how a new installer upgrades the old install: never
  change it. The workflow installs Managed DirectX first, both `Microsoft.DirectX.dll` and `Direct3D`, which runners
  lack; a release's notes come from `release-notes/<tag>.md`, else GitHub's list of merged pull requests.
- **New versions** (`Feed/UpdateCheck.cs`, `Ui/GraphicsPanel.Update.cs`): Daralet Toolkit's update check, pointed at
  `daralet-ac/Horizon`. At start, once and off the game's thread, it asks GitHub's API for the latest release (never a
  draft or pre-release) and compares its tag with the assembly's version, both made four-part. A newer one gets a line in
  chat once logged in, with a link to the release page, and an Update button above the budget; the Horizon section shows
  the version you have, with the check's result as its tooltip. Chat links carry `PluginCore.ChatLinkId` ("Horz", not the
  Toolkit's), and a click only opens Horizon's release pages (`UpdateCheck.IsReleasePage`), since anyone can put the id
  in a tell. It only tells: the player runs the new installer. Unauthenticated, GitHub allows 60 requests an hour from
  one address.
- **The game's UI pictures** (`Resources/ui`, drawn by `Art.DrawArtBox`): the option slider's track and thumb
  (`option-slider*.png`, `Sliders`) and the gold close button (`indicator-logout*.png`, `FloatingWindow.Close`).
  Drop-downs and scroll bars are ImGui's own here (the Toolkit draws the game's).
- **Settings pages** are built from `Form` (`Ui/Form.cs`): a `Section` heading per group, forms of labels and lined-up
  controls, rows that wrap (`Flow`). Sliders are `Sliders.SliderInt` and `SliderFloat`, never ImGui's directly. Tooltips
  open with `Widgets.BeginTooltip`, child windows with `Widgets.BeginChild`. Keep tooltips to one or two short sentences.
- **Floating windows** (`Ui/FloatingWindow.cs`): small auto-sized UB huds that stay on screen (fellowship vitals, target,
  tracked target). Locked means no dragging by the top edge and no sizing, and with Ctrl held
  any window, locked or not, is dragged from anywhere on it
  (`FloatingWindow.CtrlDrag`, by hand: ImGui only moves a window from its empty background, and a hotbar is all slots),
  its contents disabled meanwhile (`BeginDisabled`, `DisabledAlpha` 1 so nothing greys out) so a slot isn't used on the
  press; a window held to the toolbar (`Anchor`) isn't moved. A window the player sizes with `CtrlResize` set (the Vendor
  window) can be resized by its edges too while Ctrl is held: NoResize is lifted then, and an edge taken hold of
  (ImGui's grips being the only active items while the contents are disabled, `TrackCtrlSizing`) goes on resizing till
  the button's let go, and isn't also moved. `CtrlResize` is on for every window now (the user's call, November 2026:
  nearly every window adjustable by its edges), not only the Vendor's. **A window that sizes itself** (no
  defaultSize: the target, fellowship, hotbars, toolbar, quest tracker and the like) is sized by its edges too
  (`FloatingWindow.EdgeScale.cs`): dragging its left, right or bottom edge or a bottom corner changes its Size, the
  scale its contents are drawn at, by as much as the edge has gone (snapped as a resized edge is), the top edge still
  being the move; unlocked as it is, locked with Ctrl held; the strip is the window's padding (at least 3 pixels), so
  a hotbar's slots keep their clicks; worked by hand like SideGrips, which a window with its own (the bars) has
  instead. Untested in the game when written. Ctrl+click in a locked Corpse grid moves it instead of
  ticking. While Ctrl is held each such window gets a faint wash and frame in the theme's accent
  (`DrawCtrlMoveHighlight`), brighter on the one being dragged. Right-click one to lock, unlock or hide it. Two things decide
  whether one is drawn, worked out every frame (`Core_RenderFrame`): `Visible`, the saved setting its tab's checkbox and
  Hide change, and `inUse`, what it's showing now (null for nothing). A window with **Only when needed**
  (`WindowPlacement.OnlyInUse`) is drawn only while in use; the Corpse and Bank windows always are (`alwaysOnlyInUse`).
  Those two also have `onClose`: their close button (X), Close in the right-click menu (a locked window has no X) and the Bank window's Close bank close
  the corpse or bank (a Use of it), and the window stays away until it shows something else (`closedFor`), without touching
  the setting. Nothing sets `Visible` behind the player's back any more, so Hide holds; Track and a new destination still
  switch their windows on, as asked for. **Background** (`WindowPlacement.Background`, null for the old 0.55 locked / 0.85
  unlocked) fades each window's box; at 0% it's `NoBackground`, and the style's `WindowBorderSize` is zeroed for that
  window's Begin and put back in `Hud_OnRender` (or the next `Core_RenderFrame`), since UB begins the window between the two.
  **No title bars** (the user's call, November 2026; `Ui/FloatingWindow.Edge.cs`): locked or not, a window has none
  (`NoTitleBar | NoMove` always). Unlocked, it's moved by its top edge, as the game's panels are (`EdgeDrag`): a band
  from 4 pixels above the window (ImGui's own top-edge resize reaches that far) to 6 inside, wherever no item is under
  the mouse, takes hold on a press and moves `Look`, snapped, like a Ctrl drag (`RememberPosition` skips while it's on:
  it runs after, and saving the window's old place undid the move every frame, the first build); ImGui's resizing is off while the mouse
  is on it or a drag is on (`edgeHover`, `edgeGrab`), or that edge would resize the window instead. Its other edges and
  corners still size it. The mouse shows the game's move cursor there (`WantsMoveCursor`, through `CursorKeeper`). So
  locking no longer moves anything; before, it took the title bar away and moved the window down by its height (and
  shrank one the player sizes by it), so what was in it stayed put. Settings from before (`WindowPlacement.NoTitleBar`
  false) are moved once the same way for an unlocked window (`UpgradeFromTitleBar`: down by the height ImGui gave the
  title bar, a player-sized one that much shorter). With no title bar there's no close button: the right-click menu's
  Hide and Close do that, but a window with `CloseButton` set (the appraisal windows) has the game's gold X in its top
  right corner (`FloatingWindow.Close.cs`). Untested in the game when written. **Snapping** (`Ui/FloatingWindow.Snap.cs`,
  `Feed/WindowSnap.cs`, the Horizon window's snapping, two rows, Snap and Grid, each Moving, Resizing and a slider: `SettingsData.SnapWindows`, on to start, `SnapDistance` 10,
  `SnapGrid` off, `SnapGridSize` 20, `SnapGridShown`): a window being dragged snaps to the screen's edges, to the edges
  of the plugin's other windows showing (each notes its box every frame it's drawn, `NoteBox`; an edge only lines up
  with a window beside it the other way), and with the grid on to the grid where no edge is near; Shift places it
  freely. ImGui moves an unlocked window itself, so one is taken to be dragged while the button's down, it's focused and
  it moved without changing size (`SnapWhileDragged`), and its snapped place is put on it (`SetWindowPos`) before
  `RememberPosition` saves it; ImGui works the place out from the mouse every frame, so nothing builds up. A locked
  one's Ctrl drag snaps in `CtrlDrag`. **Resizing snaps too** (`SettingsData.SnapResize`, on to start;
  `FloatingWindow.SnapSize`, `WindowSnap.SnapEdge`): a window the player sizes has the edges it's being resized by
  snapped to the screen's, other windows' beside it and the grid, as it's dragged, through ImGui's size callback
  (`FloatingWindow.SetSizeConstraints`, cimgui's `igSetNextWindowSizeConstraints` called directly with a cdecl
  delegate kept in a field: see the gotcha on ImGui.NET's callbacks): ImGui works a
  resize out from the mouse inside Begin, every frame, and asks the callback before it uses it. The edge moving is the
  one nearer the mouse (ImGui keeps the other put, moving the window for a left or top edge). The first build snapped
  after Begin, which ImGui undid the next frame, so it only showed once let go. The grid is drawn faintly on the
  background list while one's dragged or resized. **Guide lines** (the user's call, November 2026; `WindowSnap.Guides`):
  with the grid on, the screen's middle (3 pixels) and its thirds and quarters (2) are drawn thicker over it, a
  window's middle snaps to them as it's moved and the edge being dragged as it's resized, as near as any edge, and the
  guide the middle (or that edge) is on is drawn lit, in gold (`DrawGrid`'s `litX`, `litY`, `WindowSnap.OnGuide`): the
  grid is drawn once the snapped place is known (`ShowGrid`). **Snap resizing to the grid** (`SnapResizeGrid`, on to
  start, the user's call, November 2026) is a setting of its own: resizing snaps to the grid and its guide lines with
  it on, whether or not moving does (`ResizeGridSpacing`, where moving has `GridSpacing` and Snap to a grid), and so
  do the width grips (`GamePanel.WidthGrip`, the XP bar's end), which change a window that sizes itself. ImGui asks
  the size callback of windows that size themselves too, whose wanted size can differ from their own on any click,
  so with that on by default the grid showed for every press: `SnapSize` now only acts for a window the player sizes
  and a press begun on its edge (`TrackResizePress`: 4 pixels outside to half a line inside, more at the corners).
  Untested in the game when written. A window doesn't snap to those held to it (`Follows`), or its edge
  against theirs held it in place and it moved in jumps. Untested in the game when written. `RememberPosition` skips a frame
  whose new position is still on its way (`positioned` false), or it would save the old one back. **Only a drag moves a
  window**: every frame the left button isn't down, `SetNextPlacement` puts it where it was saved (`ImGuiCond.Always`),
  and `RememberPosition` only saves while the button is down. ImGui pulls a window back inside a screen smaller than where
  it is, which the game's is for a moment around login: the hotbars came back in the middle of the screen every login,
  and that place was saved over theirs.
  These windows have `NoFocusOnAppearing`; `BringToFront` focuses one once (Appraisal uses it, so a new appraisal isn't
  hidden behind the main window). `menuItems` adds to a window's right-click menu, which never opens over an item, so whatever draws the item opens it
  (`FloatingWindow.OpenMenu`, at the window's own ID scope): the fellowship window's is the vitals and Pin of
  the fellow right-clicked (`SettingsData.FellowVitals`, by name, each vital null to follow the Stamina/Mana checkboxes).
  Vital and threat bars are never shorter than the font (`Widgets.Bar`), nor narrower than their numbers
  (`Widgets.VitalBarWidth`): a big font wrote each bar's numbers over the next. Clicking a threat row selects that
  creature or player. The Target and Tracked Target windows share their vitals' settings, as the fellowship window has
  them: Health, Stamina and Mana each shown or not, Side by side and Bar width (`TargetShowHealth`..., `TargetSideBySide`,
  `TargetBarWidth`, 220 to start), with spells and threat as wide as the row of bars; the Combat tab's Vitals shows all three.
  They're in both windows' right-click menus too (`TargetTab.RenderVitalsMenu`), which open from anywhere on the window
  (`OpenMenuOnRightClick`), since it's nearly all bars and the menu doesn't open over items.
  The **Map window** is the World tab's map in a `FloatingWindow` with its own `MapView` (its own pan,
  zoom and Follow me) and the tab's **Show...** (kinds of places, names, fellows, the names' Text size, and the Arrow
  and Dungeon windows, the last three added to the popup for it); the arrow window's **Details** box leaves the arrow
  alone.
- **Themes** (`Feed/UiThemes.cs`, `Feed/UiTheme.cs`, `Ui/UiThemeStyle.cs`, Horizon's settings,
  `SettingsData.Theme`, null for Daralet, the default): ready-made looks for the plugin's windows. A theme is a few base colours
  (window, frame, title, accent, text; optionally solid `Button`, `Selection` and `Edge` colours of their own, as
  Daralet has, sampled from the game's keyboard and options panels) and a corner rounding; `UiTheme.Colors` works out every ImGuiCol from them, by
  name, so a colour an ImGui version renamed (`TabActive`/`TabSelected`) is set under whichever it has. The UtilityBelt
  theme (`Unstyled`) changes nothing: its own look. One ImGui serves every plugin, so `UiThemeStyle.Apply` changes the style in a hud's
  OnPreRender (Begin draws the background, title and border) and `Restore` puts it back at the end of its OnRender (so
  its popups and tooltips are themed too) and, for a window that began collapsed, on the next `Core_RenderFrame`. Not
  PushStyleColor: a push before Begin popped before End trips ImGui's stack check. All are dark with light text, since
  `Colors` is made for a dark window (a test checks). A new theme is a row in `UiThemes.All`.
- **log.txt for crash reports** (`Ui/SessionLog.cs`, `Feed/CrashReport.cs`, `Feed/GraphicsSummary.cs`; the user's call,
  November 2026: a player's log.txt alone should be enough to look into a crash). Trimmed past 4 MB into log.old.txt at
  start. `session.txt` holds the game's process id and the session's start, and a clean shutdown deletes it: one left by
  another process means the last session didn't end cleanly (a hot reload is the same process). A `[session]` header
  at login: version, screen, free memory, every graphics setting. The crash itself kills the process where no .NET code
  can catch it, so its address is only in Windows' Application event log: **opt in** (Config, Troubleshooting, "Read the
  game's crash records", `SettingsData.ReadCrashRecords`, `readCrashRecords`, off to start; the user's call: reading
  the event log is normal and read-only, but players may wonder), and after an unclean session the plugin asks it, off
  the game's thread, for acclient.exe's "Application Error" and "Application Hang" entries since then, each a
  `[crash]` line with the address (0x400000 plus the fault offset) and the nearest of AcClient.Entrypoint's functions at
  or below it. Not opted in, the log says how to turn it on. `PluginCore.LogFile` is locked for the other thread.
  Untested in the game when written.
- **Draw distance** (`Ui/DrawDistance.cs`, `Ui/GraphicsPanel.DrawDistance.cs`, `Feed/DrawDistanceOptions.cs`,
  `SettingsData.DrawDistance`, off to start; the user's call, November 2026): a group of the Config page's after
  Rendering Quality, the plugin's own, for drawing further than the game allows. The game makes scenery (trees,
  bushes, rocks) only for landblocks in full detail: `CLandBlock::init_static_objs` (`0x531780`) returns unless the
  block's side_cell_count (`+0x48`) is 8, and `LScape::get_block_orient` (`0x5059F0`) gives full detail only to blocks
  1 landblock or less from yours (by the larger of the x and y offsets), a step of 2 to 2 or less, 4 to 4 or less, 8
  past (side_cell_count 8 over the step): so trees only ever stand in the 3x3 landblocks round you, whatever the
  Landscape Draw Distance. **Scenery** patches those three ring sizes, the immediates of each ring's `cmp eax,n` and
  the `mov eax,n` after it (`0x505A16`/`25`, `41`/`50`, `57`/`66`), to R, R+1, R+3 (1 to 16; 5 at first, raised once Scenery density made far scenery affordable, to see how far it
  goes, the user's call, November 2026), checking the opcodes and
  the value first and leaving a client that differs alone, put back when off or at shutdown (VirtualProtect, the
  plugin's first code patch). **Landscape** is `LScape::mid_radius`, the game's Landscape Draw Distance (the int at
  `0x81FFB4`: its choices 3, 5, 8, 11, 15; the Graphics Quality preset sets it too, `0x54BC30`), applied with
  `SmartBox::set_mid_radius` (`0x4531C0`, thiscall on `[0x83DA58]`), which releases every block and loads them again,
  any radius of 1 or more being taken (`LScape::SetMidRadius`, `0x505660`; the LScape is SmartBox `+0xB0`); up to 24
  here, set again once a second if the game puts its own back (a change of region does). The patched rings need that
  reload too. **Detail, taken out** (November 2026, the user's call): the game's Degrade Distance, the float at
  `0x81FC68` (50 to start, its setting's storage), which `GfxObjDegradeInfo::get_degrade` (`0x51EFC0`) takes off the
  distance before picking an object's level of detail, raised up to 1000 m. It cost a lot of frame rate, and teleports
  crashed with it at 1000 (`0x58712F`, `0x539153`, `0x52718B`): round trips Holtburg to `0xE74E001D` crashed in the
  first or second every time with it on, far land or not, and never in 10 with it at the game's; holding it at the
  game's through portal space didn't help. Screenshots at the overlook at 50 and 1000 m were the same but for clouds.
  Its saved `detailDistance` is left unread. **Far scenery, taken out** too (November 2026, the user's call: the
  game's trees reach only 1 or 2 landblocks now, 3D trees and billboards past them; saved `lowDetailDistance` left
  unread; it was `Ui/DrawDistance.Lod.cs`, up to 3000 m) only stopped far objects vanishing: `CPhysicsPart::UpdateViewerDistance` (`0x50EB00`) and
  `UpdateViewerDistance_` (`0x50EC70`) skip a part whose chosen level's model (`gfxobj[deg_level]`, part `+0x20`) is
  null, a tree's last level being empty; both calls to get_degrade (`0x50EC37`, `0x50ECC4`) are hooked
  (`AcClient.Hook`, thiscall), and within that distance the nearest level down with a model is used instead, with its
  mode (the degrade info's list at `+0x3C`, 20 bytes each, mode at `+4`; the part found as the level's address less
  `0x14`); hooked only while wanted, removed at shutdown. It goes by the part's real distance: the one
  `get_degrade` is given is the real one over the part's scale (`fdiv [esi+2Ch]`, `0x50EC2B`), so a big tree seemed
  nearer than it was; the real one is the part's first field (CYpt, stored first, `0x50EB7E`, `0x50EC7B`).
  **Plainer models sooner, tried and taken out** (November 2026): `get_degrade` (`0x51EFC0`) takes the distance less
  the Degrade Distance, held at 0 or more, and picks the first level whose ideal distance (stretched by the game's
  multiplier) it's under, so scaling the distance it's asked about past the Degrade Distance makes models plainer
  sooner. Three ways were built through this hook: three **detail bands** (a start distance and a percent each; up
  to 400% at first, which only did what Detail does), three **level distances** (a set distance for each model
  level, every object alike), then one **Detail falloff** slider in their place (1x to 10x, the user's call: "a
  single simplified slider that decides how quickly the detail lowers over distance"). None changed anything in the
  game: much of the scenery has one real model then an empty last level (how the game hides a far tree), so a
  falloff only reached the empty level sooner and Far scenery put the one real model back; and the frame rate's
  cost is the number of objects, not their models. Wanted again with billboards, where "how soon a tree becomes a
  billboard" is a falloff (the user's call). Their saved values (`modelDetail`..., `levelDistance1`...,
  `detailFalloff`) are left unread. The game can force a level itself (`0x8452F4` non-zero, every object finest;
  `0x81FC7C` not -1, every object that level, both read at the top of `get_degrade`).
  Measured in the game before it (the user, November 2026): about 160 fps to 14 on a mountain top among big trees
  with scenery and detail at their most, so the object count, not the models, is likely most of the cost; turning
  model detail down didn't help either. **Scenery density** (`SceneryDensity`, 10% to 100%, `Ui/DrawDistance.Density.cs`,
  `Feed/SceneryDensity.cs`; the user's call, November 2026): `CLandBlock::get_land_scenes` (`0x5311A0`) asks
  `CLandBlock::on_road` (`0x530D30`, thiscall, the place's Vector3 in block meters) at `0x53150E` before making each
  scenery object, and skips it on a road; that call is hooked (`AcClient.Call.CLandBlock__on_road_0x0053150E`), and an
  object not on a road is called one when `SceneryDensity.Keeps` says so: a hash of the landblock id (the CLandBlock's
  `+0x28`, which the game puts in the object's Position at `0x53155D`) and the place, under the percent, so the same
  ones go every load and a lower density only takes more away. Buildings and the dats' placed objects don't pass
  through it. A change loads the landscape again. It's the same everywhere, your own landblock too: the game only
  rebuilds a block whose detail changes, so a block thinned for being far would stay thin as you walked into it.
  Untested in the game when written. Lowering it gave fast gains in the game (the user, November 2026).
  **Far scenery per frame, tried and taken out** (November 2026; rows Scenery cut-off, Thin far scenery and Slow far
  models): the game draws a cell's objects in one loop (`0x507A50`), calling for each
  `CPhysicsObj::UpdateViewerDistanceRecursive` (`0x511720`, at `0x507B17`) then `CPhysicsObj::DrawRecursive`
  (`0x511760`, at `0x507B22`), and both calls were hooked to skip "scenery": a plain CPhysicsObj (vtable `0x7C78EC`;
  CBuildingObj's is `0x807550`) with id 0 (`+8`) in an outdoor cell. **That loop isn't where the land's trees are
  drawn**: it saw 1 to 7 such objects a frame at any scenery radius, and thinning them turned **the sky black**, so
  they're the sky's pieces (dome, sun, moon); not one tree was reached. The cut-off went first (the point is foliage
  as far as it'll go, and Scenery's radius ends it), the other two once the sky showed what they caught. Where the
  land's static objects are drawn is still to be found (billboards need it). Kept for that: the camera is the
  Position at `0x81FF10` that `UpdateViewerDistance` measures from (`Position::get_offset`, `0x50E3F0`); an object's
  Position is at `+0x48` (vtable, cell id `+4`, Frame `+8`: quaternion, the 3x3 matrix, the origin at `+0x3C`); a
  part keeps its object at `+0xE0`; a part's NoDraw bit is `draw_state` `+0x1C` bit 1 (`CPhysicsPart::Draw`
  `0x50E270`, `SetNoDraw` `0x50E0E0`); the dats' placed objects are made with id 0 too (`0x53180B`). Their saved
  values (`sceneryCutOff`, `farThinFrom`, `farThinPercent`, `slowModelsFrom`, `slowModelsEvery`) are left unread.
  **Seen in the game** (November 2026): scenery 14 at 12%, landscape 24, Detail 1000 m and Far scenery 3000 m ran at
  55 fps, then the screen went black once or twice; it hasn't come back since Detail went to the game's and the
  scenery steps up in place (`[device]` lines in log.txt would show a lost device).
  Scenery 4 to 5 landblocks is a big frame rate drop (seen in the game): 121 landblocks of scenery, every object's
  parts still worked through each frame, whatever model they end up with. The game's own multiplier still applies on top (`deg_mul`
  `0x86731C` while its automatic degrades, `0x81FC78`, are on, else its Object Degrade preference,
  `s_rUserSuppliedDegradeBias` `0x867318`). Untested in the game when written. Sliders are used once let go, since each reloads the landscape. `[draw-distance]` lines in
  log.txt, each radius change with the game's free address space. **A teleport crashed the game** with the landscape
  at 24 and scenery at 5 (after five hot reloads): in `CPolygon::make_plane` (`0x539153`, called from
  `CLandBlockStruct::AddPolygon`), reading a vertex through what looks like a null vertex array, likely an allocation
  that failed (the client never checks them; not confirmed). So with under 512 MB of the 32-bit game's address space
  free, the landscape and scenery go back to the game's, with a line in chat (`DrawDistance.Memory.cs`,
  `GlobalMemoryStatusEx`'s `AvailVirtual`), and what ran out is remembered (`ranOutAt`, by `Cost`: landblocks of
  scenery times the density, plus 12% of one per landscape block, about 1 MB, measured: it was 2%, which let landscape 24 with scenery 3 at 100% load at login and run the game out, crashing at `0x58712F`) so nothing as costly (90%) loads again this session;
  and with under 1 GB free nothing costlier than what's loaded is loaded at all (`MayLoad`), since a big load could run
  out before the once-a-second look. Seen in the game: scenery 10 at 20% left about 1.5 GB, 16 at 20% about 0.8 GB,
  and 16 at 53% ran out. The warning wraps on the page with the controls still there, and buttons to go back to the
  last settings that loaded before those (`FallBack`) or the game's own (the user's call, November 2026); any
  change tries again (`Soon` lifts the cap). **Settings saved too big crashed the game at every login** (scenery 16
  at 53%, landscape 24; `0x5393E4`, near the first crash's address): nothing was loaded yet and 2.8 GB was free, so
  neither rule stopped it, and `ranOutAt` doesn't outlive the game. So `MayLoad` also estimates: `MegabytesPerCost`
  (8; the game showed nearly 7, scenery 5 to 16 at 20% taking about 1.3 GB) times the cost added must leave 768 MB
  free, or nothing further loads and the page says why. At login the cost loaded is the game's own. Each load's real
  use goes in log.txt 5 seconds after it ("loaded cost ... MB a unit"), to correct the 8 by. **Big cold loads crash
  the game whatever the memory**: scenery 1 to 16 at 14% density, with 2.7 GB free and the estimate content, crashed
  in `CPolygon::UnPack` (`0x5393C6`, called from `CCellStruct::UnPack`, `0x534A40`, the cells of buildings and
  dungeons that come with each landblock) reading past the end of its dat data, as if its buffer was let go while in
  use; the login and teleport crashes (`0x5393E4`, `0x539153` in `make_plane`) are in the same code. Earlier, 5, 7,
  10, 15, 16 a step at a time held: each step reads only one more ring, the rest still in the game's cache. So the
  draw distance **steps up** (`Feed/DrawDistanceRamp.cs`, `Ui/DrawDistance.Ramp.cs`): 2 scenery rings and 3 landscape
  rings a step, each once the last has **settled** (`MayStep`: 1.5 s since it, and 1 s with no frame over 100 ms,
  `NoteFrame` timing Decal's RenderFrame), down at once. 2 every 4 seconds held but was too slow (1 to 13 in 25 s,
  the user's call); 3 every second crashed at login on the step from 10 to 13 (288 blocks of trees a second after
  the last step) with the game's own "OUT OF MEMORY: Failed to allocate -1178753584 bytes", a size gone wrong with
  2 GB free, likely a load running into the last. Behind another window every frame is slow, so stepping waits;
  **scenery is held 2 landblocks inside the landscape**
  (`DrawDistanceRamp.SceneryFor`, applied to the target and to each step, before the landscape loads in the same
  pass): Landscape 24 to 10 with Scenery 10 crashed in `make_plane` (`0x539153`) with 1.7 GB free in one piece, the
  edge blocks in full detail with no neighbour to fit their edges to (`CLandBlockStruct::AdjPlanes`, `0x5320F6`);
  the game's own settings never do that (its least landscape is 3 with full detail out to 1). At login, on any
  raise, and after a teleport: in portal space (Decal's
  `CharacterFilter.ChangePortalMode`) it goes back to the game's own, so the new place loads at the game's size, and
  steps up a second after you're out. The cost loaded is what each step has loaded, so the memory estimate follows
  the steps. `[draw-distance] stepping up` lines in log.txt. **Steps still crashed** (scenery 11 to 13 at 12%, in
  `make_plane`, 1.6 GB "free"): each step's `set_mid_radius` lets go of every block and loads them all again, and
  for a moment old and new overlap, 600 to 800 MB more than it keeps (the frame rate log's free memory showed it),
  in a 32-bit address space whose free room is in pieces. So a change of the scenery's rings alone no longer reloads
  (`Ui/DrawDistance.Refresh.cs`): it does what `LScape::update_loadpoint` (`0x507730`) does on entering another
  landblock, for the load point it has (the LScape's `+0x10`) and no move: the grid update `0x506E00` (thiscall:
  cell id, east and north in blocks, 1 to start from nothing; `ret 10h`), which fills empty places and remakes only
  the blocks whose `get_block_orient` changed (`0x507509`, `notify_change_size` `0x507525`, CLandBlockStruct's
  generate `0x5338F0`), then `0x5066D0` (thiscall: the LScape's `+0x14`, block changed; `ret 8`), the cells in
  sight. A block that becomes full detail gets its trees from `LScape::grab_visible_cells` (`0x505920`:
  `init_buildings` `0x505947`, `init_static_objs` `0x505989`), which nothing calls after those two: the game calls
  it when the player enters another cell (every 24 m: `CLandCell::release_cells` `0x533AE0` then
  `CLandCell::grab_visible_cells` `0x533AD0`, each a jump to the LScape at `[0x8456F0]`'s `release_visible_cells`
  `0x5059B0` and `grab_visible_cells`, no arguments). The first build didn't, so after a density change stepped back
  up to 14 the distance stayed bare (memory went up by a GB: the ground, not the trees). The second build called that
  release and grab after the refresh: trees came, then the next step crashed in `RefreshBlocks` (an
  AccessViolationException, from Decal's RenderPreUI). Now, for each full-detail block with no objects yet
  (`+0x104`), it calls the three per-block functions `grab_visible_cells` calls, each safe to ask again:
  `init_buildings` (`0x530AC0`, guarded by `+0x11C`), `init_static_objs` (`0x531780`, one argument, 0; `ret 4`;
  with objects it only refits them to the ground, `0x530C30`) and `0x5300F0` (the block's cells, once, `+0xE8`);
  the cells in sight (CEnvCell's indoor ones, `0x5301A0`) are left to the game's next change of cell. The landscape radius and Scenery
  density still reload whole: a **density change** is one whole reload with the scenery at the game's (only the
  nine near blocks' trees to make; old big and new small, not old and new big as the crashes were), then steps up
  again in place (the user's call, November 2026). **Only raising is done in place; lowering the scenery reloads
  whole.** The first density build brought the scenery down in place and reloaded the next frame: the game then
  drew next to nothing (120 to 140 fps, a black screen), the steps back up made trees for 0 blocks, and it crashed
  in `make_plane` (`0x539153`) a minute later; the same change straight after a plugin reload, with the scenery
  already at the game's, had worked. **The single reload went black too** (density 11% to 12% at scenery 13): the
  steps back up made trees for 0, 0, 0, 0, 16 and 55 blocks where a fresh landscape gave 40, 72, 104, 136, 168, 199,
  so after a whole reload down from a big radius most blocks came back already counting objects (`+0x104`), and
  were skipped. The reading (not confirmed): the game keeps blocks it has used, and a full-detail one comes back
  holding its released objects; after two reloads (a plugin reload then the density's) it was clean. So before
  stepping up the plugin counts blocks outside the full-detail ring that hold objects (`CountStaleBlocks`, by
  where they sit in the grid: the first build judged by the block's own detail and found none while 0 trees were
  made, so stale blocks still say they're full detail; never so in the
  game's own running), logs them, reloads whole once more if any, and if they're still there holds the scenery
  where it is (`StaleBlocksStopStep`, `[draw-distance] ... still hold objects` lines). **No landscape load until the
  last has settled** (`LoadSettled`: 2 s since it and a second with no frame over 100 ms; at first, from the plugin's
  start): every black screen came from two loads close together, the last after a hot reload (the old copy's reload
  at shutdown, then the new copy's for a density it took for changed), seen as "0 in full detail" at every look and a
  black screenshot; with the guard the same hot reload came through clean. A `restart` test step recovers the game. **Gently after login and portals**
  (`DrawDistanceRamp.Cautious...`): twice a login stepped scenery 1 to 3 and landscape 15 to 18 two and a half seconds
  in, and crashed in `make_plane` (`0x539153`) a second later, the world still streaming in; so no step for 8 seconds
  after logging in (LoginStatus turning 3, a hot reload too) or leaving portal space, then one scenery ring and two
  landscape rings a step until 38 seconds have gone by. That alone didn't do it (1 to 2 with 15 to 17 crashed the same way): every one of these crashes had the scenery and the landscape go up in the same step, the scenery refreshed in place and the landscape reloaded whole straight after, while either alone never crashed. So **one goes up a step, never both** (`DrawDistanceRamp.OneAtATime`: the landscape first), and the scenery a ring at a time (`SceneryStep` 1). Untested in the game when written. **Faster, with a loading bar** (the user's call: "a bad UX how long it takes to load"): the wait after login or a portal is 4 seconds, landscape steps are 3 rings even then, and while stepping up it's looked at four times a second, not once, so a step follows the last as soon as it's settled; a bar at the top of the screen (`DrawDistance.Progress.cs`, a NoInputs hud of its own) says what's loading and how far along, then Done for a moment. It also waits for the plugin's own pieces made after arriving (`Feed/LoadPieces.cs`: the far land with its pictures, and the billboards, 3D trees and far buildings, built together), at login and after every teleport, steps or not: it said Done 8 to 11 seconds before they were in (the user's eye). Each says when it's built or won't be (off, indoors, failed), and one silent for 60 s is given up on. **Teleports** (November 2026, the teleport test: round trips between Holtburg, Landscape 18, and Yaraq with `chat /telepoi Yaraq` and the overlook's `/teleloc`): taking the draw distance down in portal space (`PortalReload`, now off; `set portalreload 1` for a test) crashed one teleport in four, its reload in the same second as the game's own load of the new place (`0x58712F`, `0x539153`); dropping to 5 (`PortalLandscape`) instead of the game's 15 didn't help. Left as it is, 10 of 10 came through. `0x58712F` is the game emptying a hash table (`0x5870F0`) whose count says it holds something while every bucket is empty: every crash there came after two landscape reloads close together, so the landscape goes up in one reload (`LandscapeStep` all of it; five steps of 3 in 12 seconds crashed there with 2.4 GB free) and no reload comes within 5 seconds of the last (`LoadQuietSeconds`). **After arriving** nothing of
  the plugin's changes or loads for a while: 12 seconds after a login or the plugin starting
  (`DrawDistanceRamp.LoginQuietSeconds`; a cut 4 seconds after login, during the game's own load, crashed it), 4 after
  a teleport (`PortalQuietSeconds`; the user's call, November 2026). Timed in the game over eight teleports between
  the Holtburg overlook and Hebian-to: the game's own load shows as slow frames only entering portal space and the
  second you come out, so it's done 1 to 2 seconds after arriving; with 4 the far land came 8 s after arriving
  instead of 16 to 19, billboards 10 to 11 instead of 18 to 21, and none of the eight crashed (too few to prove it
  safe: about one long teleport in 15 crashes whatever the plugin does). The `[teleport]` line in log.txt (`Ui/TeleportTimeline.cs`) times each stage from
  entering portal space: the portal, "game settled" (the start of the first second after it with no frame over 100 ms,
  when the game's own load is done; never behind another window, where the game holds itself to 9 fps), full detail, far land and billboards; a test run's `set arrivalwait N` sets the
  wait after a teleport, to see how short it could be (the user's question, November 2026). Untested in the game when written. The memory guard learns its MB a unit from each load of 10 units or more (`megabytesPerCost`, a quarter over what was measured, between 2 and 8): its fixed 8 refused settings that fit by a gigabyte once the billboards were lean.
  Seen
  at scenery 14, 12%: 1.3 GB free in one piece of 1.15 GB, so the address space isn't in pieces, and the device
  stayed fine. Tried in the game: login to scenery 14 at 12% stepped up with four short hangs (0.3 s
  each) and no crash, free memory going down steadily with no spikes. **The flat 9 fps stretches** (110 ms every
  frame, about 35 seconds, then back) came with and without a black screen, Detail high or not: it's the game
  slowing itself behind another window (confirmed: the window went behind at the drop and came back with the frame
  rate), the player alt-tabbed to type to Claude. The Frame Rate window now logs
  (`Ui/FrameRateWindow.Device.cs`, `[device]` lines) the game's window going behind or in front, the Direct3D
  device's state when it changes (`Device.CheckCooperativeLevel`: lost, waiting to be reset), and every 10 seconds
  the address space's free room, largest free piece and pieces of 1 MB or more (`VirtualQuery`), so the next black
  screen says whether it was a lost device or a 32-bit address space in pieces. **Frame Rate**
  (`Ui/FrameRateWindow.cs`, `Feed/FrameStats.cs`, `FrameSummary.cs`; `frame-rate`, hidden to start, its switch the
  group's first row; the user's call, November 2026, "to really know if the settings are helping"): each of Decal's
  RenderFrames is a game frame, timed with a Stopwatch from login on, whether the window shows or not, two minutes
  kept. It shows the last second's rate, the last 10 seconds' average, 1% low (the 99th percentile frame time) and
  worst frame, a graph of the last minute, and the last mark's before and after (15 seconds each way, the first 3
  after left out for the reload; a frame over a second is a pause, counted and left out). Every change here marks the
  graph with the settings in a line (`DrawDistanceOptions.Summary`), and its Mark button with "Mark n". **The frame
  rate log** (`Feed/FrameRateLog.cs`; the user's call, November 2026, so Claude can read what each setting did after
  the player has tried them): a .csv a session in `Frame Rate Logs` (`<character> yyyy-MM-dd HH-mm-ss.csv`),
  written from login whether the window shows or not: a `second` row once a second (fps, 1% low, worst and average
  frame, pauses, free MB, the settings in a line) and a `change` row 18 seconds after each change or Mark
  (`FrameStats.Around`: the 15 seconds after, the reload's first 3 left out, against the 15 before, from the change
  before's settling on). To read one: the `change` rows sum up each experiment; the `second` rows show the rest. A
  fair comparison needs the player standing still facing the same way. Untested in the game when written. Found with the
  throwaway Iced disassembler, and UtilityBelt.Service's `AcClient` decompiled (ilspycmd) for
  the names and addresses. Untested in the game when written.
- **Test runs in the game** (`Ui/TestRunner.cs`, `Feed/TestScript.cs`; the user's call, November 2026, so Claude can
  check a build without the user at the keyboard): the user launches once from ShadowLauncher (in `Program Files
  (x86)\ShadowLauncher`, settings in `%LOCALAPPDATA%\ShadowLauncher`) with a default character set for the account
  and server (ShadowFilter then logs straight in) and **Auto relaunch** ticked (it starts the game again whenever a
  game it launched exits, crash or not, after a delay). Builds land by the loader's hot reload (build into `bin`).
  A script in `Test Runs\run.txt` (steps `set on|scenery|density|landscape|detail|far N`, `settle S`, `wait S`, `face H` (Decal's `FaceHeading`), `walk S` (autorun for S seconds, stopped at a hot reload too),
  `measure S`, `chat text`, `log text`, `restart`; `#` comments) runs once logged in, a step at a time; each step's
  outcome goes in `Test Runs\results.txt`, timed, with any chat line in the 2 seconds after a `chat` step (`chat
  /loc` gives what `chat /teleloc` takes to come back to the same place and facing; the plugins' `DAP2|` replies left
  out). `progress.txt` holds the step running: found at login, the game
  died on it, which is reported and the script dropped. `restart` writes the remaining steps back to `run.txt` and
  exits the game (`ClientMenu.ExitGame`), so they run after the relaunch: use it every ten or so builds (each hot
  reload stays in memory). Claude can't press ShadowLauncher's buttons: the game is set to run as administrator
  (`RUNASADMIN` on `acclient.exe`), so the launcher's window is closed to UI Automation from an ordinary process;
  ShadowLauncher takes no command line and builds the game's command line with the password itself, which Claude
  must never handle. Crashes are read from the Application event log as ever. Tried in the game: test runs,
  restarts carrying on after Auto relaunch, and with the monitor off (the game stays in front and its device fine).
  More steps: `screenshot name` (`Ui/GameScreenshot.cs`: the back buffer copied to system memory and saved as PNG in
  `Test Runs\shots`, from Decal's RenderFrame, before the game's UI; works as administrator and with the monitor
  off; Claude reads the PNGs) and `compare` (billboards: the real scenery against `SceneryLayout`, below).
  `profile <name>` puts the character on a settings profile (made with every setting as it starts if missing: "Bare"
  has no panel replaced, no borderless window, no graphics), kept for later logins, so a run ends with `profile
  Default`; `restart` after it logs in that way. `hooks 0|1` takes the appraisal and drag hooks out or puts them back.
  `flicker S` (`Ui/FlickerMeter.cs`, `Feed/FlickerStats.cs`) samples the picture's brightness in 32 by 18 tiles every
  frame for S seconds beside the steps after it (a walk), and counts blips (a tile jumping and straight back) and steps
  (a jump between steady frames): for comparing builds on the same walk, not alone.
  **The teleport crashes** (November 2026, about 400 test teleports, scripts in `Test Runs\tools`: `overnight.sh` with
  configs B-bare, D-draw, G-lighting, C-combo, Q-bare, P-panels and seeds `fresh-N`, 40 outdoor places never visited
  twice, and `repro-N`, round trips): the game crashes on long outdoor-to-outdoor teleports to places it hasn't loaded
  this session, about one in fifteen even in the Bare profile with the hooks out; never on dungeon teleports (72) or
  round trips between two loaded places (120). The sites are in the game's own loading (`AnimData::UnPack`,
  `MotionData::UnPack`, `CPolygon::make_plane`, `CCellStruct::get_portal`, a HashList emptying, `Particle::Init`,
  `CPartArray::UpdateViewerDistance_`), spread out as memory gone bad earlier would be; the same places recur (Drudge
  Camp, Drudge Tower, Mountain Retreat, Eastern Nanto Outpost). The dats unpack cleanly (every motion table, animation,
  setup, model and cell, with UtilityBelt.Common's readers; one small setup, `0x02001C57`, is 4 bytes short). On fresh
  routes, teleports a crash: Quality's graphics in the Bare profile 10 (8 of 8 routes crashed), bare 26, the panels
  alone 30, all the plugin's drawing and lighting over Scenery 1, Ground 1, Landscape 6 (C-combo) 45. Raising one of
  Quality's settings over C-combo, on four routes C-combo came through clean: Scenery 2 crashed all four (about 6),
  Ground 4 all four (about 12), Landscape 8 two (about 60). So the game's full-detail rings are what crash it, and the
  presets keep them at 1 (`GraphicsPresets`). Decal's on/off switches are per user (`HKCU\SOFTWARE\Decal`); with every
  other add-on, Content Override and Daralet Admin off, it still crashes. **The game alone with Decal crashes too, and
  the plugin isn't shown to add to it** (November 21): **minimal mode** (`Test Runs\minimal.txt`: `PluginCore.Startup`
  makes only the test runner) crashed 4 in 204 fresh teleports against the bare profile's 9 in 202 (2 in 119 against
  6 in 68 on the routes both ran); chains of the worst teleports from a fresh game (seeds `chain-N`: Standing Stone,
  Zaikal Lifestone, Khayyaban, Sawato, Undead Shrine) 1 in 50 against 3 in 49, and minimal mode plus WorldPass's hook
  alone (`set worldpasshook 2`, calling straight through) 3 in 49. That looked like the hook, so it's now only in while
  a subscriber has something to draw (`WorldPass.Add(draw, wanted)`, see Drawing inside the game's frame); but the
  test of that, the bare profile with the hook out (`F-fixbare`) against forced in (`H-barehook`), 20 chains each in
  turn, came out level: 5 in 70 against 6 in 67 at 16 each. The rate drifts over hours (7 of the first 9 chain trials
  crashed, then 26 in a row didn't), so short comparisons mislead; only long interleaved ones count.
  Same addresses in minimal mode (`0x539153`, `0x526A66`, `0x52718B`, `0x58712F`). At **Scenery 2** the chain crashes
  nearly every trial (Zaikal to Khayyaban most), hook or not, and **as often on one core** (the game pinned to one
  processor, `tools\pin-one-core.ps1`: 2 in 5 against 2 in 4), so it isn't the main thread racing the game's loading
  thread (`ThreadedCache`, seen in the dumps' thread lists: its worker loop at `0x678960` runs the cache's requests,
  the main thread finishes them off for up to 25 ms a frame at `0x678A10`; disk reads are already locked,
  `CThreadsafeDiskController::LoadDataEx`); more likely memory written past an end or after a free, landing wherever
  the plugin's own memory leaves room. **Page heap can't find it while Decal is loaded**: full and light page heap
  alike (`tools\pageheap-on.ps1`, `pageheap-light.ps1`, `pageheap-off.ps1`, run as administrator) stop every game 12 s
  in at a harmless one-byte read past a picture's buffer in Decal's `Inject.dll` (`+0x30695`: a 24-bit image loop
  reading 4 bytes a pixel). Windows Error Reporting is off on the test machine, so no dumps are kept; the plugin's
  test-only `Ui/CrashDumpWriter.cs` (`Test Runs\dumps.txt`) writes one from the last-chance filter or .NET's
  UnhandledException, though it caught nothing of Decal's crash. **Full dumps** (ProcDump, `tools\debug-service.ps1`
  run as administrator in procdump mode, switched to it by `tools\pd-run.sh` only after login: a debugger attached at
  launch, ProcDump or TTD's `-monitor`, keeps Decal out and the game sits at the login screen): the crash is in a dat
  file's unpack under `AsyncCache::BlockingLoadInto` (`CEnvCell::UnPack` → `DBObj::Get` → ... →
  `CEnvironment::UnPack` → `CCellStruct::UnPack` → `CPolygon::UnPack` → `make_plane`; or `CMotionTable::UnPack` →
  `MotionData::UnPack` → `AnimData::UnPack`), and the data it reads is wrong in memory: Environment `0x0D0003CF`'s
  buffer matched the dat byte for byte but **one dword zeroed** (offset `0xA3C`), the motion table's read ran into a
  freed, zeroed region. So something writes through a stale pointer into memory just reused as a load buffer, or frees
  it mid-unpack. Read a dump with `cdbX64.exe -z`, `.load wow64exts`, `!sw`, `r`, `dps esp` (`.ecxr` is WOW64's 64-bit
  record); `tools\pd-analyse.sh` does it. **Time Travel Debugging can't record it on the live server**: recorded whole
  the game is too slow and the server drops it mid-teleport; `-module` on an attach records nothing (it starts only on
  entering the module). **Full page heap can't work for this game at all**: backwards (`PageHeapFlags` 0x13, the guard
  before each block; the helper sets it from `Test Runs\pageheap.txt`) lets Decal's over-read pass, but every game then
  stopped in ImGui's font picture (`cimgui.dll+0x1FDF72`, `GetTexDataAsRGBA32`'s loop) or hung behind an ImGui error
  box. That's page heap's own cost: logged once a second from the plugin's start (`Test Runs\memory-watch.txt`,
  `CrashDumpWriter.WatchMemory`, from a thread of its own), the 32-bit address space went from 2,027 MB free to 12 MB,
  no free piece of 1 MB, in 4 seconds, so ImGui's allocation came back empty. `Test Runs\hold-for-debugger.txt`
  (`CrashDumpWriter.HoldForDebugger`) holds the plugin's start until a debugger attaches, for ProcDump to be in before
  something that comes seconds after the plugin starts. What's left: page heap on only some allocations (`/size`,
  `/random`), though the game's cache may use its own allocator (`!heap` didn't know the freed region), or a Time
  Travel recording on a local server whose timeouts let a slowed game through.
- **Billboards, stage 1: where far trees would be** (`Feed/SceneryLayout.cs`, `SceneryObject.cs`, `PlacedScenery.cs`,
  `Ui/DrawDistance.Compare.cs`; the user's call, November 2026: real scenery to 4-5 landblocks, billboards beyond).
  Past the scenery radius the game makes no objects, so places come from the dats by the client's rules
  (`CLandBlock::get_land_scenes`, `0x5311A0`, as daralet-ACE's `Entity/Scenery.cs` has them): per terrain vertex,
  terrain and scene type pick the region's (`0x13000000`) scenes, hashes (32-bit unsigned, wrapping) pick a scene and
  each object's chance, place, turn and size; kept inside the block, off roads (ACE's simple road bits), out of cells
  with a building (`CSortCell::has_building`, `0x534D40`; here the cells of buildings' origins in `LandblockInfo`)
  and on ground of the slope it takes (`ObjectDesc::CheckSlope`, `0x52FDD0`: the ground triangle's normal's upward
  part between MinSlope `+0x5C` and MaxSlope `+0x60`); height from the block's height grid, each cell split along
  `LandblockMesh.GetSplitDir`'s diagonal (`Plane::set_height`, `0x52FD90`, in the client); Scenery density's rule
  applied. Checked in the game against the real objects (`compare`: a full-detail block's objects at `+0x108`, count
  `+0x104`, the dat's placed ones first, as many as its LandblockInfo (`+0x100`) has at `+0x38`; positions at
  `+0x48`, origin `+0x3C`): 121 blocks, 13,037 real, 12,942 predicted, 90.5% within 1 m, heights exact. The slope
  and building rules brought the count from 3% over to 1% under; the 10% unmatched are placed in both, more than
  1 m apart, which billboards far off won't show.
- **Fog** (`Ui/DrawDistance.Fog.cs`, the Draw Distance page's Fog slider, which works with Draw Distance off too,
  `DrawDistanceOptions.Fog`, `fog`, 100 to
  start, 10 at least; the test step `set fog N`; the user's call, November 2026): the game's distance fog thinned. It's
  Direct3D's linear fog, start to end distance, which the game works out each frame from the sky in its landscape
  update and hands to `RenderDeviceD3D::SetFFFogProperties_` (`0x5A41B0`, thiscall: colour, start, end; it sets
  FOGCOLOR, FOGSTART and FOGEND only when they change) from one call, `0x5066BB`. That call is hooked
  (`AcClient.Call.RenderDeviceD3D__SetFFFogProperties_0x005066BB`) while below 100%, and both distances stretched by
  100 over the percent; the colour and the game's own Disable Distance Fog (PlayerOption 48) are left alone. Seen in
  the game: the game's 150 to 2400 m at midday; at 100% the far hills were grey silhouettes and the billboards gone in
  them, at 50% the forest showed through haze, at 25% (600 to 9600 m) clear to the horizon, 51 fps.
- **Shading from depth** (`Ui/DepthShading.cs`, `Shaders/*.hlsl`, `Feed/ShadingOptions.cs`, `SettingsData.Shading`,
  `shading`, off to start; rows Shading and Occlusion under Draw Distance's, `GraphicsPanel.Shading.cs`; test
  steps `set shading|occlusion|depthview`; the user's call, November 2026: realtime lighting and shadows, the
  depth route before real sun shadows). Ambient occlusion and short contact shadows away from the sun, one full-screen
  pass on Decal's RenderFrame (after the world and the billboards, before the interface), its grey multiplied onto the
  picture. The game's device (logged once, `[shading] device`): depth D24S8 at the screen's size, no multisampling,
  shader model 3, and the driver offers INTZ, a depth-stencil texture a shader can read. So one of those, the game's
  depth buffer's size, is bound in its place (SetDepthStencilSurface) at the end of each RenderFrame, the game's next
  frame draws its world into it, and the pass puts the game's back while it samples ours (and ours again after). A
  frame where the game had bound something else since gets no pass, counted every 30 seconds. **Device resets**:
  anything in the default pool still alive makes Reset fail and the game can't draw again; UtilityBelt calls every
  hud's `OnDestroyTextures` just before one (`HudManager.PreReset`, from Decal's `IDecalRender.PreReset`), so a hidden
  hud of its own lets the texture and its hold on the game's buffer go there, and it's all made again the next frame.
  Shaders are compiled ahead by `Shaders/build.cmd` (the Windows SDK's fxc) into `Resources/shaders/*.cso`, checked
  in and embedded, and made and set through the device's own vtable (CreateVertexShader 91, SetVertexShader 92,
  SetVertexShaderConstantF 94, CreatePixelShader 106, SetPixelShader 107, SetPixelShaderConstantF 109): Managed
  DirectX wants its compiler for them. The camera is the device's (`Transform.Projection`, left-handed, near 0.1:
  M33 1.00003, M43 -0.1; a depth d is z = M43 / (d - M33)); positions are rebuilt from the depth and normals from
  their screen derivatives. Faced toward the camera by the line of sight, not the view's axis: the first build flipped
  ground seen at a low angle and darkened the whole slope in front of you (`depthview 3` shows normals as colours;
  `1` the depth, `2` the shading alone in grey). Off-screen samples don't count, the depth step allowance grows with
  distance, samples are lifted off the surface, and an occluder much nearer than the surface counts for little (the
  character left a dark halo on the ground behind them). Contact shadows marched 3 m toward the sun in 16 steps
  (the Shadows row, `shadows`); **taken out** in November 2026 (the user's call), Sun shadows doing it properly; the
  saved value is left unread. Seen in the game: the
  character's feet grounded with soft occlusion and a short shadow away from the sun; about 1.6 fps at 2560x1440.
  Trees' shadows are longer than 3 m: those want real sun shadows (a shadow map), the next step if this isn't enough.
  **No noise** (`Ui/DepthShading.ShadeBlur.cs`, `Shaders/shadeblur.ps.hlsl`): the occlusion turned its 12 samples a
  random way at every pixel (a hash of the pixel) and nothing smoothed them, and characters, whose own bodies close in
  much of its 1.2 m reach, came out covered in dots that stayed on the screen as they moved (the user's screenshots,
  November 2026; tried in the game: gone with Occlusion at 0, there with lamp shadows off). Now each pixel's turn is one
  of 16 by its place in a 4 by 4 block (`Interleaved`, Bayer order; the sun shadows' disc too), the pass draws into a
  texture of its own, and a 4 by 4 average that leaves out neighbours at another depth (a character against the wall
  behind) multiplies it onto the picture; the debug views still draw straight onto it. Untested in the game when
  written.
- **Water reflections** (`Ui/DepthShading.Water.cs`, `Shaders/water.ps.hlsl`, `Feed/WaterMap.cs`,
  `Feed/WaterOptions.cs`, `SettingsData.Water`, `water`, off to start; the Horizon window's Water section,
  `GraphicsPanel.Water.cs`; test steps `set water|waterstrength|ripples|ripplesize|
  fresnel|glints|blur|softshore`; the user's call, November 2026: screen-space reflections, every piece its own switch
  to mix and match). Built on the depth shading (its depth texture is kept while either is on; the water pass comes
  first, so the shading darkens its shores too). Asheron's Call's water is ground with a water terrain type, types
  0x10 to 0x14 by vertex (the client's SurfChar, as daralet-ACE's `LandblockStruct.CalcCellWater`), and always flat:
  a map of water vertices 4 landblocks round you (`WaterMap.Build`, an L8 texture, made again on crossing a block),
  filtered between vertices, says which pixels are water, and its gradient across a shore cell is the soft shoreline.
  Only surfaces facing up count, so things standing in water don't reflect. The frame's colours are copied to a
  render-target texture (StretchRect, default pool, let go before a reset with the depth texture), the reflected ray
  marched through the depth in view space (40 growing steps, a binary search at the hit, 150 m at most, fading at
  the screen's edges), missing rays the sky colour (the device's fog colour, which the game sets to the sky's).
  Flat water reads as glass, so the surface the reflection uses is faked: **Ripples** (four waves at odd angles, their
  size a slider), **Fresnel** (Schlick's, water's 2%, lifted 1.6 times or water seen from a hill shows nothing), **Sun
  glints** (the reflected ray against the sun), **Distance blur** (four taps spread by how far the reflected point is),
  **Soft shores**, all blended over the game's own water by **Strength** (35% to start).
  Tried from the Holtburg overlook (November 2026): at 60%, with ripples a meter or so long and Fresnel lifted 1.6
  times, the pale sky washed out the game's blue and the far ripples made rings of moire. So each wave is let go of
  where it's under 12 pixels on screen (`RippleSlope`'s `metersPerPixel`, slanted by the view), the waves are
  1.7 to 6.3 m and gentler, the reflection takes the hue of the water under it half way, Fresnel's lift is 1.3,
  and Strength starts at 35%. The large river by Cragstone, from the hill above town, is the other place to tune it.
  The reflection fades out from 450 to 740 m from the camera, inside the water map's 4 landblocks: it used to stop at
  the map's edge, a step in the water's tone far off (seen in the game, November 2026).
  **Hits must stand above the water** (`water.ps.hlsl`, the march): looking across a river from its bank, the rays
  skimmed the water and the march took the water surface further out for a hit, reflecting the game's dark raw water:
  a hard dark band along the far bank and dark dashes across the far water (the user's eye, November 2026; told from
  the plugin's land and the soft shores by shots with each off). A hit now counts only where the scene there is 0.4 m
  over the water's height. And far off the reflection reaches nearer the shore (`shoreFrom` 0.45 near you to 0.2 past
  300 m), so the game's raw water left before each shore is narrower.
- **Image: anti-aliasing and sharpening** (`Ui/DepthShading.Image.cs`, `Shaders/antialias.ps.hlsl`,
  `Shaders/sharpen.ps.hlsl`, `Feed/ImageOptions.cs`, `SettingsData.Image`, `image`, off to start; the Horizon window's
  Image section, `GraphicsPanel.Image.cs`; test steps `set antialias|sharpen`; the user's call, November 2026). The
  first of the "fidelity" features: **the guiding rule for all graphics work is to show Asheron's Call's own art
  better, never to add effects for their own sake** (the user's call: keep the artistic details, enhance the world the
  creators made); every feature takes its light and colours from the game, is subtle, and is its own switch, off to
  start. Anti-aliasing is FXAA (after FXAA 3.11, quality preset 12's search, faint texture contrast left alone) and
  sharpening AMD's contrast-adaptive CAS, each over a fresh copy of the frame (`CopyFrame`, the water's render
  target), last of the passes and before the interface, so the UI and the plugin's windows stay as they are. Neither
  needs depth, so with only these on the game's depth buffer is left in place (`Frame`'s `needsDepth`,
  `TearDownDepth`). Shaders are built by `Shaders/build.cmd`'s loop (`\a` in a printf'd path became a bell
  character once: edit the script, don't append to it from bash). Untested in the game when written. The ideas
  judged doubtful or left out (colour grading, real sun shadows, bloom, god rays, depth of field) are to be tried as
  experiments, each off by default, sunlight first (the user's call).
- **Grass and reeds** (`Ui/Grass.cs`, `Ui/Grass.Chunks.cs`, `Ui/Grass.Buildings.cs`, `Ui/GrassChunk.cs`,
  `Feed/GrassLayout.cs`, `Feed/GrassPicture.cs`, `Feed/GrassAtlas.cs`, `Feed/GrassClump.cs`, `Feed/GrassOptions.cs`,
  `Shaders/grass.vs.hlsl` and `grass.ps.hlsl`, `SettingsData.Grass`, `grass`, off to start; the Horizon window's Grass
  section, `GraphicsPanel.Grass.cs`: Density, Distance 15 to 200 m, Grass height, Reed height, Reeds on marsh, Wind;
  test steps `set grass|grassdensity|grassdistance|grassheight|reedheight|reeds|wind`; the user's call, November 2026:
  "frill", grass growing out of Asheron's Call's own ground). On the grass terrains only (Lush Grass fullest,
  Grassland, Patchy Grassland, and Daralet's own 0x15 and 0x1C, a grass texture and Patchy Grassland's: left out,
  whole meadows were bare, the user's screenshot), reeds on Marsh (the user's call); never on a road, under a building or on a slope
  steeper than about 40 degrees.
  **Roads** are the client's own test (`SceneryLayout.OnRoadExact`, `CLandBlock::on_road` as daralet-ACE's physics
  `Landblock.OnRoad` has it: a cell's four corners' road bits, a road 5 m wide from each marked corner and edge);
  ACE's simple road bits, which the scenery uses, left grass on the road's edges. **Buildings** are their model's box
  across the ground, turned and placed as the building is, half a meter more (`Grass.Buildings.cs`, LandblockInfo's
  Buildings, the model's vertices, a Setup's parts by their first frame): the game's rule for trees, no cell with a
  building's origin, left bare 24 m squares round every house in Hebian-to (the user's screenshot, November 2026).
  **Reeds come in a mix** (`GrassLayout.ReedScale`: 45% short, 30% middling, 25% tall, wildness adding only a
  tenth; Reed height its own slider, 80% to start): all tall, and over 2 m in the wilds, they were a wall you couldn't
  see a character through (the user's call, November 2026). But in **stands** (`GrassLayout.ReedPatch`, smooth noise
  over 8 m, high in about a tenth of a marsh) they grow tall and thick, 1 to 1.35 times the old full height, so some
  still rise well above the grass (the user's call). A jittered 0.6 m grid fixed to each landblock, kept by hash under the terrain's share and the
  density, so a clump is always in the same place; heights from the full-detail ground. Each clump two crossed
  pictures. The pictures are made in
  code, near white and darker at the base, and **coloured by the ground they grow from**: each grass terrain's own
  texture (the region's `TerrainInfo.LandSurfaces.TexMerge.TerrainDesc`, `TerrainTex.TexGID`) averaged, `[grass]`
  lines in log.txt giving each. Lit as the ground is (the ambient plus the sun on ground facing up, both times 1.15, `Grass.GroundLight`): lit with the billboards' share of the sun, it came out near black against the ground (seen in the game, November 2026); the blades' bases are 72% of their tips. The vertex shader
  sways the tops (the second texture coordinate: how far up, and a phase) and sinks clumps into the ground over the
  last 30% of the radius, the pixel shader thinning them by the same fade against its cut-out, so nothing pops. Drawn
  with the billboards, inside the game's frame (`Ui/WorldPass.cs`), so before the depth shading, which runs on
  RenderFrame, and the occlusion darkens their bases. The device's shader calls are `Ui/DeviceCalls.cs`.
  Untested in the game when written.
  **Kept near towns** (`GrassOptions.KeptNearTowns`, on to start, test step `set keptnear`; `Feed/Wildness.cs`; the
  user's call, November 2026: short, well-kept grass near towns, taller and denser in the wilds): each clump's
  wildness, 0 to 1, is a smooth step on its distance to the nearest town (kept within 250 m, wild past 700) or outpost
  (90 and 350) in the map's places (`locations.xml`, as the World tab reads it, map units back to world meters,
  (EW + 102) * 240). Kept ground takes 60% of the grid's squares and grows 0.55 times as tall and even; wild ground
  takes all of them and grows 1.35 times as tall and uneven; between, a blend, so no line shows where a town's lawn
  ends. Kept clumps (wildness under a half) are drawn with their own pictures, upright and even as mown, wild ones
  with leaning, uneven blades.
  **Made a chunk at a time, thinned far off** (`Ui/Grass.Chunks.cs`, `Ui/GrassChunk.cs`; the user, November 2026:
  "grass is causing heavy fps loss"). Measured on the dats (a throwaway benchmark running `GrassLayout` round a
  place): at 80 m and 100% density, 18,300 clumps at the Holtburg overlook and 31,000 to 44,000 in the wilds, up to
  177,000 cut-out triangles a frame, four in five past 40 m where a clump is a few pixels; and every 3 m you moved all
  of it was laid out again (about 30 ms), copied through three lists and uploaded as one new 16 MB buffer, about 35 MB
  of garbage each time, about once a second at a run. Now each of a landblock's 24 m cells is a chunk of its own
  (`GrassLayout.PlaceCell`, the same clumps as `Place`, a test checks), made once within 2 ms a frame, nearest first,
  when it comes within the radius (about 0.8 ms a chunk), and let go 24 m past it; its clumps are sorted by a rank
  fixed to each (`GrassClump.Rank`), so far off only a share from the start is drawn, every clump to 20 m and
  (20 / distance) to the 1.2 past it, a fifth at 75 m (`ThinFrom`, `ThinPower`); the vertex shader sinks the clumps
  near the share's edge, their rank in the colour's alpha, so none pops. Chunks out of view aren't drawn (`InView`,
  the view's pyramid), and the rest go nearest first, so near grass hides what's behind it. A clump is 8 vertices
  from one shared index buffer, not 12, and a chunk's offset is a shader constant (c8), so chunks outlive a change of
  landblock. In the wilds about 30,000 clumps are sent and about 15,000 put down pixels, before culling. The
  pictures were in the default pool and nothing let them go before a device reset; the atlas, index and vertex
  buffers are managed now. `[grass]` every 10 s says the chunks kept and drawn a frame. **More pictures**
  (`Feed/GrassAtlas.cs`; the user's call): 6 wild tufts (dense and leaning, thin and tall, short and full,
  wide-bladed, sparse, low and tangled), 3 gone to seed or with a few blossoms (15% of wild clumps), 3 kept and 2
  reeds, by each clump's hash (`GrassClump.Variety`), all near white so the ground colours them: seed heads and
  blossoms are a warm near white, a pale tint of the ground's own colour. One 256 atlas, each picture's mips made
  alone (`MipChain`) and its blades kept off its edges, so a far clump takes in none of its neighbour's. Tried in the
  game (Hebian-to's square, running round it): with the old grass about 30 fps (the user's count), with the new 65
  to 76, against 70 to 74 with it off; 11 to 13 chunks and about 4,400 clumps drawn a frame, the 64 round you made
  in 38 ms all told. Standing still the old grass had cost nothing to see (62.7 against 63.1 fps): its cost was the
  rebuilds as you moved.
  **Out to 200 m** (the user's call): a chunk keeps only the share of its clumps it would draw a cell nearer
  (`GrassChunk.KeptShare`, `KeepShareAt`) and is made again with more as you come near, so far chunks hold a few
  hundred clumps each; the blocks round you are read once (`lands`, 30 at most). Untested in the game when written.
- **Billboards, stages 2 and 3: pictures and drawing** (`Feed/TextureDecode.cs`, `Feed/ImpostorRaster.cs`,
  `Ui/BillboardModels.cs`, `Ui/BillboardModel.cs`, `Ui/DrawDistance.Billboards.cs`; Billboards in the Draw Distance
  rows, `DrawDistanceOptions.Billboards`, `billboards`, off to start; the test step `set billboards 1`). Each scenery
  model is drawn side on once by a small software renderer (a Setup's parts by its first placement frame and default
  scales; every colour format a dat can hold decoded, `TextureDecode.Decode`, so a season's new trees draw too: Daralet
  changes season by a new dat set, and nothing of the billboards is kept between sessions, so they follow it; clip-map surfaces cut out below half alpha) into a 128-pixel-tall
  A8R8G8B8 texture (managed pool); models under 2 m (the ground scatter) get none. **Drawn on a worker** (November
  2026: 30 to 170 ms a model on the game's thread, a hitch whenever new trees came into view): the dats are read on
  the game's thread, the decoding, drawing and smaller copies on up to two pool threads (`BillboardModels.StartDrawing`),
  and `Collect` makes the textures, 3 ms a frame; the placing isn't done until `Pending` is 0, so the old billboards
  stay drawn till the new come in whole. Untested in the game when written. Every block within the landscape
  radius outside the full-detail ring has its scenery worked out by `SceneryLayout` (4 ms a frame, `WorkOutBudgetMs`: it was 6 blocks a frame, and a block takes only 0.05 to 0.07 ms, measured on the dats round Hebian-to, so 3,300 blocks waited about 9 seconds for 0.2 seconds of work; a disk cache of the worked-out blocks, the first plan, would have read about as slowly as the work it saved, November 2026), and each object
  becomes two crossed upright quads turned by its yaw and sized by its scale, in one vertex buffer grouped by model,
  made again when you cross a landblock or a setting changes. Drawn inside the game's frame (`Ui/WorldPass.cs`, below), after the solid world and
  before its see-through things (on Decal's RenderFrame if that can't be hooked), so the world's depth buffer hides them behind hills: world identity, the device's own View and
  Projection (skipped while `CombatProjection.IsSwapped`), depth test and write, alpha test at 128, no blending,
  lighting or culling, the device's state captured and put back with a StateBlock. Not indoors, in portal space, or in
  a frame after you've crossed a landblock until it's rebuilt. `CustomVertex.PositionTextured.Format` can't be compiled
  without Microsoft.VisualC (a C++ const), so the format is written out. Tried in the game (scenery 5 at 30%,
  landscape 15): 17,959 billboards of 25 models, 71,836 triangles, built in about a second, 115 MB, and no frame rate
  cost (63 fps before, 65 after); the far ridges had trees, hidden behind the near hill. At landscape 24: 47,346
  billboards, 62 fps. **They stand on the ground the game draws**, not the true ground: past the full-detail ring the
  game draws a block from every 2nd, then 4th, then 8th vertex (`SceneryLayout.DetailStep`, get_block_orient's
  rings), so a far block is two triangles and trees on a hump floated over the flattened ridge (seen in the game).
  `SceneryLayout.Ground(..., step)` takes those vertices, each coarse cell split by `SplitNwSe` of its coarse index
  (`CLandBlockStruct::ConstructPolygons`, `0x532A50`, adds the cell's own index to the block's times 8 at any
  detail). **The game's distance fog covers them** (Disable Distance Fog, option 48, off): the device's fog states are
  left as the game set them, and they fade with the hills (seen in the game). **Lit as the game lights outdoors**
  (`Ui/DrawDistance.Lighting.cs`): the device's light 0 has no direction left in it by RenderFrame (read with
  IDirect3DDevice9::GetLight; Managed DirectX's `Lights[]` only echoes what it set), so lit by it they got the
  ambient alone and went nearly black. The client's own outdoor light is in globals: the ambient
  (`SmartBox::SetWorldAmbientLight`, colour times level, `0x8682B0`), the sun's colour (`0x8682BC`) and its vector
  toward the sun (`0x8682C8`, x east, y north, z up; LScape's copy at `0x842950`), worked out from the sky
  (`SkyDesc::GetLighting`); `LScape::calc_object_light` (`0x455850`) is the ambient level plus the sun vector's length
  times 0.2. The billboards are drawn with that ambient and the sun on light 0 (put back by the state block), a white
  material, normals up, and a share of the sun by its strength, not its height (`SunShare` times the sun vector's
  length, as `calc_object_light` goes by it: at dusk, lit by its height, they came out a tenth darker than the real
  trees, which darkened only with the ambient; matched again after). The share: measured against real trees in the same places (the mean
  foliage colour in two screenshots), which take about 0.37 by brightness, lowered for their bluer tone, which
  showed at the seam. **Mipmaps** (`Feed/MipChain.cs`): far billboards sparkled; each picture now has its halved
  copies, colours weighted by alpha, each level's alpha scaled so as many texels pass the alpha test as in the full
  picture (averaged alone, a cut-out thins to nothing far off). Seen in the game: real trees to 3 at 100% density,
  billboards to 24, 54 fps, the seam hard to find. **Density
  matters little to them**: at 100% density with real scenery held at the game's 1 (the memory guard refused 5,
  896 MB short), 170,037 billboards from ring 2 to 24 ran at 142 fps, where real trees to 5 at 60% ran at 33 (seen
  in the game, November 2026): the real scenery is the cost, the billboards almost none. Not yet: a handover distance
  from real trees, density of their own, and a rebuild for each step of the scenery
  stepping up. Test steps `set billboards 0|1` and `option <PlayerOption> 0|1` (a game option, through
  `ClientOptions.Set`); a hot reload mid-run hands the steps left to the next copy (`TestRunner.Dispose`): before, the
  new copy took the leftover progress.txt for a crash and dropped the script.
- **3D far trees** (`Ui/DrawDistance.FarTrees.cs`, `Ui/FarTreeModels.cs`, `FarTreeModel.cs`, `FarTreePart.cs`,
  `FarTreeInstance.cs`, `Shaders/fartree.vs.hlsl`, `fartree.ps.hlsl`; `DrawDistanceOptions.FarTrees`, `farTrees`,
  rings past Scenery, 0 to start, 8 at most; the 3D trees row; test step `set fartrees N`; the user's call,
  November 2026). The game's real trees cost about a microsecond a draw call, each part its own; these are the same
  objects as the billboards (SceneryLayout, stood on the ground as drawn) as the game's own models, read from the
  dats (a Setup's parts by its first placement frame, polygons fanned, a part per surface, textures through
  `BillboardModels.SurfacePixels` with mips), one managed vertex (position, texture place, 20 bytes) and index
  buffer a model, and every tree a 32-byte instance (place, height, size, cos and sin of its turn, as the billboards
  turn them) in one buffer, drawn with hardware instancing (stream 0 `0x40000000 | copies`, stream 1 `0x80000000 |
  1`, through the device's SetStreamSourceFreq, vtable 102, `DeviceCalls.StreamFrequency`), one DrawIndexedPrimitive
  a model and texture, in the game's frame (WorldPass) before the billboards. The shaders place, light (the
  billboards' light: ambient plus the sun's share by strength), cut out and fog (the game's linear fog, which
  shaders get none of: its start, end and colour read from the device). `VertexElement.VertexDeclarationEnd` needs
  Microsoft.VisualC to compile, so the end is written out. **Textures on a worker** (`FarTreeModels.TextureFor`,
  `FarTreeTexture`, shared by surface; November 2026: a teleport into a forest spent 2.5 s of the game's thread on
  them over 527 models, up to 349 ms a model, timed in the `[far-trees] model` lines): the dats are read on the game's
  thread, the decoding and smaller copies done on up to two pool threads, `Collect` uploading 3 ms a frame; the
  placing waits for `Pending` as for the billboards, and a part with no texture isn't drawn. The geometry and its
  buffers are still made on the game's thread (0.8 s over those 527, up to 96 ms a model). Untested in the game when
  written. Seen in the game at the plateau overlook: real trees to 3
  landblocks 52 fps; real to 2 with billboards 98; real to 2 with 3D trees on ring 3 (1,322 trees) 103, rings 3 to
  6 (8,028 trees, 1.4 million triangles) 104; the 3D ring looked all but the same as the real one.
- **Budget and presets** (`Ui/GraphicsPanel.Budget.cs`, `Feed/GraphicsCost.cs`, `Feed/GraphicsPresets.cs`; the user's call,
  November 2026: a cost beside each setting and two budget bars, presets, and the Horizon window regrouped: Budget, View
  Distance (Landscape, Ground detail, Fog), Trees and Scenery (Scenery, density, 3D trees, billboards, far buildings),
  Model Detail, then Lighting, Water, Grass, Image). `GraphicsCost` is each setting's estimate over the game's own, in
  MB and ms, from the rates measured: Landscape 1 MB a block, Ground detail 1.8 MB and 0.013 ms a block, Scenery 8 MB
  and 0.2 ms a block at full density (a forest's: a desert took far less), the screen passes rough (`Rough`, shown with
  "~"). Each slider shows its cost for the value held, before anything loads (`Previewed`). The bars: memory against
  what's free plus what these settings hold, less 800 MB spare; frame rate, the last 5 seconds' frame time less the
  estimate for what's on (the game's own cost where you stand) plus the estimate for what's shown, against a target
  (`GraphicsTargetFps`, 60, 100 or 144); both taken only while the draw distance is loaded (`AtTarget`). Presets
  (Scenery and Ground detail always the game's own 1: raised, either alone crashed teleports far sooner, see The
  teleport crashes): Retail (off), Performance (Landscape 5, far land 19, 3D trees 2 rings), Balanced (6, 18, 3),
  Quality (8, 24, 4, and the takeover of the game's coarse rings), each with billboards and far buildings; lit when
  the settings are one, else Custom. Those are the **Landscape** presets, at the top of View Distance; the **Lighting**
  presets (`Feed/LightingPresets.cs`, the same four names) are at the top of Lighting, setting what costs: shading,
  sun shadows and lamp light on (Retail: all off), occlusion 40/60/70, sun shadows to 120/250/400 m, lamps lit to
  100/200/300 m with the 4/8/12 nearest shadowed, leaving the look (darkness, softness, brightness, reach, night)
  alone; and the **overall** row at the top, under Budget, sets both of a name, lit while both are it (the user's call,
  November 2026). The lighting's costs scale with its settings (the user's call, November 2026; they were on or
  off, a fixed guess each): sun shadows 0.8 ms plus 0.6 ms each 150 m of Distance (1.4 ms at 150 m, the one measured),
  lamp light 0.2 ms, plus 0.015 ms a lamp lit (Distance over 10 m, up to 32), plus with shadows 0.3 ms for the maps'
  redraws and 0.06 ms each shadowed lamp (no more than the card's shader has, `LampCost`); all but the 150 m sun figure
  rough ("~"), shown beside those sliders too. The presets come to about 2.5, 3.4 and 4.4 ms of lighting. **Changes wait for Apply** (`GraphicsPanel.Draft.cs`; the user's call: try settings
  until the estimate suits, then apply): the rows edit copies (`DrawDistanceSettings`, `ShadingSettings`...), made
  afresh each frame while nothing's changed; Apply copies their fields into the settings' own objects and does what a
  change did (`Commit`, which the test runner calls directly); Revert drops them. The bars price the copies against
  what's in use. Untested in the game when written.
- **Ground detail** (`Ui/DrawDistance.Ground.cs`, `DrawDistanceOptions.GroundRadius`, `groundRadius`, 0 for with Scenery;
  the row under Scenery; test step `set ground N`; the user's call, November 2026: "roads are drawn from the Scenery
  setting too"). Past the full-detail ring the game draws a block's ground from every 2nd, 4th or 8th vertex, and
  roads, marked per vertex, break up; the far water smears the same way. So the rings are patched out to Ground
  detail (`patchedRadius`), and past Scenery (`treeRadius`, from the load point, the LScape's `+0x10`, the LScape at
  `[0x8456F0]`) the road check that Scenery density hooks says every tree is on a road, so the game makes none;
  buildings and the dats' placed objects come as ever. Blocks left without trees are noted (`skippedBlocks`, by
  0xXXYY) and get billboards and 3D trees (`TreeRing`); one walked toward, now within Scenery, gets the game's trees
  from `get_land_scenes` (`0x5311A0`, thiscall, nothing else; only while the scenery flag `[0x81EF08]` is on) then
  `init_static_objs` (0, its with-objects path, which fits them to the ground and puts them in their cells), six
  blocks a look (`GrowTrees`). One walked away from keeps its trees and isn't noted, so nothing doubles it. Far
  buildings start past the full-detail ring, where the game's stop. Tried in the game (a desert overlook, Scenery 4):
  Ground 4 to 8 stepped up in 12 s, the road ran on to the horizon, 132 fps to 91 and 336 MB (`GroundBlockCost`
  0.2). Walking toward far blocks untested when written. `[ground]` lines in log.txt.
- **Memory for teleports** (November 2026, the test suite below): a teleport loads a whole new place while the old is
  still held, so the memory guard keeps 1.4 GB free (`HeadroomMegabytes`, `RaiseMemoryMegabytes`, and the budget bar's
  `SpareMb`): 768 MB let a teleport from Holtburg (Scenery 2, Ground 6, Landscape 18, 1.1 GB free) run the game "OUT OF
  MEMORY" (its own error box), and 1.1 GB crashed one from Drudge Temple in `make_plane`. In portal space the far
  scenery is let go (`ReleaseFarScenery`: buffers, every block's worked-out scenery, a full collection) and the
  full-detail rings go back to the game's 1 (`PortalRings`: left at the presets' 2 for the load, nearly every
  teleport between Holtburg and Hebian-to crashed, 0x58712F, November 2026), a code patch with no reload (out of a dungeon nothing outdoors is loaded,
  and with the rings at 6 that cold load crashed the game in its unpacking, `0x526A8A`, with 2.5 GB free); they step
  back up in place once you're there, nothing stepping while in portal space (`inPortalSpace`). The guard learns its MB
  a unit up to 16 (`MaxLearnedPerCost`: the rings at Drudge Temple took 12 to 15) and remembers the costliest settings
  loaded since the last teleport (`highestCostHere`): lowering the draw distance frees nothing (the game keeps what it
  read), so going back up to them is let through. (Presets then: Performance 1/6/15, Balanced 2/6/16, Quality 2/7/16;
  since cut to Scenery and Ground 1, see Budget and presets.)
  The game alone, at its own Landscape 15 at Holtburg, uses about 2.6 GB of its 4 GB: memory, not the plugin's extras
  (billboards about 25 MB, 3D trees about 1, far buildings 4, the screen passes none measurable), is what limits the
  presets, which is what plugin-drawn far land (SESSION-HANDOFF.md) is for.
  **Models no longer in sight are let go** (`BillboardModels.KeepOnly`, `FarTreeModels.KeepOnly`, after each build from
  `usedModels`): every model ever seen was kept, in Direct3D's managed pool outside the plugin's heap, and the game's
  free memory fell 60 to 100 MB every six teleports; after, a leak loop of 8 round trips held flat (1,867 to 1,850 MB,
  the plugin's heap 117 MB throughout). **The game's cache** (`Ui/GameCache.cs`): `DBCache::FlushFreeObjects`
  (`0x4144E0`, thiscall on `[0x837BAC]`, 0 for every object cache in its table at `0x81860C`; the game calls it so
  itself at `0x411BD0`) is called once loading has settled after a teleport, the draw distance lowered, or memory low
  (`MaybeFlushCache`); in every test it gave back 0 MB, so whether the free lists are empty or the call misses them is
  open. `0x58712F`, the earlier crash, is `0x5870F0` emptying a hash table inside that cache code (`0x415098`).
  **Video memory only, always** (`Ui/ScenePool.cs`, `DrawDistance.VideoMemory.cs`; a setting of its own,
  `videoMemory`, until November 2026, then always on, the user's call, the saved value left unread): the billboards' and 3D trees' buffers in the
  default pool and their pictures filled in system memory then copied across (`device.UpdateTexture`), let go before a
  device reset (a hidden hud's `OnDestroyTextures`) and made again; tried in the game: switched both ways cleanly,
  about 35 MB saved at Holtburg. **Test tools**: steps `memory label` (free, largest piece, private, managed after a full
  collection) and `flushcache`, `set portalreload|ground`; a run.txt is read the moment it appears, so write
  it to a temp file and move it in. The suite (random places from locations.xml with `/tele 37s,67w`, dungeons by name
  with `/teledungeon <name>`, wilderness runs, settings churn, memory snapshots at the Holtburg overlook between) and a
  watcher (crash and error boxes closed, a game frozen 15 s ended so Auto relaunch restarts it, a silent plugin log
  flagged) are scripts kept outside the repo (SESSION-HANDOFF.md says what they do).
- **Far buildings** (`Ui/DrawDistance.FarBuildings.cs`, `DrawDistanceOptions.FarBuildings`, `farBuildings`, off to start; the
  Far buildings row under 3D trees; test step `set farbuildings 1`; the user's call, November 2026: a town two
  landblocks off vanished with a step back). The game makes a block's buildings and the dats' placed objects only in
  full detail (`init_buildings`, `init_static_objs`), as its trees. Past that ring, out to the landscape's edge, each
  block's LandblockInfo (`Buildings`: model and frame; `Objects`: the placed ones) goes into the far trees' instances
  (the game's own models, instanced), turned by the yaw of its quaternion (2 atan2(z, w)), size 1, raised or lowered
  by the coarse ground's distance from the true ground under its origin. Models under 2 m are left out. Past the game's landscape
  they stand on the far land, drawn in its pass with its camera (the far trees' runs with `Far` set, `DrawFarTrees` given
  the far projection): with a low landscape, far towns were gone. `[far-buildings]`
  lines in log.txt. Untested in the game when written.
- **Far land** (`Ui/FarLand.cs`, `Shaders/farland.vs.hlsl`, `farland.ps.hlsl`, `DrawDistanceOptions.FarLand`, `farLand`,
  rings past the game's landscape, 0 to start, 32 at most, working with Draw Distance off as the fog does; no row on the
  page yet; test step `set farland N`; the user's call, November 2026: keep the game's landscape low, its blocks cost
  about 1 MB each, and draw the land beyond for a few MB). Stage 1 of SESSION-HANDOFF.md's plan. Every block from the
  game's last loaded ring (`LScape::mid_radius`, the LScape at `[0x8456F0]`, `+0`) out: its 81 heights through the
  region's LandHeightTable, cells split by `SceneryLayout.SplitNwSe`, normals from the heights, each vertex its
  terrain's texture averaged (a road vertex terrain 0x20's), a 30 m skirt down each edge; read from the cell dat's file
  bytes (`GetReaderForFile`, then `CellLandblock.Unpack`), not `ReadFromDat`, whose FileCache keeps every file for
  good; 3 ms a frame of reading, nearest first; one managed buffer, 32-bit indices, built round your landblock and
  shifted into the camera's. **Written a slice a frame** (`FarLand.Building.cs`; the user's eye, November 2026: a
  jumpy hang at every landblock edge): each crossing rewrote all 4,216 blocks in one frame, about 180 ms, for 66 new
  ones; now the blocks are listed in the buffers' order, new buffers made, and each frame writes what fits in 4 ms, the
  old buffers drawn until the new are whole. Each triangle was a three-int array (about 1.9 million a rebuild), now
  added straight. The billboards, 3D trees and far buildings are placed the same way (`DrawDistance.Placing.cs`): their
  placing took 123 ms in one frame at a crossing and 1,358 ms on an area's first build (each model's picture made as
  it's first met), now as many blocks a frame as fit in 4 ms, the old ones drawn till all are placed (they share one
  home, so they swap together), restarted if another crossing comes first; the build's log line gives each step's
  time (placing, 3D trees, buffer, writing). **Drawn only where the game drew nothing**, in WorldPass: the game's projection ends near
  3.3 km, so the land has its own (the game's with the far plane at 60 km, near 10 m), and its depths can't mix with
  the game's. A screen quad at depth 1 (LessEqual) sets the stencil's top bit where the depth is still the cleared 1.0
  and clears it elsewhere (only that bit: `StencilWriteMask` 0x80); the land is drawn there alone, depth tested and
  written with its own depths; then a quad puts depth 1.0 back there, so billboards and the game's see-through things
  test as before. **Drawn as background** (`FarLand.Background.cs`; the user's screenshots, November 2026: the far skyline
  covered the lower half of lifestones' crystals, some characters and big see-through crystals): drawn after the game's
  world, those objects, drawn without writing depth, looked empty to it. So it's drawn from LScape's draw (`0x506D90`:
  the sky's pieces, `0x507A50` with 0, then the landblocks, `0x5069E0`, thiscall, called at `0x506DAB`, then the
  sky's after-world pieces), reached from `SmartBox::RenderNormalMode` (`0x453BED`) with the camera outdoors and from
  the render device's indoor drawing (`0x5A5950`, at `0x5A597B`) with the camera in a building: that call is hooked,
  the far land drawn first (the same drawing, sun shadows' noting paused), then the game's landblocks, so everything
  the game draws lands on top. The world pass draws it as before when the hook can't be set or the landscape draw
  didn't come. Found with a throwaway Iced disassembler. Untested in the game when written. Lit as the game's ground: ambient plus the sun on the normal, times 0.88 (`GroundLight`), measured
  where the far land meets the game's ground with the red test tint (`set farlanddebug 1`): Grass's 1.15 came out 1.31
  times as bright as the game's (86, 88, 39 against 66, 67, 31; after, 67, 68, 31), a bright band past that line that
  the player saw first on a tan island in the river; the game's fog.
  Tried in the game (the Holtburg overlook, landscape 11, fog 10%): rings 11 to 24, 1,960 blocks, 376,320 triangles,
  10.4 MB, built in 49 ms; the far snow-capped mountains showed where there had been sky, 90 fps, free memory 90 MB
  less (the buffers' system copies and the build's lists, not collected yet). `[far-land]` lines in log.txt.
  The terrains' pictures and alpha maps, made once a session for every tier, took a 594 ms frame at login on the
  game's thread: their dats are read there and the decoding and shrinking done on a worker (`FarLand.StartTerrain`),
  nothing else of the far land happening till `terrainReady`. Untested in the game when written.
  Water terrains (0x10 to 0x14) are their own blue textures (0x10 `234C6E`, 0x11 teal `154450`, 0x12 to 0x14 darker),
  lit as the ground (with water reflections off, 25, 57, 77 against the game's 27, 57, 74). A fixed
  blue came first, taken for right because the vertex colours had averaged narrow rivers with the grass beside them
  into olive.
  **Billboards on the far land** (`FarLandBillboardRings`: with Billboards or Far buildings
  and far land both on): `UpdateBillboards` takes blocks out to the far land's edge, those past the game's landscape
  (`billboardLandscape`) standing on the true ground (the far land is full detail), with no 3D trees or far buildings,
  in runs of their own after the rest (`farLandBatches`), which `FarLand` draws inside its pass with its camera, with the game's fog on again for
  them (the land's shader fogs itself, so it was off, and far billboards stood out bright against hazy hills)
  (`DrawFarLandBillboards`, the state block taking the projection back). Tried in the game, fresh restarts at the
  overlook, far land to ring 24, video memory on: landscape 5, 1,736 MB free, 91 fps; landscape 11, 1,626 MB free,
  89 fps; the two look all but the same (135,559 billboards, 133,294 on the far land at 5). So a game block cost about
  0.27 MB here, not the 1 MB measured before, and lowering the landscape saves less than hoped at Holtburg.
  **Textures** (`Ui/FarLand.Textures.cs`): one R5G6B5 atlas in the default pool (4096 square, 7 levels, about 43 MB
  of video memory, none of the game's 4 GB), 64 pixels a block, each block at its landblock x and y mod 64 so filtering
  across an edge reads the neighbour; each pixel the four corner terrains' textures (each shrunk to 8 pixels a cell,
  one copy a cell as the game lays them) laid as the game lays them: `Feed/TerrainBlend.cs` ports the client's rules
  (ACE's TexMerge: the palette code, the base terrain and up to three overlays by corner set, road codes, the alpha map
  picked by a hash of the code), and `Ui/FarLand.Shapes.cs` reads the region's corner, side and road alpha maps (some
  surface textures' first picture isn't in the portal dat: the first one it has is taken), shrunk to a cell's 8 pixels in
  each of four turns, each turn the one whose solid corners are the set's. The maps hold how much of the layer under
  shows (every one's solid corners were its code's complement), so they're inverted; their turns then run 8, 1, 2, 4
  for a corner map, the client's own order. Seen in the game: subtle at a distance (the user's call, November 2026,
  for the same edges as the game's). Baked on up to three threads of their own
  (`FarLand.Baking.cs`: each picture, its smaller copies and their R5G6B5 pixels are pure arithmetic on tables made
  once; the user's call, November 2026: baked on the game's thread at 4 ms a frame, 3.9 s of work for 4,784 blocks took
  10.2 s after a teleport), at most 192 blocks out at once, and copied in on the game's thread within 4 ms a frame
  through a system-memory texture (`UpdateSurface`, level by level); a new set of blocks raises a generation, and
  pictures baked for an older one are dropped. The buffers are made only once their blocks are baked. A set of blocks wanted mid-bake (the landscape changing) has to queue its bakes again, or its
  new blocks stayed black (the default pool starts zeroed): seen in the game. A hidden hud lets the atlas go before a
  device reset; vertex colours are drawn until it's baked again.
  **Fine rings at the seam** (`FarLand.Textures.cs`; the user's call, November 2026: a line across the hills where
  the game's ground meets the far land, across the river from the Holtburg overlook): the first three rings past the
  seam (`FineRings`, no further than ring 6) come from a second atlas, 256 pixels a block (75 cm a pixel), terrains
  32 pixels a cell, slots mod 16 (`FineSlots`), 9 levels, about 43 MB more of video memory; the ring inside the seam
  (the game's, read for this alone, `marginOnly`) and the ring past them are baked into it too but not drawn from
  it, so filtering across a fine block's edge reads its true neighbour. Each fine block bakes in 3 to 7 ms. The
  buffers run near-fine, near-coarse, far-fine, far-coarse, a draw each. Seen in the game (same spot, test step
  `set farlandfine 0|1`): the thin dark line along the seam (the coarse atlas's empty neighbour slot) gone, the near
  hillside a little crisper; the change of shading at the seam is the lighting's, not the texture's.
  **Three tiers** (the user, November 2026: from a mesa over the desert (landblock 8B7A) the far half of the desert showed
  as grass, then later didn't): with only the 64-slot atlas a far land reaching past ring 31 (Landscape 15 and Far land
  32 reach 47) put blocks 64 apart in one slot, the last baked winning. Now fine (256 px, mod 16, the seam's first
  rings), mid (64 px, mod 64, to ring 30, `MidMaxRing`) and far (32 px, mod 128, past it; the settings reach 56 at
  most), each tier baked with a ring either side (`FarLand.TiersFor`), the buffers a run a tier, near then far
  (`runTriangles`), the shapes' turns by tier. Untested in the game when written.
  **Taking over the game's coarse rings** (`Ui/FarLand.Takeover.cs`, `DrawDistanceOptions.FarLandNear`, `farLandNear`,
  off to start; test step `set farlandnear 1`; the user's call, November 2026: distant water and rivers lose their
  shape in the rings the game draws from every 2nd, 4th or 8th point, and Ground detail costs too much memory there).
  `RenderDeviceD3D::DrawBlock` (`0x5A28D0`, thiscall, the CLandBlock, `ret 4`) draws one block's ground (its cells'
  DrawLandCell, vtable `+0x54`, then sort cells, `+0x58`), called through the device's vtable, slot `0x7E6638`, which
  is pointed at the plugin while on (checked to hold `0x5A28D0` first). A block under full detail (`+0x48` under 8,
  its id at `+0x28`) that the far land has built is skipped; the nearest such ring seen each second is where the far
  land starts (`nearestCoarseRing`). Those blocks (`wantedNear`, ring up to the landscape) are drawn first, with the
  game's own camera, real depths and no stencil, so billboards, trees and buildings sort against them; billboards
  there stand on the full-detail ground (`FarLandTakesOver`). Anything standing in a skipped block's cells (creatures
  half a kilometer off) isn't drawn. Tried in the game (landscape 8, rings 3 to 8 taken over, 264 blocks): the river
  keeps its shape, 92 fps, about 6,000 block draws skipped a second. The game still loads those blocks, so this is
  for looks, not memory.
- **Hitch log** (`Ui/HitchLog.cs`; November 2026, the user's "hitching every few seconds while running around"): every  frame over 40 ms with the game in front gets a `[hitch]` line in log.txt (400 a session): its length, whether a  garbage collection ran in it and the managed heap's size, and the plugin's pieces that took time in it, each timed  with `using var _ = HitchLog.Measure("name")` (the draw distance's RenderFrame as `billboards` and `draw  distance`, `far land`, `grass`, `shading`, and each world-pass drawer by its class name), the rest being the  game's and other plugins'. The frame is the frame rate window's, one RenderFrame to the next. Time anything new  that runs every frame the same way. Untested in the game when written.
- **Memory** (`Ui/FrameRateWindow.Memory.cs`, `Feed/MemoryHistory.cs`, `MemorySample.cs`; the user's call, November 2026:
  "the client's memory leak is my top concern"): the Frame Rate window's Memory section, every 2 s from login whether
  it shows or not: the 32-bit address space used of its total (GlobalMemoryStatusEx; the real limit), private bytes
  and working set (Process), the managed heap (GC.GetTotalMemory, every plugin and every hot-reloaded copy in it), and
  every 10 s the largest free piece (the VirtualQuery walk the device lines use, `FreePieces`), all read on a
  thread of the pool (`ReadMemory`), as the device line's walk is, since on the game's thread they were likely a
  frame of about 40 ms every 10 s (not confirmed; the device line now says how long its walk took); growth in MB a minute
  over 5 and 15 minutes and since login (a fitted line, so one load doesn't decide it), minutes until 512 MB is left
  at that rate, a 15-minute graph with the marks; Clear forgets it. The frame rate log's rows end with used_mb,
  private_mb, working_set_mb, plugin_mb and largest_free_mb. Seen in the game: 2,987 MB used, 2,036 private, the
  managed heap about 300 MB (after a session of hot reloads), the largest free piece 875 MB; standing still for three
  minutes it held flat (the rise in the first 30 s was the reload's loading).
- **Texture filtering, taken out** (November 2026, the user's call: no difference seen, and the game's own Config page
  has Anisotropic texture filtering; it was `Ui/DepthShading.Anisotropy.cs`, `ImageOptions.Anisotropy`, 2 to 16, the Image
  section; test step `set anisotropy N`; the user asked why the far water looked distorted). The far water's streaks
  are the game's own: retail looks the same as Scenery 1 and 2 (seen in the game). Setting the samplers' filter from
  outside did nothing, since RenderDeviceD3D::SetSamplerFilterMode (`0x5A4460`) sets it on every draw; it turns linear
  into anisotropic when the texture filtering preference (`0x81FFA0`, registered at start, `0x54F6B8`, set by the
  graphics presets; 2 here, trilinear) is 3. So the preference is set to 3 while on (put back when off or at shutdown),
  and MaxAnisotropy (sampler state 10, which the game only sets making the device, `0x5A5347`) each frame. At 16x the
  far water looked the same: likely the wavy texture squashed by foreshortening, which filtering can't undo, or the
  terrain's blending stages not taking the setting (not looked into). Whether it sharpened roads far off was
  never checked.
- **Built once after arriving** (`DrawDistance.HoldBuilds`, `Feed/WorkTimer.cs`): the far scenery (billboards, 3D
  trees, far buildings, the far land) isn't built while the draw distance is still stepping up (not `AtTarget`, held
  30 s at most). On arriving it was built for the game's own landscape (3 rings, 2,796 blocks at Hebian-to), then all
  again for the setting's (8, 3,631 blocks), and the first build's hitches held back the step, whose frames must run
  smooth first: 19 seconds in log.txt (the user, November 2026: "the loading bar is important"). Each stage logs its
  totals once done, for what loading waits on: `[billboards] blocks worked out:`, `[far-land] blocks read:` and
  `[far-land] block pictures baked:` (items, time spent, frames used, seconds from first to last). Untested in the
  game when written.
- **Outdoors, or a room that sees out** (`Ui/OutdoorView.cs`): the far land, billboards and 3D trees, water reflections
  and grass are drawn while your character's cell or the camera's is outdoors (under 0x100) or an indoor cell the dats
  mark as seeing outside (the EnvCell's flag `SeenOutside`, 0x1: a building's rooms and doorways; no dungeon cell has
  it, checked on five dungeons). They went by the cell number alone, so a building counted as a dungeon (the user's
  eye, November 2026): in a doorway cell, standing on the grass, the horizon was gone, and stepping out rebuilt every
  far tree and billboard, a second's hang each time. Each cell is read once from its file (not the dat's cache) and
  remembered. The night darkening still goes by the cell number: switched, it would darken building interiors too.
- **Where the device's world is measured from** (`Ui/RenderOrigin.cs`): the camera's landblock, not your character's. Whether the device's camera is the swapped one some effects use is told the same way (`RenderOrigin.Swapped`: where the view puts the camera against the game's camera); going by how far your character was from it each way guessed wrong wherever your height and north differed by about 6 m, and the far land, billboards, 3D trees and grass vanished there (seen in the game, November 2026).
  The game keeps the camera's Position at `0x81FF10` (cell `+4`, origin `+0x3C`: the device's camera exactly, height and
  north swapped as the device has them; the test step `camera` logs both). A step across a landblock's edge with the
  camera still behind you moved the shadow maps and the far scenery 192 m (the user's screenshots, November 2026). So
  everything drawn in the 3D world measures from `RenderOrigin.Landblock` (billboards, 3D trees, grass, shadow maps,
  the water map, lamps), your place is `RenderOrigin.Player()`, and billboards and 3D trees made for another landblock
  are drawn shifted by the landblocks between until they're made again, so the far scenery neither jumps nor blinks. **Their memory** (November 2026): each billboard's 12 vertices are written once, straight into the locked buffer, from a reused list of placements (`billboardPlaces`, sorted by model run into the reused `writeOrder`); **a slice a frame** (`DrawDistance.Writing.cs`, `WriteSome`, 4 ms, one stretch of the buffer locked each frame, the old billboards drawn till `SwapBillboards`): written whole in one frame, 258,000 billboards (99 MB) took 430 ms, a 450 ms hang at every landblock crossed, the user's "lag spikes while running around" (November 2026, the frame rate log against the `[billboards]` build lines); untested in the game when written; the build once grew a list of vertices per model, copied them into one array and that into the buffer, three large copies in the large object heap on every landblock crossed, and kept every block's worked-out scenery ever seen (`ForgetScenerySeen` now drops blocks no longer wanted). At Holtburg with 76,435 billboards a fresh login had 679 MB free before and 1,099 after; the plugin's heap held at about 152 MB through rebuilds. They're built round your character's landblock (the game's full-detail ring is round it), and the centre the drawn buffers are shifted from changes only once the new ones are made (`pendingHome`): set at once, the old ones were drawn a landblock off while the new were worked out, and the far forest vanished at an edge and came back a few steps on (seen in the game).
  The memory panel's time-left warning waits for five minutes of readings and goes by the 15-minute rate: a minute
  holding a landscape load read +105 MB/min.
- **Lamp light and glow** (the user's call, November 2026: "lighting/shadows from non-sun light sources, and maybe
  even emissive objects"). **The game's lights** (`Ui/GameLights.cs`, `GameLight.cs`; test step `lights` logs them):
  Render::add_static_light (`0x54DFF0`) and add_dynamic_light (`0x54E030`), cdecl (LIGHTINFO*, cell, Frame*), both
  keep RenderLight pointers sorted nearest first via `0x54DDC0`: static count `0x8683B4`, pointers `0x86B748`;
  dynamic `0x86B838`, `0x86C0D4`. A RenderLight: D3DLIGHT9 (0x68), cell `+0x6C`, type `+0x70` (0 point, 1 distant,
  2 spot), frame `+0x74` (origin `+0xA8`, its cell's landblock's meters), colour `+0xC0`, intensity `+0xCC`, falloff
  `+0xD0`, cone `+0xD4`, distance squared `+0xD8` (stale: 100 m for lights 20 away). Seen at Holtburg: the houses'
  lamps (static, indoor cells, warm, falloff 3 to 15, intensity 100), a white light round you (dynamic, 2.25, 10)
  and a purple one. Render::config_hardware_light (`0x59BD40`) makes each a Direct3D light: diffuse colour times
  intensity, attenuation 1 over the distance (all but saturated in reach), range falloff times the float at
  `0x821CD4` (1.5); the game lights objects and indoor cells per vertex with them, never the outdoor ground, and
  nothing blocks them. **Lamp light** (`Ui/DepthShading.Lamps.cs`, `Shaders/lamps.ps.hlsl`, `Feed/LampLightOptions.cs`,
  `lampLight`, off to start; Lamp light rows under Sun shadows; test steps `set lamps|lampstrength|lampshadows`): the
  eight nearest to the camera brighten what's round them per pixel, a smooth fade to the game's reach, facing only,
  blocked by a 12-step march through the depth buffer, blended as the picture times one plus the light; strength the
  colour times intensity over ten (to one). Seen indoors at Holtburg: the wall and stones by a lantern warmer, 164 fps.
  **Lamp shadows from cube maps** (`Ui/DepthShading.LampShadows.cs`, `Shaders/lampshadow.vs.hlsl`, `lampshadow.ps.hlsl`):
  the first, marched through the depth buffer, "look awkward, of the character and especially whatever is making that
  shadow on the wall" (the user): a march only knows what's on screen. Now the six lamps nearest you that cast shadows
  get a cube map each, kept between frames (a new lamp drawn at once, two kept ones again each frame in turn) (six 90-degree faces, 512, R32F, distance over reach; one D24S8 surface shared), drawn from the
  noted game draws within the lamp's reach and 15 m (the hooks are in while sun or lamp shadows want them), and the
  pass compares a place's distance with the map's that way (five spread samples). The lamp's light sits inside its own
  lantern, which then hid everything from it (every pixel came out shadowed): nothing within 1 m counts (the near
  plane: 35 cm at first, and a beam by the tavern's lantern darkened the wall below) and draws placed within 75 cm (the lantern) aren't made; the ceiling's beams by a lamp shadowed themselves in
  stripes until a grazing surface was looked up further off it along its normal (a bigger allowance let light past
  walls' feet onto the ground outside). Each map is looked up once, before the shader's loop over the lights
  (`MapSeen`, the maps' lamps in c24 to c29): six lookups in the loop ran out of registers. **Every lamp within the
  Distance setting, and up to twelve shadowed** (the user's call, November 2026: "so much flickering" on trees and
  buildings, whole objects gaining or losing light at a distance as you walked): the game lights only its eight
  nearest lamps with Direct3D's fixed lights, and the plugin took over only the six nearest of those, so a lamp leaving
  the game's eight took its light off a whole building at once. Now every lamp within `LampLightOptions.Distance` (200 m
  to start, 50 to 400, a slider) is the plugin's, up to 32 lit and 8 more fading out (`MostLit`, `MostDrawn`; lamps whose reach touches the view first, `InView`, then the rest: by
  distance alone, lamps behind you and inside houses took the 32 places in Hebian-to, and one across the square in
  plain view stayed dark, the user's screenshot; the 10-second `[lamps]` line says how many are in range and in view; the
  shader's `MOST_LIT` 40), each easing in over half a second as it comes into range (`EaseLampShare`, now every lamp's
  presence) and fading over the last fifth of the distance, the game's own light of each dimmed by the plugin's share
  (`GameLampSwitch.Places`, every lamp but the light round you). While the game draws its see-through things (`GameLampSwitch.BeginSeeThrough`
  and `EndSeeThrough`, round every flush of its list, see Drawing inside the game's frame), its lamps are whole: those come after the plugin's lamp light, which can't light
  them, and dimmed with the rest the spire's glow, the crystals' flames and the lamps' halos in Hebian-to went dark
  unless the light round you reached them (the user's screenshots, November 2026). **The night** (Night, the lamp pass's
  darkening outdoors after dark) **spares what's bright already** (`spare` in lamps.ps.hlsl, from a copy of the frame
  so far on sampler 13, `beforeLamps` in `DepthShading.LampCopies.cs`): brightest channel 0.55 to 0.9 eases it off, so the lamps' glass,
  portals and the moon, drawn self-lit, keep their glow; at 80% night a lamp's glass measured 141 against 255 without
  (test shots in Hebian-to's square, November 2026). The crystals' flames, being see-through and drawn after, were never
  darkened (203 against 215); their halos over a night sky are only darker by the sky behind. Untested in the game when
  written. **What shows through see-through things isn't darkened** (`Ui/DepthShading.SeeThrough.cs`,
  seethrough.ps.hlsl): the crystals are partly see-through and drawn after the night pass, over ground it had
  darkened, so up close they went faint and the gold one nearly clear (the user's screenshots; tried in the game from
  the same spot with each part off in turn: only the night made the difference, not the lamp light, the game's lamps,
  shading or where the passes are drawn). After the frame's done, before Glow, each pixel that differs from the copy
  made just after the lamp pass (something drawn over it since) gets the night's change to what's behind it (before
  less after) put back, as much as it differs. Untested in the game when written. Lamps without a map light unshadowed, as the game's
  always did (they lit nothing before). Shadows go to the `ShadowedLamps` nearest you (8 to start, up to 12: the
  shader's texture slots, s1 to s12, beside the depth), each map's shadow easing in when its lamp takes it and out before
  it's given up (`lampShadowShares`, the shader's `mapShares`); maps are made as their slots are first used, 6 MB each
  in the card's memory, three kept ones redrawn a frame. The shader's lamp array was `lamps[i * 2]`: indexed by
  anything but the loop's counter, ps_3_0 copies a constant array into temporary registers, which 16 entries fitted
  and 80 didn't (`X4505`), so places and colours are two arrays (c2, c42). About 5,000 instruction slots with twelve
  maps (the old six-map one 2,600): the user's card refused it ("couldn't make the shader lamps.ps.cso"), which stopped
  all the shading for the session, so `build.cmd` also makes it with `MAPS` 10, 8 and 6 (4,097, 3,384 and 2,682 slots now;
  `lamps10.ps.cso`...), `EnsureLampShader` takes the biggest the card accepts (logging the card's
  `MaxPixelShader30InstructionSlots`). The user's card says 4,096 but took an eight-map shader of 3,458 by fxc's count
  and refused one of 3,609 (the driver counts its own way), so keep the eight-map one under 3,458: `Seen`'s hash and
  turn have no sine or cosine for that (eight slots each, twice a map); shadows go to no more lamps than it has, the settings page says "your card: up to
  N", and a card that takes none keeps the game's lamps with the rest of the shading still on. Test steps `set lampdistance N`, `set shadowedlamps N`. Untested in the game when written. **In the game's place**
  whenever Lamp shadows are on (`Ui/GameLampSwitch.cs`; a switch of its own, `replace`, until November 2026, the user's
  call, since it only worked with shadows): the device's LightEnable (vtable
  53) is hooked and refuses the point and spot lights at the places of the lamps given maps (`GameLampSwitch.Places`,
  D3DLIGHT9's Position at +52), so those, which the game lights with nothing blocking them, are the plugin's alone, and
  a lamp with no map is left to the game (refusing every lamp left those past the six nearest you lighting nothing);
  no light through walls or floors near you, and none of the subtracting shadow pass. **Handing a lamp over smoothly**
  (the user, November 2026: "a lot of flickering with object lighting, while moving around"): lamps crossed the
  six-nearest line as you walked, and each one's light on characters and furniture jumped between the game's look (per
  vertex, full strength, through walls) and the plugin's, with a frame of neither or both between (Places was last
  frame's). Now a lamp holding a map counts as 3 m nearer (`LampStickiness`), keeps its map until its light has faded
  out of the plugin's hands, and its share eases over half a second (`EaseLampShare`, `lampShares` by place): the
  plugin's colour times the share, the game's own dimmed by the rest (`GameLampSwitch.Places` carries each lamp's game
  share; SetLight, vtable 51, is hooked too and keeps each light as the game set it, so LightEnable scales from that and
  sets it whole again after). A lamp holding a map is always among the eight lit. Untested in the game when written.
  **Dimmed in SetLight only, never switched off** (November 2026): the game puts each of its eight light slots' light
  on the device with SetLight every time (`Render::enable_active_lights`, `0x54CC90`) but switches a slot through
  `RenderDeviceD3D::SetFFLightEnable` (`0x5A4230`), which calls LightEnable only when that slot's state changes,
  remembering it at `+0x4C9` of the RenderDeviceD3D (`[0x870340]`; its Direct3D device at `+0x468`). Switched off in
  LightEnable, a slot the game still took for on stayed dark for every light it put there after, the sun's too, and
  which slots were caught changed as you moved: Hebian-to's crystals went see-through and every lamp in view dark at
  one step and lit at the next (the user's screenshots, cell `0xE74E001E` against `001D`; with Lamp light off they
  were solid). So only SetLight is hooked: a lamp's colours times the game's share, to black for none, and the slots
  are switched as the game remembers them when the hook goes in and out (`Resync`). Untested in the game when
  written.
  The light the game hangs round
  you sits about 3.7 m ahead and above: taken for a lamp, it lit everything outside the tavern and leaked under its
  walls, so it's told by being dynamic, faint (intensity under 10) and within 6 m. `depthview 5` colours each pixel
  by the light giving it most, the lights listed once in log.txt (`[lamps] debug light`). **Fires and lamp posts**
  (`LampLightOptions.FireLight`, `fireLight`, on to start; the Fires box beside Cast shadows; test step `set
  firelight`): the game lights only what its light lists hold, and outdoors those had nothing: Hebian-to's bonfire
  and lamp posts glowed but lit nothing (seen in the game, November 2026). So `Ui/FireLights.cs` gives each world
  object named like a fire (bonfire, campfire, fire, torch, brazier), or with the campfire or brazier model (`FireSetups`, Decal's `LongValueKey.Model`: the camps' campfire, weenie 4128, has no name), not creatures, players or anything carried, a
  warm flickering light a meter above it, falloff 6, and `Ui/PlacedLights.cs` reads the dats' own: the placed
  objects in the 3x3 landblocks round you (LandblockInfo's Objects and Buildings) are Setups, and a Setup carries
  `Lights` (a place in the model, colour, intensity, falloff, as the game's lamps have: Hebian-to has 51, warm,
  falloff 4 to 5), turned and placed as their objects, left out where the game has a light already. Both join the
  lamp pass's lights, shadow maps and all. At Reach 300% the posts washed the town orange: 150% suits them.
  **Fire surfaces** (`Ui/PlacedLights.Fires.cs`, `Ui/PlacedLight.cs`; the user's call, November 2026): Hebian-to's
  fireplace is no object but the hearth's floor, a polygon of indoor cell `0xE74E0137` painted with the embers picture
  `0x05000D32`, so every polygon showing one of `FirePictures` (that and the glowing embers `0x050026C6`; lava and
  flames left out, or a lava dungeon would crowd out the lamps) is gathered into 3 m clusters, each a light half a meter
  above its middle with FireLights' colour and flicker (`FireLights.Flicker`), reaching 1.5 times its size (4 to 8 m).
  Looked for in the 3x3 landblocks' indoor cells (`0xXXYY0100` on, `NumCells` of them, read 2 ms a frame from the
  file's bytes, not `ReadFromDat`, whose cache keeps every file), their static objects, and the outdoor Objects and
  Buildings' models (worked out once a model). Pictures were picked by eye from a contact sheet of the red ones (a
  throwaway tool over the dats). `[lamps] a fire surface given light` in log.txt. Untested in the game when written.
  **Reach** (`Reach`, 100 to 300% of the game's, 150 to start): the Holtburg tavern's lamps reach 4.5 to 7.5 m, hung
  9 m in from the west door; at 300% the open door throws a beam outside with your shadow in it (seen in the game,
  November 2026). How a face turns counts for little (`turned` in lamps.ps.hlsl, 0.35 to 0.85, faded to none for faces
  turned well away): the depth only gives each triangle's own normal, where the game lights a character smoothly
  between its corners', and armour showed every facet (the user, November 2026); and **the way a surface faces is
  smoothed** (`SmoothNormal`): from places about 6 cm off each way (a pixel to 16 of them; past a few meters, the
  next pixel), each side counted as far as it lies on the pixel's own facet (`SameSurface`), so a face and armour by a
  bonfire no longer showed each triangle as a hard-edged patch (the user's screenshot, November 2026). Only for how much
  light a surface turned toward a lamp takes: the shadow maps are still looked up off the triangle's own way, since off
  the smoothed way a lookup on a low-poly face could stay near its own triangle, half blocked by the character's own
  body, and the samples' turn (a hash of the place, fixed in the world) showed as dots that characters moved through
  (the user's screenshot, November 2026; untested in the game when written). How far off the
  facet a side is goes against how far to the side it was looked (6 cm), not against its own distance: so judged,
  something in front of the ground (a sword, an arm) passed, being off mostly along the line of sight, and the
  ground's way tilted round it, a dark outline round the character and the weapon (seen in the game); untested in the
  game since; and lamp colours are deepened
  (`Warmth`, each channel to the power 1.6), so lit characters keep the game lamps' warm look. A line of light along the walls' feet was the lookup point on a wall's bottom row: a
  wall is one sheet, so its outside is as far from a lamp inside as the wall, and the crease's normal pointed up; the
  point is now also moved 30 cm toward the camera (10 cm fixed walls seen face on, not an angled one), and normals
  come from the neighbouring pixel on the same surface each way, not the 2x2 block. A pixel also takes the least light any of its
  four neighbour normals gives (a crease pixel has the wall's among them), where they disagree by more than about
  75 degrees (`crease`: a wall's foot, not a character's facets), and, as the ground outdoors isn't in the
  maps (a ray just under a wall's lower edge found nothing), a point 30 cm above must be seen too, softly, where
  something is within half a meter of it (`nearWall`; further, your body cast a second, stepped shadow). Shadows soften with distance
  from what casts them, the lamp taken as a light 40 cm across (1.5 m washed a beam's shape out; 60 cm left your shadow
  soft): the blocker nearest the place, found over a wide search first (averaged, the door frame blurred your shadow; from the ray alone, a place just outside a shadow found none and the edge stayed
  hard), then nine samples spread by how far past them the place is, turned by a hash of the place so the map's
  texels don't show as steps on the ground and the light fades out as
  (1 - x squared) squared: the beam outside the door ended in a hard line (the user, November 2026). `depthview 4` shows what the first shadowed lamp sees. Seen: your
  legs' and the table's shadows on the floor, the pillar's far side dark, the wall no longer shadowed for nothing.
  The pass itself:
  **Lamp shadows** (the user's call: player shadows from lamps, as in a room): a shadow pass before the light, the
  same shader in mode 1, multiplying in what's left of the game's own light (it lights everything in reach in full)
  where the march finds something between a surface and a lamp, up to 55% times the strength; the dynamic light within
  3 m of you (the one the game hangs round your character) casts none. Seen: your shadow on a house's floor.
  **Glow** (`Ui/DepthShading.Glow.cs`, `Shaders/glowbright.ps.hlsl`, `glowblur.ps.hlsl`, `ImageOptions.Glow`, 0 to start;
  the Image section's Glow; test step `set glow N`): the art marks nothing as glowing, so it's told by look, bright
  (brightest channel past 0.6) and strongly coloured (near-white sunlit armour and clouds don't); a quarter-size
  pass of what glows (from 0.5), a Gaussian blur across and down three quarter-size texels a sample, added (twice the
  strength) onto the screen, after the see-through things and before
  the anti-aliasing. Seen in the game: the lifestone's crystal brighter with a soft halo; the hearth's dim embers don't
  glow. **What the plugin's lamp light made bright doesn't glow** (`Ui/DepthShading.LampCopies.cs`): a house wall lit
  bright orange bloomed whole (the user's screenshot, November 2026). The frame is copied just before the lamp pass
  (`beforeLamps`, also the night's sampler 13) and just after it (`afterLamps`), and glowbright.ps.hlsl takes a pixel
  that still matches the copy after (nothing drawn over it since) as it looked before the lamp light, and one that
  doesn't (a crystal, a lifestone, a halo, drawn see-through after) as it ends up. Untested in the game when written.
  Teleporting into a building takes the indoor cell and a place in it (`/teleloc 0xA9B40163 [104 39 97]`); a
  place outside the cell is refused without a word.
- **Drawing inside the game's frame** (`Ui/WorldPass.cs`): billboards and grass drawn on Decal's RenderFrame, after
  the whole world, came out in front of the lifestone's crystal (seen in the game, November 2026): the game draws its
  see-through things last, sorted and without writing depth, so they hide nothing behind them. SmartBox::
  RenderNormalMode (`0x453B40`) draws the world, then calls D3DPolyRender::FlushAlphaList (`0x59E3F0`, static cdecl,
  one int, 0) at `0x453C2B` for the see-through list; that call is hooked while a subscriber has something to draw
  (`WorldPass.Add(draw, wanted)`, `wanted` looked at on every RenderFrame: the depth passes' switches, billboards
  built, far land built, grass on), drawing each subscriber's piece and then the game's list. Every piece subscribes
  when it's made, so before this the hook was in with every graphics setting off; now it's out then (done when the
  hook was suspected in the teleport crashes, which a longer test didn't bear out: see The teleport crashes); it's
  in for a second at login while the Default profile is
  still the one in use. Each still builds on RenderFrame and draws there while the hook isn't in (`WorldPass.Hooked`). Tried in the game: the crystal now covers the
  far trees, and grass draws as before. The passes that read depth (water, shading, sun shadows: `DepthShading.
  DrawInWorldPass`) are drawn there too, after the billboards and grass: on RenderFrame they shaded the ground behind
  the crystal onto the crystal (seen in the game, the sun shadows' first build). Ours is the depth buffer then, so the
  pass puts the game's in its place while it samples ours and ours back after, for the see-through things to be
  tested against. The image passes (anti-aliasing, sharpening) stay on RenderFrame, over everything. The far land isn't
  drawn here but earlier, after the sky (see Far land, Drawn as background), unless that hook can't be set.
  **That isn't the only flush of the see-through list** (November 2026): `FlushAlphaList(fraction)` keeps two lists
  of 3000 meshes (counts, a ushort each, at `0x8729BC` and `0x8729C0`; size the float at `0x7E6550`) and draws only
  when one holds that fraction of its size, the last call passing 0 (always). `DrawBlock` calls it after every cell
  with the float at `0x821EE0`, 0.75, so in a busy place (Hebian-to's square) the list is drawn part way through the
  world, before the plugin's passes; `DrawBuilding` calls it at `0x5A041B` and `PView::DrawCells`, with the camera in
  a building, flushes it whole at `0x5A5982` before the indoor cells. A full list drops what comes after
  (`AddMeshToAlphaList`, `0x59D240`, returns false and `DrawMesh` goes on), so some guard is needed. Those three
  calls are hooked (`WorldPass.EarlyFlush`), giving the game's lamps whole to what they draw, and counted:
  `[world-pass] the game's see-through list` lines every 10 s say how full it got at the last flush and how often it
  was drawn early. In Hebian-to's square they said none at the last flush and 1,000 to 6,000 early draws in 10 s:
  `DrawBuilding` flushes whatever's queued before every building (just after setting the next draw's detail texture,
  `0x81FCB0` and `0x81FCB4` for `ACRender::SetDetailSurfaceInternal`, and `DrawMesh`'s draw-now flag `0x867374`,
  nothing the queued meshes need), so in a town every see-through thing came before the plugin's passes, darkened by
  the night and under the billboards. So `DrawBuilding`'s and `PView::DrawCells`' flushes are made the same 0.75
  overflow guard as `DrawBlock`'s (`WorldPass.DeferEarly`, `OverflowGuard`; `set alphadefer 0` puts the game's way
  back): the rest waits for the last flush, tested against the whole world's depth, which is also right for a crystal
  in front of a building drawn after it. Untested in the game when written.
- **Sun shadows** (`Ui/ShadowCasters.cs`, `Ui/DepthShading.Shadows.cs`, `Shaders/shadow.vs.hlsl`, `shadow.ps.hlsl`,
  `Feed/SunShadowOptions.cs`, `SettingsData.SunShadows`, `sunShadows`, off to start; rows Sun shadows, Darkness and
  Distance under Shading; test steps `set sunshadows|sunstrength|sundistance|sunreplay`, `probe`, `find words`; the user's call, November 2026:
  "especially sun light"). A real shadow map, from the game's own draws. The test step `probe` (`Ui/DrawProbe.cs`)
  counts the device's calls over a frame, split at BeginScene and at the see-through list: at the Holtburg overlook
  the world was about 8,300 DrawIndexedPrimitive calls from managed vertex buffers of 44-byte vertices (FVF `0x252`:
  position, normal, colour, two texture coordinates; drawn through SetVertexDeclaration), each placed by
  SetTransform(World), fixed-function, about 280,000 triangles, plus 1,350 small DrawPrimitiveUP calls (left out).
  Managed buffers outlive the frame, so while sun shadows are on the device's BeginScene, SetTransform,
  SetRenderState, SetTexture, SetVertexDeclaration, SetStreamSource, SetIndices and DrawIndexedPrimitive (vtable 41,
  44, 57, 65, 87, 100, 104, 82: one vtable for every device, patched with VirtualProtect) note each such draw from
  BeginScene to the see-through list (`ShadowCasters.EndOfWorld`, from WorldPass), its buffers held (AddRef) until
  the replay lets them go. The replay (before the shading pass, on RenderFrame) draws those placed within Distance
  plus 40 m of you, with the game's own vertex declaration, into a 2048 R32F render target from the sun
  (orthographic, Distance each way, moved in whole texels), leaves cut out where the game alpha tests. The shading
  pass (which runs for these with Shading off too, its occlusion and contact shadows then none) looks each pixel up:
  a 3x3 texel filter blended by where it falls between texels (hard steps showed each 6 cm texel as a block at the
  camera's feet), and a shadowed pixel keeps the game's ambient and loses the sun's share by the game's own light
  (ambient plus sun on it, as the grass matched). Seen in the game: the character, the lifestone's feet and the
  buildings cast soft shadows in the right direction, with no crash. Billboards and grass cast none (drawn by the
  plugin, not noted). Two maps (the user's call): near 40 m, far out to Distance (150 to start, 400 at most), the
  shader blending near into far toward the near map's edge; at 60 m alone the town below the overlook cast nothing.
  **The cost, measured at the plateau overlook** (test steps `measure`; `set sunreplay 0` notes the draws but makes
  no map, to time the hooks alone): retail 150 fps, landscape 24 141, billboards 139, real trees to 2 landblocks 107,
  to 3 at 50% 86 and at 100% 60 (the game's own drawing: the third ring alone costs about 40 fps). The first sun
  shadows halved the frame rate with trees to 3 (60 to 30): every draw held its buffers through a delegate made for
  each call (Marshal.GetDelegateForFunctionPointer, eight a draw), a record and matrix allocated per draw, and
  eight hooks. Now (61 to 56 with trees to 3, 107 to 93 at 2): records are reused, a draw too far
  for the far map isn't noted (`SetArea`), only BeginScene, SetTransform and DrawIndexedPrimitive are hooked (the
  rest asked of the device for a near draw only), and `DeviceCalls` keeps its delegates. What's left is mostly the
  hooks themselves: each call the game makes into one crosses into managed code (about 65 ns), some 17,000 a frame;
  the maps and the screen pass cost little. **Holding the buffers**: a noted draw keeps the references the device's Get
  calls hand back until the frame's replays are done (`ShadowCasters.ReleaseNoted`, after the world pass's, at the end
  of RenderFrame, and before a device reset); without them the game crashed in `DrawInto` (an AccessViolationException,
  which .NET can't catch: November 2026), a buffer let go within the frame before the replay. Held till the next frame
  instead, the device couldn't reset ("Could not initialize Direct3D"): some of them are in the default pool. **Casters out of view are kept** (`Ui/ShadowCasters.Kept.cs`; the user's screenshots, November 2026: "I turn slightly to the right and the tree shadow turns on"): the game draws only what's in the camera's view, so a tree just off the screen cast nothing, and every turn of the camera put shadows on or took them off. Now each noted draw is a record kept by its mesh (buffers and range) and its place (to 5 cm) and turn, holding its references, for 30 s after it was last seen while within the maps' reach (`SetKeepArea`); only managed index buffers are noted, a default-pool one perhaps being refilled. A mesh drawn again within 1 m of where it was the frame before, and gone from there, twice within 10 frames (`ConfirmFrames`: once was enough at first, and a tree's parts sharing a mesh a meter apart, one leaving the view as another came in, took the canopy's shadow away out of view), is moving (a creature, you), and is kept only while it's drawn, or it would leave shadows behind. All are let go before a device reset (`ReleaseAll`, from `Hud_OnDestroyTextures`) and, but this frame's, when the device's world measures from another landblock (`Origin`). The lamps' cube maps use the same records. Managed buffers aren't all filled once: the first build kept records whose buffers the game had filled again since, and the maps filled with that geometry in the wrong places (no shadows and a darker scene until Sun shadows were switched off and on, seen in the game). So the buffers' Lock (vtable 11, vertex and index buffers, `ShadowCasters.Locks.cs`) notes each buffer locked to write, and before the replays a record not seen this frame whose buffer was is let go (`PurgeWritten`). The 30-second `[sun-shadows]` line gives how many are kept out of view, the meshes seen moving and those let go for a write. Untested in the game when written.
  **Draws that write no depth aren't casters** (D3DRS_ZWRITEENABLE, render state 14, asked of the device for a near
  draw): the game's sky, its dome and cloud layer, is drawn round the camera, so always near, and its cut-out clouds
  cast shadows that drifted across the ground ("almost like cloud shadows", the user, November 2026). So SetTransform and DrawIndexedPrimitive now go through native stubs
  (`Ui/ShadowStubs.cs`: x86 written into a read-write-execute page, never freed): SetTransform copies a World matrix
  to the page, DrawIndexedPrimitive while recording checks its distance with SSE, and only a near draw jumps to the
  C# hook; both end in a jump to the device's own, so the stack is the game's call's. Measured: 61 to 58 fps with
  trees to 3, 106 to 98 at 2. **A close map** (the user's call: "more detailed" character shadows): a third, 8 m
  each way round you, 8 mm texels (the near map's 4 cm left a blob), every caster within 48 m made into it (a tree's
  shadow reaches in), the shader taking it where it covers the pixel; the fixed shadow allowance went from 5 cm to
  2 cm, or the shadow left the feet. Seen in the game: the character shades itself (head on chest, belt, arms) and
  its ground shadow has crisp edges; 88 fps with trees to 2. **Your character alone in the close map**
  (`CharacterReach`, draws placed within 1.5 m of you across the ground; the rest only in the near and far maps), with a
  softness of its own (`SunShadowOptions.CharacterSoftness`, `characterSoftness`, 2 cm to start; the Character softness
  row; test step `set charsoftness N`; the user's call, November 2026: characters want other blurring than trees, whose
  10 cm blunted the character's shadow), the shader taking the darker of the close map and the others
  (`Shadowed`'s softness argument, `closeTexel.z`). Untested in the game when written (it was night). **The real trees' cost** (probe at the overlook): to 2
  landblocks 3,400 draws and 103,000 triangles a frame at 94 fps, to 3 8,500 and 286,000 at 64: the third ring is
  about 5,100 more draw calls, about a microsecond each, every tree part its own call; nothing in the game's own
  drawing to make cheaper from here. **Softness** (`SunShadowOptions.Softness`, 20 cm to start, 0 to 100; the Softness row; test
  step `set sunsoftness N`; the user's call: tree shadows "great, but the edges maybe slightly too hard"): a width in
  meters, the same in every map, so the near and far edges match; past a texel and a half, sixteen samples in a Poisson
  disc of it, turned by each pixel's hash, with a little more allowance on slanting ground; under it, the texel
  filter as before. Seen in the game at a forest's edge in Holtburg's south: 0 hard, 20 soft tree shapes, 40 softer.
  **Normal offset**: each map is looked up a little way out from the surface, along its normal, by 2 to 5 of that map's
  texels (more where the sun grazes): with a low sun the far map's 15 cm texels shadowed the ground they covered, and
  everything past the near map's edge darkened, a lit square round you (the user's screenshot, November 2026).
  **Shade on things**: the ground is lit by how it faces the sun (so ground turned away is skipped), but the game
  lights characters and things evenly all round, so a character's back in a tree's shade went unshaded (seen in the
  game); a surface facing sideways or down (`upView`, c20, the view's up) now loses at least 0.4 of the sun's share in
  shadow whichever way it faces. Seen: the character darker in the pines' shade; dusk hillsides unchanged.
  **Drawn again only when needed** (the user, November 2026: 20 to 35 fps in Hebian-to's square, settings lowered
  or not): every frame replayed about 3,100 of the game's draws into the far map (at 516 m), 560 into the near one and
  1,296 into three lamps' cube maps, each draw seven Direct3D calls, with every draw within the far map's reach noted
  too. Now the near map is drawn again every second frame or 2 m moved, the far map every 30 frames, 3% of its reach
  moved or the sun a third of a degree (`MapStale`, `FarStale`), another landblock always; between, each keeps the
  matrix it was drawn with. The noting area for the next frame (`FollowSunShadows`) reaches the far map only when it
  will be drawn then (`farNoted`), else the near map's 80 m and the furthest shadowed lamp's casters
  (`lampShadowArea`). One kept lamp map is drawn again a frame, not three (`LampRedrawsAFrame`). A replayed draw sets
  only the layout, buffers, texture and cut-out flag that differ from the last one's (`Ui/ReplayState.cs`). The
  close map's count in the 30-second report was never reset (it was a total since the start). Untested in the game
  when written.
## Gotchas, each found the hard way

- **Decal must be 2.9.8.x.** Decal 3.0 (2.9.7.5) runs plugins on .NET 2.0 and says "Failed to register plugin" for a net48
  dll. The agent's title bar shows the version.
- **Decal registers a .NET plugin by the assembly's `[Guid]`**, under `HKLM\SOFTWARE\WOW6432Node\Decal\Plugins\{guid}`, with
  `Object` = the plugin class's full name. Give every plugin (and loader) its own guid in `Properties/AssemblyInfo.cs`:
  never reuse Daralet Admin's, or the two can't be installed side by side.
- **A hot-reloaded plugin isn't registered with Decal**, so `PluginBase.Host`, `Core` and the base events
  (`ServerDispatch`...) never work for it. Use `CoreManager.Current` everywhere, and
  `CoreManager.Current.EchoFilter.ServerDispatch` for raw network messages. `CharacterFilter.LoginComplete` has already
  fired when a reload starts, so check `CharacterFilter.LoginStatus == 3` instead.
- **A plugin running in the game locks `bin\*.dll`** (acclient and DenAgent). To check a build while it runs, build to
  another folder (`-p:OutputPath=...`), or develop with the loader.
- **The loader must build the same every time.** The game holds `bin\Horizon.Loader.dll` open, and the SDK writes
  the git commit into a build's version and pdb by default, so after every commit Visual Studio couldn't copy the
  rebuilt loader over it (MSB3027, "locked by acclient.exe"). The loader's csproj turns both off
  (`IncludeSourceRevisionInInformationalVersion`, `EnableSourceLink`).
- **Piping `dotnet build`/`dotnet test` into `grep` can hang forever**: MSBuild's reused worker nodes keep the pipe open
  after the build ends. Pass `-nodeReuse:false` (and set `MSBUILDDISABLENODEREUSE=1`), or write to a log file and grep that.
- **Unit tests (net48 MSTest) can't load Decal.Adapter.** Keep Decal types out of anything tested: for example, pass
  logging in as `Action<string>` instead of calling `PluginCore.Log`.
- **`ImGui.GetColorU32` crashes before an ImGui context exists**, so colours in static fields are packed by hand
  (`(a << 24) | (b << 16) | (g << 8) | r`).
- **A hot reload must leave the old copy inert.** The loader shuts the running plugin down and starts the new build in the
  same game. If any old window kept drawing, it kept asking the server for data while its chat filter was gone, and the
  `DAP1|` replies flooded the chat box. So shutdown disposes each piece on its own (`PluginCore.ShutdownStep`,
  `PlayerHud.Dispose`), and a disposed feed or window does nothing (`PlayerFeed.Request`, `FloatingWindow.Hud_OnRender`).
- **Every hot reload stays in memory for good.** .NET Framework can't unload an assembly, so each
  `Assembly.Load(bytes)` keeps that 5 MB build for the rest of the game, along with anything its static fields still
  point at. A game reloaded 14 times ran out of memory on the 15th (an OutOfMemoryException in the loader's
  `Assembly.Load`, November 2026). So a static that holds the feed, settings, icons or a window is set back to null at
  shutdown (`Appraisal.Stop`, `PanelDrop.Stop`), and the game wants restarting every ten or so reloads. Players don't
  have the loader, so this is development only.
- **UtilityBelt can start without the portal dat.** It opens `client_cell_1.dat`, then `client_portal.dat`, by relative path
  from the game's working folder, and stops at the first that fails. ShadowLauncher starts each game in its own
  `AppData\Local\ShadowLauncher\Instances\<id>` folder of that server's dats (hard links into `DatSets`, or another dat
  folder), and a fresh cell dat there left `UBService.PortalDat` null: every `GetIconTexture` threw a
  NullReferenceException. `ItemIcons.EnsurePortalDat` opens `client_portal.dat` from the same working folder (so it's still
  that server's dats, custom icons and all) and gives it to UtilityBelt, by reflection, since `PortalDatDatabase` is in
  `UtilityBelt.Common`, which the plugin doesn't reference.
- **Decal's hooks crash the game before login is over.** `Actions.Heading`, `Landcell`, `LocationX`, `CurrentSelection`,
  `ChatState` and the like read the player straight from the game's memory, which isn't there at character select or
  while logging in: an AccessViolationException, which no catch stops, and acclient dies (seen from the Map window
  drawing during login, `ACHooksClass.get_HeadingDegrees` in the Windows event log). Nothing reads one until
  `CharacterFilter.LoginStatus == 3`: `FloatingWindow` stays hidden until then, `SelfPosition` gives nothing, and
  `Hotkeys.ForTheGame` and `GameDrag` check first. The main window draws at character select, so a tab mustn't read them
  before the feed is connected (it isn't until login).
- **Putting ImGui's cursor back after drawing out of place asserts** ("Code uses SetCursorPos()/SetCursorScreenPos() to
  extend window/parent boundaries"), in a dialog over the game, if nothing is drawn after it. Move it to one item gap
  above where it was and draw `ImGui.Dummy(Vector2.Zero)`, which lands it there (`CombatLogTab.DrawCopy`). The same goes
  for the end of a group or child: `GamePanelArt.Centred` put the cursor back after its text even when the text had left
  it there already, and the Vendor window, whose picked line ends a group with two, stopped the game every time it
  opened. It now only moves it when it isn't there. It still puts it back where the line began, so text centred beside
  something (the Vendor window's picked line, beside the kind menu) goes in a group of its own, or what follows starts
  mid-row. And `SameLine(x)` inside a group adds the group's offset to an x that has it already: `GamePanelArt.LeftRight`
  placed its value so, a group's width too far, which pushed the Vendor window's Buy and Sell buttons out of the window;
  it now goes by the distance from the label (`SameLine(0, spacing)`).
- **A button drawn only while `IsWindowHovered()` never gets clicked.** A pressed button is an active item, which that says
  blocks the window, so the button goes before its release counts. Pass `AllowWhenBlockedByActiveItem` (`CombatLogTab.DrawCopy`).
  But never `AllowWhenDisabled` to `IsWindowHovered`: it's an item's flag only, and ImGui asserts ("Invalid flags for
  IsWindowHovered()!", the XP bar's side grips' first build). A window's hover doesn't care whether its items are
  disabled.
- **ImGui.NET's `ImGuiSizeCallback` crashes the game.** The delegate type has no `UnmanagedFunctionPointer(Cdecl)`, so on
  x86 .NET makes it stdcall, and cimgui calls it cdecl: the argument came off the stack twice, and the game died in
  `igBegin` (an AccessViolationException, cimgui.dll in the Windows event log) the first frame a window drew with
  `ImGui.SetNextWindowSizeConstraints(..., callback)`. Call cimgui's own function with a delegate type of your own marked
  cdecl (`FloatingWindow.SetSizeConstraints`), and check any other ImGui.NET callback type the same way first.
- **Every Decal event handler and render callback is wrapped in try/catch.** An exception escaping into Decal can crash the
  client.
- **MSBuild takes a dotted name for a language.** `shading.ps.cso` went into a satellite `ps\Horizon.resources.dll`
  (Pashto) instead of the plugin, and `GetManifestResourceStream` found nothing: embedded resources whose names have
  a second dot need `WithCulture="false"` (the shaders' item in the csproj).
- **Never keep a Direct3D state block from one frame to the next.** Direct3D 9 won't reset the device while one lives,
  and the game resets it on exiting (and on alt-tab in full screen): the billboards' kept one hung the game on a
  restart (November 2026), its last frame the moment the exit began, and nothing outside could end it (the game runs
  as administrator). The billboards and the depth shading make theirs each frame and let it go after (`savedState`),
  and anything in the default pool goes on UtilityBelt's `OnDestroyTextures` (see Shading from depth). A borderless
  window never resets, which is why it went unseen. The test step `kill` ends the game from inside, for one that's
  stuck but still drawing; a frozen one has to be closed by hand.
- **JSON:** System.Text.Json writes dictionaries as objects; `DataContractJsonSerializer` only reads them with
  `UseSimpleDictionaryFormat = true` (see `Feed/Json.cs`).

## Conventions

- **License:** GPL-3.0 or later (`LICENSE`), with `NOTICE` adding a section 7 permission to combine it with Decal,
  UtilityBelt.Service, Managed DirectX and the client. Code copied in from elsewhere needs a GPL-compatible license.
- Braces on every `if`, even one-line bodies.
- One type per file. A big class is split into partial files by topic, named `Class.Topic.cs`; look for a class's other
  parts before adding to its main file. Fields go at the top of the class, and lines stay under about 125 characters.
- Every Decal event handler and render callback is wrapped in try/catch; anything new that runs every frame is timed with
  `HitchLog.Measure`.
- Code comments explain why, in plain words; tooltips (`Widgets.Tip`) on every control say what it does.
- The user commits and pushes with GitHub Desktop: leave changes staged (`git add`), don't commit.
