using UltraCinematic.Configuration;

namespace UltraCinematic.Integration
{
    internal sealed class DeleteLastPointCheat : CinematicCheatBase
    {
        public override string LongName => UiText.T("Delete Last Point", "Удалить последнюю точку");
        public override string Identifier => "ultracinematic.delete-last-point";
        public override string ButtonEnabledOverride => UiText.T("DELETE", "УДАЛИТЬ");
        public override string ButtonDisabledOverride => UiText.T("DELETE", "УДАЛИТЬ");
        public override bool IsActive => Controller != null && Controller.EditModeEnabled;
        public override void Enable(CheatsManager manager) => Controller?.DeleteLastPoint();
        public override void Disable() => Controller?.DeleteLastPoint();
    }
}
