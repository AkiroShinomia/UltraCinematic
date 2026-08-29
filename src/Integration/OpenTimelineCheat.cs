using UltraCinematic.Configuration;

namespace UltraCinematic.Integration
{
    internal sealed class OpenTimelineCheat : CinematicCheatBase
    {
        public override string LongName => UiText.T("Open Timeline", "Открыть таймлайн");
        public override string Identifier => "ultracinematic.open-timeline";
        public override string ButtonEnabledOverride => UiText.T("OPEN", "ОТКРЫТЬ");
        public override string ButtonDisabledOverride => UiText.T("OPEN", "ОТКРЫТЬ");
        public override bool IsActive => Controller != null && Controller.EditModeEnabled;
        public override void Enable(CheatsManager manager) => Controller?.ToggleTimeline();
        public override void Disable() => Controller?.ToggleTimeline();
    }
}
