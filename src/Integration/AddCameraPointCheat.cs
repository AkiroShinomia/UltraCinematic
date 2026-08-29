namespace UltraCinematic.Integration
{
    internal sealed class AddCameraPointCheat : CinematicCheatBase
    {
        public override string LongName => "Add Camera Point";
        public override string Identifier => "ultracinematic.add-point";
        public override string ButtonEnabledOverride => "ADD";
        public override string ButtonDisabledOverride => "ADD";
        public override bool IsActive => Controller != null && Controller.EditModeEnabled;
        public override void Enable(CheatsManager manager) => Controller?.AddCameraPoint();
        public override void Disable() => Controller?.AddCameraPoint();
    }
}
