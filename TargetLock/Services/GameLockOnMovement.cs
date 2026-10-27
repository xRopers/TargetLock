using System;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using TargetLock.Game;

namespace TargetLock.Services;

/// <summary>
/// Keeps the game's own lock-on camera but removes its backpedalling.
///
/// The game decides "lock-on is active" in one TargetSystem function (sub_1406276A0 on 2026.10.06: a lock-on flag
/// bit at TargetSystem+0x134 for Target/SoftTarget, while the normal camera is active). The camera asks it to switch
/// into lock-on mode, and the player movement code (MoveControllerSubMemberForMine.vf1 and the ~40 helpers it
/// calls) asks it to turn left/right into strafes, back into a backwards step, and to flag that movement as
/// backward/strafe, which is the slow backpedal speed.
///
/// This hooks both: while the movement update runs, "is lock-on active" answers no, so you run and turn exactly as
/// without lock-on. Everything else, including the camera, still sees the real lock-on.
/// </summary>
public sealed class GameLockOnMovement : IDisposable
{
    // MoveControllerSubMemberForMine.vf1, the local player's per-frame movement update. Not in ClientStructs, so
    // found by its opening instructions (all offsets wildcarded).
    private const string MovementUpdateSignature =
        "40 55 53 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 83 79 ?? 00 48 8B D9 75 ?? 32 C0";

    private delegate byte IsLockOnDelegate(nint targetSystem);
    private delegate byte MovementUpdateDelegate(nint moveController);

    private readonly Configuration configuration;
    private readonly Hook<MovementUpdateDelegate>? movementUpdateHook;
    private Hook<IsLockOnDelegate>? isLockOnHook;
    private bool inMovementUpdate;

    public bool HooksActive => isLockOnHook is { IsEnabled: true } && movementUpdateHook is { IsEnabled: true };

    /// <summary>How the lock-on check was found, shown in the settings window.</summary>
    public string LockOnCheckSource { get; private set; } = "waiting for the game camera";

    public string MovementUpdateSource { get; } = "not found";

    public GameLockOnMovement(Configuration configuration)
    {
        this.configuration = configuration;

        try
        {
            movementUpdateHook = Plugin.GameInterop.HookFromSignature<MovementUpdateDelegate>(MovementUpdateSignature, MovementUpdateDetour);
            movementUpdateHook.Enable();
            MovementUpdateSource = "signature";
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Could not find the movement update; the game may have been patched.");
        }

        // The lock-on check is found through the game camera, which may not exist yet (title screen).
        Plugin.Framework.Update += TryHookLockOnCheck;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= TryHookLockOnCheck;
        movementUpdateHook?.Dispose();
        isLockOnHook?.Dispose();
    }

    private void TryHookLockOnCheck(IFramework framework)
    {
        var (address, source) = LockOnCheckResolver.Resolve();
        if (address == 0 && source == LockOnCheckResolver.NoCamera)
            return; // Try again next frame.

        Plugin.Framework.Update -= TryHookLockOnCheck;
        LockOnCheckSource = source;
        if (address == 0)
        {
            Plugin.Log.Error($"Could not find the game's lock-on check ({source}); the game may have been patched.");
            return;
        }

        try
        {
            isLockOnHook = Plugin.GameInterop.HookFromAddress<IsLockOnDelegate>(address, IsLockOnDetour);
            isLockOnHook.Enable();
            Plugin.Log.Information($"Hooked the lock-on check at 0x{address:X} ({source})");
        }
        catch (Exception ex)
        {
            LockOnCheckSource = "hook failed";
            Plugin.Log.Error(ex, "Could not hook the game's lock-on check.");
        }
    }

    private byte MovementUpdateDetour(nint moveController)
    {
        inMovementUpdate = true;
        try
        {
            return movementUpdateHook!.Original(moveController);
        }
        finally
        {
            inMovementUpdate = false;
        }
    }

    private byte IsLockOnDetour(nint targetSystem)
    {
        if (inMovementUpdate && configuration.NoBackpedalOnGameLockOn)
            return 0;

        return isLockOnHook!.Original(targetSystem);
    }
}
