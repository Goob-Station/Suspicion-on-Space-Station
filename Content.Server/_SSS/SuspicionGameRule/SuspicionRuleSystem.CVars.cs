using Content.Shared.CCVar;

namespace Content.Server._SSS.SuspicionGameRule;

public sealed partial class SuspicionRuleSystem
{
    private void InitializeCVars()
    {
        Subs.CVar(_cfg, CCVars.SSSTraitorPercentage, f => _traitorPercentage = f, true);
        Subs.CVar(_cfg, CCVars.SSSDetectivePercentage, f => _detectivePercentage = f, true);
        Subs.CVar(_cfg, CCVars.SSSPreparingDuration, i => _preparingDuration = i, true);
        Subs.CVar(_cfg, CCVars.SSSRoundDuration, i => _roundDuration = i, true);
        Subs.CVar(_cfg, CCVars.SSSTimeAddedPerKill, i => _timeAddedPerKill = i, true);
        Subs.CVar(_cfg, CCVars.SSSPostRoundDuration, i => _postRoundDuration = i, true);
    }

    private float _traitorPercentage = 0.25f;
    private float _detectivePercentage = 0.25f;
    private int _preparingDuration = 30;
    private int _roundDuration = 480;
    private int _timeAddedPerKill = 30;
    private int _postRoundDuration = 30;
}
