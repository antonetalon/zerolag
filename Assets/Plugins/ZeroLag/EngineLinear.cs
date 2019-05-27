using FixMath.NET;

namespace ZeroLag
{
    public class EngineLinear<T, S> : Engine<T, S> where T : PredictableModel<T, S> where S : IPredictableSettings<T, S>, new()
    {
        #region I can be created for server and single player
        int delayFromPresentStep;
        private EngineLinear(S settings, bool simulate, bool fixedDt, int delayFromPresentStep, string debugTag)
            : base(settings, simulate, 0, fixedDt, debugTag)
            => this.delayFromPresentStep = delayFromPresentStep;
        public static EngineLinear<T,S> CreateServer(S settings, bool simulate, int delayFromPresentStep, string debugTag) 
            => new EngineLinear<T, S>(settings, simulate, true, delayFromPresentStep, debugTag);
        public static EngineLinear<T, S> CreateSinglePlayer(S settings, string debugTag = null)
            => new EngineLinear<T, S>(settings, true, false, 0, debugTag);
        public static EngineLinear<T, S> CreateReplay(S settings, bool fixedDt, string debugTag = null)
            => new EngineLinear<T, S>(settings, true, fixedDt, 0, debugTag);
        #endregion

        #region I update to time linearly, do step each time present time advances.
        int stepToUpdateTo { get { return presentStep - delayFromPresentStep - 1; } }
        public override int presentStepUnclamped => fixedDt ? presentStepForFixedDt : playedGameReplay.stepDtsCount;
        public override T viewModel => cursor;
        protected override void OnPresentTimeAdded(Fix64 dt)
        {
            // Add steps with fixed dt or with free dt.
            if (!fixedDt)
                playedGameReplay.AddStep(dt);
            else
            {
                while (presentTime - playedGameReplay.savedTime >= settings.fixedDt)
                    playedGameReplay.AddStep(settings.fixedDt);
            }
            // Update cursor to needed time.
            if (simulate)
                MoveCursorForwardTo(stepToUpdateTo);
        }
        protected override bool replicationAllowed => false;
        protected override int debugMaxStepsToCatchUpLastActualStepToPresent => 1;
        #endregion
    }
}