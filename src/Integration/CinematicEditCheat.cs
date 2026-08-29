using UltraCinematic.Core;
using UltraCinematic.Configuration;

namespace UltraCinematic.Integration
{
    internal sealed class CinematicEditCheat : CinematicCheatBase
    {
        public override string LongName => UiText.T("Cinematic Edit Mode", "Режим синематика");
        public override string Identifier => "ultracinematic.edit-mode";
        public override string ButtonEnabledOverride => UiText.T("ENABLED", "ВКЛЮЧЕНО");
        public override string ButtonDisabledOverride => UiText.T("DISABLED", "ВЫКЛЮЧЕНО");
        public override bool IsActive => Controller != null && Controller.EditModeEnabled;

        public override void Enable(CheatsManager manager)
        {
            Controller?.EnableEditMode();
        }

        public override void Disable() => Controller?.DisableEditMode();
    }
}
