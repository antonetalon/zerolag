using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FixMath.NET;

namespace ZeroLag
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T">Type for battle model on each step</typeparam>
    /// <typeparam name="S">Type for game settings const for each game</typeparam>
    public abstract class Engine<T, S> where T : PredictableModel<T, S> where S : IPredictableSettings<T, S>, new()
    {
        #region Receiving commands
        /// <summary>
        /// Saves commands per step where they should be processed.
        /// </summary>
        protected Replay<T, S> playedGameReplay;
        public Fix64 GetDt(int step) => playedGameReplay.GetDt(step); // Thread-safe. 
        /// <summary>
        /// Last step that is calculated and wasnt changed by new commands.
        /// </summary>
        protected int lastActualStep;
        /// <summary>
        /// Each command that received from other PredictiveModels should go here.
        /// </summary>
        public virtual void ReceiveCommand(ZeroLagCommand command)
        {
            if (!DebugCheckCommandAllowed(command))
                return;
            if (stopped)
                return;
            playedGameReplay.ReceiveCommand(command);
            // Declare as not actualized all steps after this command's step.
            //if (command.stepInd == -1)
            //    throw exception.
            lastActualStep = Math.Min(lastActualStep, command.stepInd);
        }

        public virtual void ReceiveTimeout(TimeoutCommand timeout) { }
        protected IEnumerable<ZeroLagCommand> GetCommands(int step)
        {
            List<ZeroLagCommand> currStepCommands;
            playedGameReplay.commands.TryGetValue(step, out currStepCommands);
            return currStepCommands != null ? currStepCommands : null;
        }
        #endregion

        #region Pool for all models
        private Stack<T> modelsPool;
        protected T GetModelFromPool()
        {
            if (modelsPool.Count == 0)
                return CreateModelInstance();
            return modelsPool.Pop();
        }
        protected abstract bool replicationAllowed { get; }
        protected T CreateModelInstance()
        {
            T instance = settings.CreateZeroModel(this, replicationAllowed);
            //instance.__debugTag = debugTag;
            onModelCreated?.Invoke(instance);
            return instance;
        }

        public T DebugCreateInstance() => CreateModelInstance();
        protected void ReturnToPool(T model)
        {
            modelsPool.Push(model);
        }
        #endregion

        #region Start/stop match
        /// <summary>
        /// Game settings.
        /// they're independant from steps and are constant during battle.
        /// </summary>
        public readonly S settings;
        public string debugTag { get; private set; }
        public readonly bool fixedDt;
        public readonly bool simulate;
        public Engine(S settings, bool simulate, int modelPoolSize, bool fixedDt, string debugTag)
        {
            this.simulate = simulate;
            this.fixedDt = fixedDt;
            this.settings = settings;
            this.debugTag = debugTag;
            if (simulate)
            {
                modelsPool = new Stack<T>(modelPoolSize);
                for (int i = 0; i < modelPoolSize; i++)
                    modelsPool.Push(CreateModelInstance());
                cursor = CreateModelInstance();
                cursor.EnliveWorld();
            }
            lastActualStep = 0;
            playedGameReplay = new Replay<T, S>(fixedDt, settings);
        }
        public bool stopped { get; private set; }
        /// <summary>
        /// Stops simulation, stops all threads, do not do anything in update, dont add anything to replay.
        /// </summary>
        public virtual void Stop()
        {
            stopped = true;
        }
        #endregion

        #region Updating present time
        /// <summary>
        /// Tells engine that present match time changed, simulation should catch up with it.
        /// </summary>
        public void UpdatePresentTime(Fix64 dt)
        {
            if (stopped)
                return;
            presentTime += dt;
            OnPresentTimeAdded(dt);
        }
        protected abstract void OnPresentTimeAdded(Fix64 dt);
        /// <summary>
        /// Step that correspods currently present time.
        /// </summary>
        public int presentStep
        {
            get
            {
                int val = presentStepUnclamped;
                if (debugMaxPresentStep != -1 && val > debugMaxPresentStep)
                    val = debugMaxPresentStep;
                return val;
            }
        }
        public abstract int presentStepUnclamped { get; }
        public int nextStep => presentStep + 1;
        public virtual Fix64 presentTime { get; protected set; }
        protected int presentStepForFixedDt => (int)(presentTime / settings.fixedDt);
        #endregion

        #region Moving cursor forward in time
        /// <summary>
        /// Updating cursor to simulate game to current time.
        /// </summary>
        protected T cursor;
        /// <summary>
        /// Updates cursor from some step to some another step.
        /// </summary>
        protected void MoveCursorForwardTo(int stepToUpdateTo)
        {
            int stepToUpdateFrom = cursor.step;
            for (int currStep = stepToUpdateFrom; currStep < stepToUpdateTo;)
            {
                var currStepCommands = GetCommands(currStep);
                debugOnBeforeStepUpdate?.Invoke(cursor, currStepCommands);
                onBeforeUpdateStep?.Invoke(cursor);
                cursor.UpdateStep(playedGameReplay.GetDt(currStep), currStepCommands);
                currStep++;
                onAfterUpdateStep?.Invoke(cursor);
                OnModelActualized(currStep, cursor);
            }
        }
        protected virtual void OnModelActualized(int step, T stepModel) { }
        #endregion

        #region ViewModel
        /// <summary>
        /// viewModel - alive model accessible from main thread.
        /// Modification not allowed.
        /// </summary>
        public abstract T viewModel { get; }
        #endregion

        #region Debug
        public bool debugFreezed => debugMaxPresentStep != -1;
        protected int debugMaxPresentStep { get; private set; } = -1;
        protected int debugMaxInputStep { get; private set; } = -1;
        protected abstract int debugMaxStepsToCatchUpLastActualStepToPresent { get; }
        public virtual void DebugSetFreeze(int maxInputStep)
        {
            debugMaxInputStep = maxInputStep;
            debugMaxPresentStep = maxInputStep + debugMaxStepsToCatchUpLastActualStepToPresent;
        }
        private bool DebugCheckCommandAllowed(ZeroLagCommand command)
        {
            if (debugMaxInputStep == -1)
                return true;
            return command.stepInd <= debugMaxInputStep;
        }
        // Actions for debugging purposes.
        public Action<T> onModelCreated;
        public Action<T> onBeforeUpdateStep;
        public Action<T> onAfterUpdateStep;
        public Action<T, IEnumerable<ZeroLagCommand>> debugOnBeforeStepUpdate;

        protected string GetShowable(List<int> steps, List<T> models)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < steps.Count; i++)
                sb.AppendFormat("{0}:{1} ", steps[i], GetShowable(models[i]));
            return sb.ToString();
        }

        protected string GetShowable(T model)
        {
            return (model.CalculateHash() % 1000).ToString("000");
        }

    protected string GetShowableCommands(int step)
        {
            long stepHash = CalcStepCommandsHash(step, playedGameReplay);            
            return (stepHash % 1000).ToString("000");
        }
        public static long CalcStepCommandsHash(int step, Replay<T, S> replay)
        {
            if (step < 0)
                return 0;
            long stepHash = 0;
            List<ZeroLagCommand> currStepCommands;
            if (replay.commands.TryGetValue(step, out currStepCommands))
            {
                foreach (var currCommand in currStepCommands)
                    stepHash += currCommand.CalculateHash();
            }

            stepHash += CalcStepCommandsHash(step - 1, replay);
            return stepHash;
        }
        #endregion
    }
}