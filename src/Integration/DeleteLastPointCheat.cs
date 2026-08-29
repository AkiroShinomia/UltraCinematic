namespace UltraCinematic.Integration
{
    internal sealed class DeleteLastPointCheat : CinematicCheatBase
    {
        public override string LongName => "Delete Last Point";
        public override string Identifier => "ultracinematic.delete-last-point";
        public override string ButtonEnabledOverride => "DELETE";
        public override string ButtonDisabledOverride => "DELETE";
        public override bool IsActive => Controller != null && Controller.EditModeEnabled;
        public override void Enable(CheatsManager manager) => Controller?.DeleteLastPoint();
        public override void Disable() => Controller?.DeleteLastPoint();
    }
}
