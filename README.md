# Horizon

A Decal plugin for Asheron's Call that extends draw distance and adds real lighting, shadows, water and grass, built from
the game's own art.

Everything Horizon adds takes its look from the game: its own textures, models, light and fog. Each feature is a switch
of its own and starts off, so the game looks as it always has until you turn something on.

## What it does

- **View distance:** land drawn far past the game's own landscape, distant trees as 3D models and billboards, towns
  and buildings that stay in sight, and thinner fog.
- **Lighting:** shading from depth, shadows from the sun, light and shadows from lamps, fires and lamp posts, and a
  darker night.
- **Water:** reflections on the game's own water.
- **Grass:** grass and reeds growing from the ground, coloured by the ground they grow on.
- **Image:** anti-aliasing, sharpening and a soft glow on bright things.
- **Budget:** presets, and two bars that estimate what your settings cost in memory and frame rate before you apply them.
- **Frame Rate window:** the frame rate and the game's memory, with a log of each change you try.

Type `/horizon` in chat to open the window.

## Requirements

- **Decal 2.9.8.x.** The Decal agent's title bar shows its version.
- **[UtilityBelt.Service 2.2.12](https://gitlab.com/utilitybelt/utilitybelt.service/-/releases/v2.2.12).** Horizon draws
  its window with UtilityBelt.Service, which is a separate install from UtilityBelt itself.
- **.NET Framework 4.8.**

## Installing

Close the game and the Decal agent, then download and run `Horizon-Setup-<version>.exe` from the
[latest release](https://github.com/daralet-ac/Horizon/releases/latest). It installs Horizon, ticks it under Plugins in
the Decal agent, and installs UtilityBelt.Service if you don't have it. To update, run the newer installer the same way:
Horizon says in chat when a new version is out, and your settings are kept.

## Installing by hand

1. **Install UtilityBelt.Service**, even if you already have UtilityBelt: UtilityBelt's installer doesn't include it.
   Close the game and the Decal agent, then run
   [UtilityBelt.Service-Installer-2.2.12.exe](https://gitlab.com/utilitybelt/utilitybelt.service/-/releases/v2.2.12/downloads/packages/UtilityBelt.Service-Installer-2.2.12.exe).
2. **Put `Horizon.dll` somewhere it can stay**, for example `C:\Games\Decal Plugins\Horizon\`. Decal loads the file from
   wherever you put it, so don't leave it in your Downloads folder.
3. **Add it to Decal.** In the Decal agent, click **Add**, then **Browse**, and pick `Horizon.dll`.
4. **Make sure Horizon is ticked** under **Plugins** in the Decal agent, then start the game.

If you were using Daralet Toolkit's Graphics tab, Horizon brings your graphics settings over the first time it starts.

## Building

```bash
dotnet build Horizon.sln -c Release
dotnet test tests/Horizon.Tests/Horizon.Tests.csproj
```

The build goes to `bin\`. For development, enable `Horizon.Loader.dll` under Decal's Network Filters instead of the plugin:
it reloads `Horizon.dll` every time you rebuild.

## License

GPL-3.0 or later, with an added permission to combine it with Decal, UtilityBelt.Service, Managed DirectX and the
client. See [LICENSE](LICENSE) and [NOTICE](NOTICE).
