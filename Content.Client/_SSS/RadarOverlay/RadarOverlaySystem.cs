using Content.Shared._SSS.RadarOverlay;
using Robust.Client.Graphics;

namespace Content.Client._SSS.RadarOverlay;

public sealed class RadarOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    public RadarInfo[] RadarInfos = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<OnRadarOverlayToggledEvent>(OnOverlayToggled);
        SubscribeNetworkEvent<RadarOverlayUpdatedEvent>(OnOverlayUpdate);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayManager.RemoveOverlay<RadarOverlay>();
    }

    private void OnOverlayToggled(OnRadarOverlayToggledEvent ev)
    {
        if (ev.IsEnabled)
            _overlayManager.AddOverlay(new RadarOverlay());
        else
            _overlayManager.RemoveOverlay<RadarOverlay>();
    }

    private void OnOverlayUpdate(RadarOverlayUpdatedEvent ev)
    {
        RadarInfos = ev.RadarInfos;
    }
}
