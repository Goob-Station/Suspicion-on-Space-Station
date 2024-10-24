using Content.Shared.Roles;
using Robust.Shared.Serialization;

namespace Content.Shared._SSS.SuspicionGameRule.Components;

[RegisterComponent]
public sealed partial class SuspicionRoleComponent : BaseMindRoleComponent
{
    [ViewVariables]
    public SuspicionRole Role { get; set; } = SuspicionRole.Pending;
}

public static class SusRoleExtensions
{
    public static string GetRoleColor(this SuspicionRole role)
    {
        return role switch
        {
            SuspicionRole.Traitor => "red",
            SuspicionRole.Detective => "blue",
            SuspicionRole.Innocent => "green",
            _ => "white",
        };
    }
}

[Serializable, NetSerializable]
public enum SuspicionRole
{
    Pending,

    Traitor,
    Detective,
    Innocent,
}
