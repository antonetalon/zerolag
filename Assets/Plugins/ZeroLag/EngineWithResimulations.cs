
using FixMath.NET;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroLag
{
    public class EngineWithResimulations<T, S> : Engine<T, S> where T : PredictableModel<T, S> where S : IPredictableSettings<T, S>, new()
    {
        #region Start/stop match
        public static int GetDenseSnapshotsCount(Fix64 maxServerAllowedLag, Fix64 stepDuration)
        {
            return Math.Max(1, (int)(maxServerAllowedLag * 2 / stepDuration));
        }
        public static int GetSparseSnapshotsFrequency(Fix64 maxServerAllowedLag, Fix64 stepDuration)
        {
            return GetDenseSnapshotsCount(maxServerAllowedLag, stepDuration);
        }
        public static int GetSparseSnapshotsCount()
        {
            const int minSparseModels = 10; // There should be enough sparse models for all cases.
            return minSparseModels;
        }

        public EngineWithResimulations(S settings, bool simulate, string debugTag)
            : this(settings, simulate, GetDenseSnapshotsCount(settings.maxAllowedLag, settings.fixedDt),
                  GetSparseSnapshotsCount(), GetSparseSnapshotsFrequency(settings.maxAllowedLag, settings.fixedDt), debugTag)
        { }
        
        public EngineWithResimulations(S settings, bool simulate, int savedModelsDenseCount, int savedModelsSparseCount, int sparsePeriod, string debugTag) 
            : base(settings, simulate, savedModelsDenseCount + savedModelsSparseCount, true, debugTag)
        {
            InitHistory(savedModelsDenseCount, savedModelsSparseCount, sparsePeriod);
            InitTimeouts();
            InitViewModel();
        }
        protected override void OnPresentTimeAdded(Fix64 dt)
        {
            TryConsumeTimeouts();
            if (simulate)
            {
                UpdateSingleThreadedWave(dt);
                UpdateViewModelLinearly(dt);
            }
        }
        #endregion

        #region I save history for rollbacks
        /// <summary>
        /// Each model in range [lastModelsShift-lastDenseModelsMaxCount+1; lastModelsShift].
        /// </summary>
        private List<int> lastDenseModelsSteps;
        private List<T> lastDenseModels { get; set; }
        private int lastDenseModelsMaxCount;
        /// <summary>
        /// Models earier than earliest in lastDenseModels saved sparsely.
        /// </summary>
        private List<int> lastSparseModelSteps;
        private List<T> lastSparseModels { get; set; }
        /// <summary>
        /// Sparse model saving restricted by this count.
        /// </summary>
        private int lastSparseModelsMaxCount;
        /// <summary>
        /// Sparse models only saved if their ind%lastSparseModelsPeriod==0.
        /// </summary>
        private int sparsePeriod;
        /// <summary>
        /// Starting model. Ensures resimulation possible.
        /// </summary>
        protected T zeroModel { get; private set; }
        void InitHistory(int savedModelsDenseCount, int savedModelsSparseCount, int sparsePeriod)
        {
            this.lastDenseModelsMaxCount = savedModelsDenseCount;
            this.lastSparseModelsMaxCount = savedModelsSparseCount;
            this.sparsePeriod = sparsePeriod;
            lastDenseModelsSteps = new List<int>();
            lastDenseModels = new List<T>();
            lastSparseModelSteps = new List<int>();
            lastSparseModels = new List<T>();
            zeroModel = GetModelFromPool();
        }
        protected override void OnModelActualized(int step, T stepModel)
        {
            ClearTooLateModels(step);

            // Save in dense models.
            T newDenseModel = GetModelFromPool();
            newDenseModel.UpdateFrom(stepModel);
            lastDenseModelsSteps.Add(step);
            lastDenseModels.Add(newDenseModel);

            // Save in sparse models.
            if (step % sparsePeriod == 0)
            {
                T newSparseModel = GetModelFromPool();
                newSparseModel.UpdateFrom(stepModel);
                lastSparseModelSteps.Add(step);
                lastSparseModels.Add(newSparseModel);
            }

            ClearTooOldModels(step);
        }
        private void ClearTooLateModels(int step)
        {
            ClearModelsLater(step - 1, lastDenseModelsSteps, lastDenseModels);
            ClearModelsLater(step - 1, lastSparseModelSteps, lastSparseModels);
        }
        private void ClearTooOldModels(int step)
        {
            ClearModelsEarlier(step - lastDenseModelsMaxCount + 1, lastDenseModelsSteps, lastDenseModels);
            ClearModelsEarlier(step - lastSparseModelsMaxCount * sparsePeriod + 1, lastSparseModelSteps, lastSparseModels);
        }
        private void ClearModelsLater(int step, List<int> steps, List<T> models)
        {
            // Clear array form models later than needed.
            for (int i = 0; i < models.Count; i++)
            {
                int currInd = steps[i];
                if (currInd > step)
                {
                    // Remove all later models from array.
                    int countToDelete = models.Count - i;
                    RemoveModels(i, countToDelete, steps, models);
                    return;
                }
            }
        }
        private void ClearModelsEarlier(int step, List<int> steps, List<T> models)
        {
            // Clear array form models earlier than needed.
            for (int i = models.Count - 1; i >= 0; i--)
            {
                int currInd = steps[i];
                if (currInd < step)
                {
                    // Remove all earlier models from array.
                    int countToDelete = i + 1;
                    RemoveModels(0, countToDelete, steps, models);
                    return;
                }
            }
        }
        void RemoveModels(int startInd, int countToDelete, List<int> steps, List<T> models)
        {
            for (int i = startInd; i < startInd + countToDelete; i++)
                ReturnToPool(models[i]);
            steps.RemoveRange(startInd, countToDelete);
            models.RemoveRange(startInd, countToDelete);
        }
        protected void GetLastActual(out int lastActualModelStep, out T lastActualModel)
        {
            // Try to find model in dense array.
            if (lastDenseModelsSteps.Count > 0 && lastActualStep >= lastDenseModelsSteps[0])
            {
                GetLastActual(out lastActualModelStep, out lastActualModel, lastDenseModelsSteps, lastDenseModels);
                return;
            }
            // Try to find in sparse array.
            if (lastSparseModelSteps.Count > 0 && lastActualStep >= lastSparseModelSteps[0])
            {
                GetLastActual(out lastActualModelStep, out lastActualModel, lastSparseModelSteps, lastSparseModels);
                return;
            }
            // if others failed, fallback to zeroModel.
            lastActualModelStep = 0;
            lastActualModel = zeroModel;
        }
        private void GetLastActual(out int lastActualModelStep, out T lastActualModel, List<int> steps, List<T> models)
        {
            for (int i = steps.Count - 1; i >= 0; i--)
            {
                int currStep = steps[i];
                if (currStep <= lastActualStep)
                {
                    lastActualModel = models[i];
                    lastActualModelStep = currStep;
                    return;
                }
            }
            lastActualModel = default(T);
            lastActualModelStep = -1;
        }
        protected override bool replicationAllowed => true;
        #endregion

        #region Give me timeouts and I'll consume them = consider in simulation
        List<TimeoutCommand> notConsideredCommands;
        void InitTimeouts()
        {
            notConsideredCommands = new List<TimeoutCommand>();
        }
        public override void ReceiveTimeout(TimeoutCommand timeout)
        {
            base.ReceiveTimeout(timeout);
            notConsideredCommands.Add(timeout);
        }
        private bool TryConsumeTimeout(TimeoutCommand timeout)
        {
            ZeroLagCommand targetCommand = playedGameReplay.GetTimeoutedCommand(timeout);
            if (targetCommand == null)
                return false;
            int targetCommandStep = targetCommand.stepInd;
            lastActualStep = Math.Min(lastActualStep, targetCommandStep);
            targetCommand.OnTimedout(timeout);
            playedGameReplay.commands[targetCommandStep].RemoveOne(cmd => cmd.hash == timeout.targetCommandHash);
            if (timeout.whatToDo == ActionOnTimeout.ExecuteLater)
                ReceiveCommand(targetCommand); // Receive command later.

            return true;
        }
        protected void TryConsumeTimeouts()
        {
            for (int i = notConsideredCommands.Count - 1; i >= 0; i--)
            {
                if (TryConsumeTimeout(notConsideredCommands[i]))
                    notConsideredCommands.RemoveAt(i);
            }
        }        
        #endregion

        #region I simulate game in waves.
        public override int presentStepUnclamped => presentStepForFixedDt;
        Fix64 simulationSpeedPerRealSpeed = 10m;
        protected override int debugMaxStepsToCatchUpLastActualStepToPresent 
            => (int)(settings.maxLagInSteps * (1m + 1m / simulationSpeedPerRealSpeed)) + 1;
        Fix64 waveSimulationTimeElapsed;
        Fix64 waveTimeElapsed;
        int maxSimulationStepsAllowed = -1;
        public void DebugSetMaxSimulationStepsInUpdate(int maxSimulationStepsAllowed) {
            this.maxSimulationStepsAllowed = maxSimulationStepsAllowed;
        }
        protected void AddReplayStepsUntilPresentTime()
        {
            while (playedGameReplay.stepDtsCount < presentStep)
                playedGameReplay.AddStep(settings.fixedDt);
        }
        private void UpdateSingleThreadedWave(Fix64 dt)
        {
            AddReplayStepsUntilPresentTime();
            // Calc how many steps to simulate in this call.
            waveTimeElapsed += dt;
            int simulationStepsAllowed;
            if (maxSimulationStepsAllowed == -1) // Simulation is faster than real time by 'simulationSpeedPerRealSpeed' times.
                simulationStepsAllowed = (int)((waveTimeElapsed * simulationSpeedPerRealSpeed - waveSimulationTimeElapsed) / settings.fixedDt); 
            else // Debug simulating threaded replay may require this.
                simulationStepsAllowed = maxSimulationStepsAllowed;
            int stepsToPresent = presentStep - cursor.step;
            int simulateStepsCount = Math.Min(stepsToPresent, simulationStepsAllowed);
            waveSimulationTimeElapsed += settings.fixedDt * simulateStepsCount;
            
            for (int i = 0; i < simulateStepsCount; i++)
                DoOneWaveStep(presentStep);
        }
        protected void DoOneWaveStep(int presentStep)
        {
            int stepToUpdateTo = cursor.step + 1;
            bool updatingActual = lastActualStep >= cursor.step;            
            MoveCursorForwardTo(stepToUpdateTo);
            if (updatingActual)
                lastActualStep = Math.Max(stepToUpdateTo, lastActualStep);

            bool waveCameToPresent = stepToUpdateTo == presentStep;

            if (waveCameToPresent)
                OnWaveCameToPresent();
        }
        protected virtual void SetAsViewModel(ref T cursor)
        {
            if (_viewModel.step < cursor.step)
            {
                _viewModel.MortifyWorld();
                ReturnToPool(_viewModel);
                _viewModel = cursor;
                cursor = null;
            }
        }
        void OnWaveCameToPresent()
        {
            // Update viewmodel if needed.
            SetAsViewModel(ref cursor);
            // Cursor goes to past and next wave started.
            int lastActualModelStep;
            T lastActualModel;
            GetLastActual(out lastActualModelStep, out lastActualModel);
            if (cursor!=null && lastActualModelStep==cursor.step)
            {
                // Just use current cursor, no need to go to past.
            } else
            {
                // Go to past and get new cursor.
                if (cursor != null)
                {
                    cursor.MortifyWorld();
                    ReturnToPool(cursor);
                    cursor = null;                    
                }

                cursor = GetModelFromPool();
                cursor.UpdateFrom(lastActualModel);
                cursor.EnliveWorld();
            }
            // Reset wave.
            waveSimulationTimeElapsed = 0;
            waveTimeElapsed = 0;
        }
        #endregion

        #region I'm giving view model reflects match as best as possible, considering all network lags and computational limits.
        protected T _viewModel;
        public override T viewModel => _viewModel;
        void InitViewModel()
        {
            _viewModel = CreateModelInstance();
            _viewModel.EnliveWorld();
        }
        protected void UpdateViewModelLinearly(Fix64 dt)
        {
            if (debugFreezed) return;
            _viewModel.Update(dt, null); // TODO: also get player local commands stream here.
        }
        #endregion

        #region I can be shown pretty for debug
        public override string ToString()
        {
            return string.Format("Non-authoritive model, curr = {0}, last actual ={1}, zero = {2}\nlast dense   =\t{3}, sparse={4}\nlast commands=\t{5}",
                playedGameReplay.stepDtsCount, lastActualStep, GetShowable(zeroModel),
                GetShowable(lastDenseModelsSteps, lastDenseModels), GetShowable(lastSparseModelSteps, lastSparseModels), GetShowable());
        }
        protected string GetShowable()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lastDenseModelsSteps.Count; i++)
            {
                int step = lastDenseModelsSteps[i];
                string commandsShowable = GetShowableCommands(step);
                sb.AppendFormat("{0}:{1} ", lastDenseModelsSteps[i], commandsShowable);
            }
            return sb.ToString();
        }
        #endregion
    }
}