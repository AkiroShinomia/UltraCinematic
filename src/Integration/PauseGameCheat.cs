using UltraCinematic.Configuration;

namespace UltraCinematic.Integration
{
    internal sealed class PauseGameCheat : CinematicCheatBase
    {
        public override string LongName => UiText.T("Pause Game", "Пауза игры");
        public override string Identifier => "ultracinematic.pause-game";
        public override string ButtonEnabledOverride => UiText.T("ENABLED", "ВКЛЮЧЕНО");
        public override string ButtonDisabledOverride => UiText.T("DISABLED", "ВЫКЛЮЧЕНО");
        public override bool IsActive => Controller != null && Controller.PhotoPauseActive;

        public override void Enable(CheatsManager manager) => Controller?.TogglePhotoPause();

        public override void Disable() => Controller?.TogglePhotoPause();
    }
}
