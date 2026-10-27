using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace TargetLock.Windows;

public sealed class ConfigWindow : Window
{
    private const float WindowWidth = 420f;

    private static readonly Vector4 Muted = new(0.6f, 0.6f, 0.65f, 1f);
    private static readonly Vector4 OnColor = new(0.45f, 0.9f, 0.5f, 1f);
    private static readonly Vector4 Warning = new(1f, 0.45f, 0.45f, 1f);

    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base("TargetLock###TargetLockConfig", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(WindowWidth, 0f),
            MaximumSize = new Vector2(WindowWidth, 2000f),
        };
    }

    public override void Draw()
    {
        var config = plugin.Configuration;

        ImGui.Spacing();
        var enabled = config.NoBackpedalOnGameLockOn;
        if (ImGui.Checkbox("Move normally while using the game's lock-on", ref enabled))
        {
            config.NoBackpedalOnGameLockOn = enabled;
            config.Save();
        }

        ImGui.Spacing();
        ImGui.TextWrapped("Keeps the game's own lock-on camera, framing and target switching, but you run and turn in " +
                          "any direction at full speed instead of strafing and backpedalling.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(Muted, "Status");
        ImGui.SameLine();
        var lockOn = plugin.GameLockOn;
        if (!lockOn.HooksActive && lockOn.LockOnCheckSource.StartsWith("waiting", System.StringComparison.Ordinal))
            ImGui.TextColored(Muted, "starting (waiting for the game camera)");
        else if (!lockOn.HooksActive)
            ImGui.TextColored(Warning, "not available - the game may have updated. Check /xllog.");
        else if (config.NoBackpedalOnGameLockOn)
            ImGui.TextColored(OnColor, "active");
        else
            ImGui.TextColored(Muted, "off");

        ImGui.TextColored(Muted, "Lock-on check found via");
        ImGui.SameLine();
        ImGui.TextUnformatted(lockOn.LockOnCheckSource);
        ImGui.TextColored(Muted, "Movement update found via");
        ImGui.SameLine();
        ImGui.TextUnformatted(lockOn.MovementUpdateSource);
    }
}
