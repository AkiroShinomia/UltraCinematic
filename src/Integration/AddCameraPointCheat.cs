using UltraCinematic.Configuration;

namespace UltraCinematic.Integration
{
    internal sealed class AddCameraPointCheat : CinematicCheatBase
    {
        public override string LongName => UiText.T("Add Camera Point", "Добавить точку камеры");
        public override string Identifier => "ultracinematic.add-point";
        public override string ButtonEnabledOverride => UiText.T("ADD", "ДОБАВИТЬ");
        public override string ButtonDisabledOverride => UiText.T("ADD", "ДОБАВИТЬ");
        public override bool IsActive => Controller != null && Controller.EditModeEnabled;
        public override void Enable(CheatsManager manager) => Controller?.AddCameraPoint();
        public override void Disable() => Controller?.AddCameraPoint();
    }
}
