namespace Content.Shared._SSS.SuspicionGameRule.Components;

[RegisterComponent]
public sealed partial class SuspicionRadarComponent : Component
{
    [DataField]
    public bool ShowTraitors { get; set; } = true;
}
