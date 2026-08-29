using UltraCinematic.Core;

namespace UltraCinematic.Integration
{
    internal sealed class StartCinematicCheat : CinematicCheatBase
    {
        public override string LongName => "Start Cinematic";
        public override string Identifier => "ultracinematic.play";
        public override string ButtonEnabledOverride => Controller != null && Controller.PlaybackActive ? "STOP" : "PLAY";
        public override string ButtonDisabledOverride => "PLAY";
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
