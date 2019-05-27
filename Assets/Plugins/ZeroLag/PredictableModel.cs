using System.Collections.Generic;
using FixMath.NET;

namespace ZeroLag
{
    public abstract partial class PredictableModel<T, S> where T : PredictableModel<T, S>
    {
        public abstract void UpdateStep(Fix64 dt, IEnumerable<ZeroLagCommand> consideredCommands); // Inc step, check that commands are from appropriate step and call Update.
        public abstract void Update(Fix64 dt, IEnumerable<ZeroLagCommand> consideredCommands); // Update game logic with every given command, dont inc curr step.
        public abstract T Copy();
        public abstract int step { get; }
        public abstract long CalculateHash();
    }
}