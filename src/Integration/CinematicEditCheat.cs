using UltraCinematic.Core;

namespace UltraCinematic.Integration
{
    internal sealed class CinematicEditCheat : CinematicCheatBase
    {
        public override string LongName => "Cinematic Edit Mode";
        public override string Identifier => "ultracinematic.edit-mode";
        public override string ButtonEnabledOverride => "ENABLED";
        public override string ButtonDisabledOverride => "DISABLED";
        public override bool IsActive => Controller != null && Controller.EditModeEnabled;

        public override void Enable(CheatsManager manager)
        {
            Controller?.EnableEditMode();
        }

        public override void Disable() => Controller?.DisableEditMode();
    }
}
