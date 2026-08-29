using System.Collections;
using UltraCinematic.Core;

namespace UltraCinematic.Integration
{
    internal abstract class CinematicCheatBase : ICheat
    {
        protected CinematicController Controller => UltraCinematicPlugin.Controller;
        public abstract string LongName { get; }
        public abstract string Identifier { get; }
        public abstract string ButtonEnabledOverride { get; }
        public abstract string ButtonDisabledOverride { get; }
        public string Icon => "";
        public bool DefaultState => false;
        public StatePersistenceMode PersistenceMode => StatePersistenceMode.NotPersistent;
        public abstract bool IsActive { get; }
        public abstract void Enable(CheatsManager manager);
        public abstract void Disable();
        public IEnumerator Coroutine(CheatsManager manager) { yield break; }
    }
}
