namespace UltraCinematic.Integration
{
    internal sealed class PauseGameCheat : CinematicCheatBase
    {
        public override string LongName => "Pause Game";
        public override string Identifier => "ultracinematic.pause-game";
        public override string ButtonEnabledOverride => "ENABLED";
        public override string ButtonDisabledOverride => "DISABLED";
        public override bool IsActive => Controller != null && Controller.PhotoPauseActive;

        public override void Enable(CheatsManager manager) => Controller?.EnablePhotoPause();

        public override void Disable() => Controller?.DisablePhotoPause();
    }
}
