using Content.Server.Damage.Systems;
using Content.Shared.Damage;

namespace Content.Server.Damage.Components
{
    [Access(typeof(DamageOtherOnHitSystem))]
    [RegisterComponent]
    public sealed partial class DamageOtherOnHitComponent : Component
    {
        [DataField("ignoreResistances")]
        [ViewVariables(VVAccess.ReadWrite)]
        public bool IgnoreResistances = false;

        [DataField("damage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier Damage = default!;

        [DataField("damageMultiplierOverTime")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float DamageMultiplierOverTime = 1f;

        [DataField("timeTillMaxDamage")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float TimeTillMaxDamage = 0f;

        [ViewVariables(VVAccess.ReadOnly)]
        public TimeSpan TimeThrown = TimeSpan.Zero;

        [ViewVariables(VVAccess.ReadOnly)]
        public TimeSpan TimeToMaxDamage = TimeSpan.Zero;

    }
}
