using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace TargetLock.Game;

/// <summary>
/// Finds the TargetSystem "is lock-on active" function through ClientStructs, which Dalamud updates for each game
/// patch, instead of a hardcoded address:
///  - ClientStructs maintains Camera.DetermineControlMode (camera vfunc 17), which calls the check to decide
///    whether to switch into the lock-on camera.
///  - Of the functions it calls, the check is the one that starts by reading TargetSystem.SoftTarget and then
///    TargetSystem.Target, at the offsets ClientStructs currently gives for those fields:
///        mov rdx, [rcx+SoftTarget] ... cmp qword ptr [rcx+Target], 0
/// A byte signature is the fallback.
/// </summary>
public static unsafe class LockOnCheckResolver
{
    public const string NoCamera = "no game camera yet";

    // push rbx; sub rsp,20h; mov rdx,[rcx+SoftTarget]; xor bl,bl; test rdx,rdx; jz; movzx eax,byte [rcx+flags]; shr al,7
    private const string FallbackSignature =
        "40 53 48 83 EC 20 48 8B 91 ?? ?? ?? ?? 32 DB 48 85 D2 74 ?? 0F B6 81 ?? ?? ?? ?? C0 E8 07";

    private const int MaxCallerBytes = 0x800;  // How much of DetermineControlMode to look through.
    private const int CalleeHeadBytes = 0x30;  // How far into each callee to look for the field reads.

    public static (nint Address, string Source) Resolve()
    {
        var manager = CameraManager.Instance();
        if (manager == null || manager->Camera == null)
            return (0, NoCamera);

        var fromClientStructs = FindThroughDetermineControlMode(manager->Camera);
        if (fromClientStructs != 0)
            return (fromClientStructs, "ClientStructs (Camera.DetermineControlMode)");

        try
        {
            return (Plugin.SigScanner.ScanText(FallbackSignature), "signature");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Lock-on check signature not found");
            return (0, "not found");
        }
    }

    private static nint FindThroughDetermineControlMode(FFXIVClientStructs.FFXIV.Client.Game.Camera* camera)
    {
        var caller = (nint)camera->VirtualTable->DetermineControlMode;
        if (!IsInText(caller, MaxCallerBytes))
            return 0;

        var softTarget = (int)Marshal.OffsetOf<TargetSystem>(nameof(TargetSystem.SoftTarget));
        var target = (int)Marshal.OffsetOf<TargetSystem>(nameof(TargetSystem.Target));
        ReadOnlySpan<byte> readSoftTarget = [0x48, 0x8B, 0x91, .. BitConverter.GetBytes(softTarget)];    // mov rdx, [rcx+SoftTarget]
        ReadOnlySpan<byte> checkTarget = [0x48, 0x83, 0xB9, .. BitConverter.GetBytes(target), 0x00];     // cmp qword ptr [rcx+Target], 0

        var body = ReadCode(caller, MaxCallerBytes);
        for (var i = 0; i + 5 <= body.Length; i++)
        {
            if (body[i] != 0xE8)
                continue; // call rel32

            var callee = caller + i + 5 + BitConverter.ToInt32(body.Slice(i + 1, 4));
            if (!IsInText(callee, CalleeHeadBytes))
                continue;

            var head = ReadCode(callee, CalleeHeadBytes);
            var hard = head.IndexOf(checkTarget);
            if (hard < 0)
                continue;

            // A hook (ours from before a plugin reload, or another plugin's) puts a jump over the first bytes, which
            // can cover the SoftTarget read; the Target check further in is still there.
            var soft = head.IndexOf(readSoftTarget);
            if ((soft >= 0 && soft < hard) || (soft < 0 && StartsWithHookJump(head)))
                return callee;
        }

        return 0;
    }

    private static bool StartsWithHookJump(ReadOnlySpan<byte> code) =>
        code[0] == 0xE9 || (code[0] == 0xFF && code[1] == 0x25); // jmp rel32 / jmp [rip+x]

    private static ReadOnlySpan<byte> ReadCode(nint address, int length) => new((void*)address, length);

    private static bool IsInText(nint address, int length)
    {
        var start = Plugin.SigScanner.TextSectionBase;
        return address >= start && address + length <= start + Plugin.SigScanner.TextSectionSize;
    }
}
