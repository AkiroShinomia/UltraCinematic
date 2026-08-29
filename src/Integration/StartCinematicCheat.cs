using UltraCinematic.Core;
using UltraCinematic.Configuration;

namespace UltraCinematic.Integration
{
    internal sealed class StartCinematicCheat : CinematicCheatBase
    {
        public override string LongName => UiText.T("Start Cinematic", "Запустить синематик");
        public override string Identifier => "ultracinematic.play";
        public override string ButtonEnabledOverride => Controller != null && Controller.PlaybackActive ? UiText.T("STOP", "СТОП") : UiText.T("PLAY", "ЗАПУСК");
        public override string ButtonDisabledOverride => UiText.T("PLAY", "ЗАПУСК");
        public override bool IsActive => Controller != null && Controller.EditModeEnabled;
        public override void Enable(CheatsManager manager) => TogglePlayback();
        public override void Disable() => TogglePlayback();

        private void TogglePlayback()
        {
            if (Controller == null) return;
            if (Controller.PlaybackActive) Controller.StopPlayback();
            else Controller.StartPlayback();
        }
    }
}
