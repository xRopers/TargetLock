# TargetLock

A Dalamud plugin for FINAL FANTASY XIV: the game's own lock-on, without the backpedalling.

With the game's lock-on, your character is forced to face the target, so moving away from it is a slow backpedal and left/right become strafes. TargetLock keeps the game's lock-on exactly as it is (its camera, framing and target switching) but lets you move as if you weren't locked on: run and turn in any direction at full speed.

## Install

1. In game, open `/xlsettings` → **Experimental**.
2. Under **Custom Plugin Repositories**, add:
   ```
   https://raw.githubusercontent.com/xRopers/TargetLock/main/repo.json
   ```
   then tick **Enabled** and click **Save and Close**.
3. Open `/xlplugins`, search for **TargetLock** and install it.

Then use the game's lock-on as usual.

## Commands

| Command | Effect |
| --- | --- |
| `/targetlock` | Open settings |
| `/targetlock on` / `off` | Turn normal movement during lock-on on or off |

## How it works

The game asks one TargetSystem function whether lock-on is active. The camera uses the answer to switch into the lock-on camera; the player movement update uses it to turn your inputs into strafes and backwards steps and to apply the slow backpedal speed. TargetLock hooks both: while the movement update runs, that check answers "not locked on", and everything else (including the camera) still sees the real lock-on.

The lock-on check is located through [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs), which Dalamud updates for each game patch: it is the function called by `Camera.DetermineControlMode` that reads `TargetSystem.SoftTarget` and `TargetSystem.Target` at ClientStructs' current offsets, with a byte signature as fallback. The movement update isn't in ClientStructs yet, so it is found by a signature with all offsets wildcarded. The settings window shows how each was found.

## Building

Requirements:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- XIVLauncher with Dalamud installed and run at least once, so the development files exist in `%AppData%\XIVLauncher\addon\Hooks\dev\`. Set the `DALAMUD_HOME` environment variable to build against Dalamud somewhere else.

```
dotnet build TargetLock.sln -c Release
```

The build writes `TargetLock/bin/x64/Release/TargetLock.dll` with its manifest, plus a packaged `TargetLock/bin/x64/Release/TargetLock/latest.zip`. To load your own build, add the full path to the DLL under Dev Plugin Locations in `/xlsettings` → **Experimental**.

## Project layout

```
TargetLock/
  Plugin.cs                       Entry point: Dalamud services, settings window and /targetlock
  Configuration.cs                Saved settings
  Game/LockOnCheckResolver.cs     Finds the game's lock-on check through ClientStructs
  Services/GameLockOnMovement.cs  The hooks that remove the lock-on backpedal
  Windows/ConfigWindow.cs         Settings window
images/                           Plugin icon (referenced by the manifest's IconUrl)
scripts/                          Icon generator and the repo.json updater used by the release workflow
repo.json                         Custom plugin repository feed
```

## Releasing

Push a version tag. The [Release workflow](.github/workflows/release.yml) builds the plugin, publishes `latest.zip` as a GitHub release and updates `repo.json` on `main`:

```bash
git tag v0.2.0
git push origin v0.2.0
```

## License

TargetLock is licensed under the [GNU Affero General Public License v3.0 or later](LICENSE).
