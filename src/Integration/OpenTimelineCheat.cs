namespace UltraCinematic.Integration
{
    internal sealed class OpenTimelineCheat : CinematicCheatBase
    {
        public override string LongName => "Open Timeline";
        public override string Identifier => "ultracinematic.open-timeline";
        public override string ButtonEnabledOverride => "OPEN";
        public override string ButtonDisabledOverride => "OPEN";
        public override bool IsActive => Controller != null && Controller.EditModeEnabled;
        public override void Enable(CheatsManager manager) => Controller?.ToggleTimeline();
        public override void Disable() => Controller?.ToggleTimeline();
    }
}
