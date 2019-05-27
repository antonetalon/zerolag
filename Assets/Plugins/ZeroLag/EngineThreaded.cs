//#if CLIENT
using FixMath.NET;
using System.Collections.Generic;
using System.Threading;

namespace ZeroLag
{
    public class EngineThreaded<T, S> : EngineWithResimulations<T, S>
        where T : PredictableModel<T, S> where S : IPredictableSettings<T, S>, new()
    {
        #region I transfer presentTime to calc thread from main thread
        object lockPresentTime = new object();
        public override Fix64 presentTime
        {
            get
            {
                lock (lockPresentTime)
                    return base.presentTime;
            }
            protected set
            {
                lock (lockPresentTime)
                    base.presentTime = value;
            }
        }
        #endregion

        #region I receive commands from main thread and push them to calc thread
        List<ZeroLagCommand> commandsThreadBuffer = new List<ZeroLagCommand>();
        object commandsBufferLock = new object();
        public override void ReceiveCommand(ZeroLagCommand command)
        {
            lock (commandsBufferLock)
                commandsThreadBuffer.Add(command);
        }
        List<TimeoutCommand> timeoutsThreadBuffer = new List<TimeoutCommand>();
        object timeoutsBufferLock = new object();
        public override void ReceiveTimeout(TimeoutCommand timeout)
        {
            lock (timeoutsBufferLock)
                timeoutsThreadBuffer.Add(timeout);
        }
        private void ApplyCommandsFromThreadBuffers()
        {
            lock (commandsBufferLock)
            {
                commandsThreadBuffer.ForEach(command => base.ReceiveCommand(command));
                commandsThreadBuffer.Clear();
            }
            lock (timeoutsBufferLock)
            {
                timeoutsThreadBuffer.ForEach(timeout => base.ReceiveTimeout(timeout));
                timeoutsThreadBuffer.Clear();
            }
        }
        #endregion

        #region I make calc thread give viewModel to main thread
        object viewModelContext = new object();
        object lockViewModelBuffer = new object();
        T viewModelThreadBuffer;
        int viewModelStep;
        private void ApplyViewModelFromThreadBuffer()
        {
            lock (lockViewModelBuffer)
            {
                if (viewModelThreadBuffer != null && _viewModel.step < viewModelThreadBuffer.step)
                    ZeroLagUtils.Swap(ref _viewModel, ref viewModelThreadBuffer);
                viewModelStep = _viewModel.step;                
            }
        }
        protected override void SetAsViewModel(ref T cursor)
        {
            bool settingNeeded = false;
            T modelToReturnToPool = default(T);
            lock (lockViewModelBuffer)
            {
                settingNeeded = viewModelThreadBuffer == null || viewModelStep < cursor.step;
                if (settingNeeded)
                {
                    // Update viewModel.
                    modelToReturnToPool = viewModelThreadBuffer;
                    viewModelThreadBuffer = cursor;
                    viewModelThreadBuffer.context = viewModelContext;
                }
            }
            if (settingNeeded)
            {
                // Return previous viewModel to pool.
                if (modelToReturnToPool != null)
                {
                    modelToReturnToPool.context = this;
                    modelToReturnToPool.MortifyWorld();
                    ReturnToPool(modelToReturnToPool);
                }                
                // Do not use this model in calc thread while its viewModel.
                cursor = null;
            }
        }
        #endregion

        #region I do all battle model calculations in another thread
        Thread calcThread;
        public EngineThreaded(S settings, bool simulate, string debugTag) : base(settings, simulate, debugTag)
        {
            // Prepare start viewModel.
            _viewModel = GetModelFromPool();
            _viewModel.context = viewModelContext;
            _viewModel.EnliveWorld();
            if (simulate)
            {
                // Start calc thread.
                calcThread = new Thread(RunCalcThread);
                calcThread.Start();
            }
        }
        protected override void OnPresentTimeAdded(Fix64 dt)
        {
            // Do not do any calculations - they're moved to another thread.
            // Only update viewModel.
            ApplyViewModelFromThreadBuffer();
            UpdateViewModelLinearly(dt);
        }
        public override void Stop()
        {
            base.Stop();
            calcThread.Abort();
            //UnityEngine.Debug.Log("Stop thread");
        }
        void RunCalcThread()
        {
            while (true)
            {
                ApplyCommandsFromThreadBuffers();
                TryConsumeTimeouts();
                AddReplayStepsUntilPresentTime();

                int presentStep = this.presentStep;
                int cursorStep = cursor.step;

                if (presentStep > cursorStep)
                {
                    DoOneWaveStep(presentStep);
                    //UnityEngine.Debug.Log($"I'm updating step {cursorStep} while presentStep is {presentStep}");
                }
                else
                {
                    Thread.Sleep(30);
                    //UnityEngine.Debug.Log($"I'm sleeping while presentStep is {presentStep}");
                }
            }
        }
        #endregion
    }
}
//#endif