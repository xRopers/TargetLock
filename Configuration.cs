using System;
using Dalamud.Configuration;

namespace TargetLock;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;

    /// <summary>
    /// Use the game's own lock-on, but move normally: no forced facing, strafing or backpedalling.
    /// See <see cref="GameLockOnMovement"/>.
    /// </summary>
    public bool NoBackpedalOnGameLockOn { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
