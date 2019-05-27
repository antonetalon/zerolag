﻿using System;
using System.Collections.Generic;
using FixMath.NET;
using OmniBEPUPhysics;
using PortalHunter.Tools;
using Utils = PortalHunter.Tools.Utils;

namespace ZeroLag
{
    [GenSerialization, GenHashing]
    public partial class Replay<T, S> : ISerializable where S : IPredictableSettings<T, S>, new() where T : PredictableModel<T, S>
    {
        public Replay() { }
        public Replay(bool fixedDt, S settings, List<Fix64> stepDts = null) {
            this.fixedDt = fixedDt;
            this.settings = settings;
            if (stepDts != null)
                this.stepDts = stepDts;
        }
        [GenInclude] public S settings = new S();
        [GenIgnore] public Dictionary<int, List<ZeroLagCommand>> commands { get; private set; } = new Dictionary<int, List<ZeroLagCommand>>();

        [GenInclude] public bool fixedDt;
        [GenInclude] protected List<Fix64> stepDts = new List<Fix64>(); // TODO: add special mode when dt is fixed, save only steps count.
        public int stepDtsCount => stepDts.Count;
        public Fix64 GetDt(int step) // Thread-safe. 
        {
            if (fixedDt)
                return settings.fixedDt; // Multithreaded version return const.
            else
                return stepDts[step]; // Single threaded version returns data from replay.
        }
        [GenIgnore] public Fix64 savedTime { get; private set; }
        public void AddStep(Fix64 dt)
        {
            stepDts.Add(dt);
            savedTime += dt;
        }

        public void ReceiveCommand(ZeroLagCommand command)
        {
            List<ZeroLagCommand> currTurnCommands;
            int commandKey = GetCommandKey(command);
            //Debug.Log(commandKey.ToString());
            if (!commands.TryGetValue(commandKey, out currTurnCommands))
            {
                currTurnCommands = new List<ZeroLagCommand>();
                commands.Add(commandKey, currTurnCommands);
            }
            command.hashWithPriority = command.CalculateHashWithPriority();
            currTurnCommands.InsertSorted(command, cmd => cmd.hashWithPriority);
            commandsModified = true;
        }

        [GenInclude] public int totalSteps; // Present step after replay finished.
        public Fix64 CalcDuration()
        {
            if (fixedDt)
                return totalSteps * settings.fixedDt;
            else
            {
                Fix64 sum = 0;
                stepDts.ForEach(dt => sum += dt);
                return sum;
            }
        }

        // Serialization workarounds.
        [GenIgnore] bool commandsModified;
        [GenIgnore] List<List<ZeroLagCommand>> _commandsSerializable;
        [GenInclude, CanBeNull] List<List<ZeroLagCommand>> commandsSerializable
        {
            get {
                if (!commandsModified)
                    return null;
                _commandsSerializable = new List<List<ZeroLagCommand>>();
                foreach (var item in commands)
                {
                    int step = item.Key;
                    List<ZeroLagCommand> stepCommands = item.Value;
                    while (step >= _commandsSerializable.Count)
                        _commandsSerializable.Add(new List<ZeroLagCommand>());
                    foreach (var command in stepCommands)
                        _commandsSerializable[step].InsertSorted(command, cmd=>cmd.hashWithPriority);
                }
                return _commandsSerializable;
            }
            set {
                commands.Clear();
                int step = 0;
                foreach (var stepCommandsSerializable in value)
                {
                    step++;
                    if (stepCommandsSerializable.Count>0)
                    {
                        var stepCommands = new List<ZeroLagCommand>();
                        foreach (var command in stepCommandsSerializable)
                        {
                            command.hashWithPriority = command.CalculateHashWithPriority();
                            stepCommands.InsertSorted(command, cmd => cmd.hashWithPriority);
                        }
                        commands.Add(step, stepCommands);
                    }                    
                }
                commandsModified = true;
            }
        }
        public virtual void OnAfterDeserialize()
        {
            commandsSerializable = _commandsSerializable;
        }
        

        /// <summary>
        /// Commands saved by step they should be executed in.
        /// </summary>
        protected virtual int GetCommandKey(ZeroLagCommand command)
        {
            return command.stepInd;
        }

        public virtual ZeroLagCommand GetTimeoutedCommand(TimeoutCommand timeout)
        {
            List<ZeroLagCommand> currTurnCommands;
            if (!commands.TryGetValue(timeout.targetCommandStep, out currTurnCommands))
                return null;
            ZeroLagCommand targetCommand = currTurnCommands.Find(cmd => cmd.hashWithPriority == timeout.targetCommandHash);
            return targetCommand;
        }
    }
    [GenSerialization, GenHashing]
    public partial class NetworkReplay<T, S> : Replay<T, S> where S : IPredictableSettings<T, S>, new() where T : PredictableModel<T, S>
    {
        public NetworkReplay() { }
        [GenIgnore] Func<int> getCurrStep;
        [GenInclude] public bool resimulations;
        public NetworkReplay(bool resimulations, bool fixedDt, S settings, Func<int> getCurrStep):base(fixedDt, settings)
        {
            this.getCurrStep = getCurrStep;
            this.resimulations = resimulations;
        }
        /// <summary>
        /// Commands saved by step they received in.
        /// </summary>
        protected override int GetCommandKey(ZeroLagCommand command)
        {
            return getCurrStep();
        }

        [GenIgnore] public Dictionary<int, List<TimeoutCommand>> timeouts = new Dictionary<int, List<TimeoutCommand>>();        
        public void ReceiveTimeout(TimeoutCommand timeout)
        {
            List<TimeoutCommand> currStepReceivedTimeouts;
            int step = getCurrStep();
            if (!timeouts.TryGetValue(step, out currStepReceivedTimeouts))
            {
                currStepReceivedTimeouts = new List<TimeoutCommand>();
                timeouts.Add(step, currStepReceivedTimeouts);
            }
            currStepReceivedTimeouts.Add(timeout);
            timeoutsModified = true;
        }


        // Serialization workarounds.
        [GenIgnore] bool timeoutsModified;
        [GenIgnore] List<List<TimeoutCommand>> _timeoutsSerializable;
        [GenInclude, CanBeNull] List<List<TimeoutCommand>> timeoutsSerializable
        {
            get
            {
                if (!timeoutsModified)
                    return null;
                _timeoutsSerializable = new List<List<TimeoutCommand>>();
                foreach (var item in timeouts)
                {
                    int step = item.Key;
                    List<TimeoutCommand> stepTimeouts = item.Value;
                    while (step >= _timeoutsSerializable.Count)
                        _timeoutsSerializable.Add(new List<TimeoutCommand>());
                    _timeoutsSerializable[step] = stepTimeouts;
                }
                return _timeoutsSerializable;
            }
            set
            {
                timeouts.Clear();
                if (value != null)
                {
                    int step = 0;
                    foreach (var stepTimeoutsSerializable in value)
                    {
                        if (stepTimeoutsSerializable.Count > 0)
                        {
                            List<TimeoutCommand> stepTimeouts = new List<TimeoutCommand>();
                            timeouts.Add(step, stepTimeouts);
                        }
                        step++;
                    }
                }
                timeoutsModified = true;
            }
        }
        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            timeoutsSerializable = _timeoutsSerializable;
        }

#if CLIENT
        public static NetworkReplay<T, S> ReadFromFileOnPC(string fileName)
        {
            var replay = Utils.ReadFromFile<NetworkReplay<T, S>>(fileName, false);
            replay.OnAfterDeserialize();
            return replay;
        }
#endif

        public static NetworkReplay<T, S> ReadFromFileOnDevice(byte[] bytes)
        {
            var replay = Utils.DeserializeFromBytes<NetworkReplay<T, S>>(bytes);
            replay.OnAfterDeserialize();
            return replay;
        }

        public override ZeroLagCommand GetTimeoutedCommand(TimeoutCommand timeout)
        {
            var replay = FlatternToReplay();
            return replay.GetTimeoutedCommand(timeout);
        }
        public Replay<T, S> FlatternToReplay()
        {
            Replay<T, S> replay = new Replay<T, S>(fixedDt, settings, stepDts);
            foreach (var item in commands)
            {
                List<ZeroLagCommand> currStepReceivedCommands = item.Value;
                foreach (var command in currStepReceivedCommands)
                    replay.ReceiveCommand(command);
            }
            foreach (var timeoutsReceivedOnCurrStep in timeouts.Values)
            {
                foreach (var timeout in timeoutsReceivedOnCurrStep)
                {
                    ZeroLagCommand targetCommand = replay.GetTimeoutedCommand(timeout);
                    targetCommand.OnTimedout(timeout);
                    var targetStepCommands = replay.commands[timeout.targetCommandStep];
                    targetStepCommands.RemoveOne(cmd => cmd.hashWithPriority == timeout.targetCommandHash);

                    if (timeout.whatToDo == ActionOnTimeout.ExecuteLater)
                        replay.ReceiveCommand(targetCommand); // Receive command later.
                }
            }
            return replay;
        }

        public Fix64 CalcMaxLag()
        {
            int maxLagSteps = 0;
            foreach (var item in commands)
            {
                int receivingStep = item.Key;
                List<ZeroLagCommand> currStepReceivedCommands = item.Value;
                foreach (var command in currStepReceivedCommands)
                {
                    int currCommandLag = receivingStep - command.stepInd;
                    maxLagSteps = Math.Max(currCommandLag, maxLagSteps);
                }
            }
            var replay = FlatternToReplay();
            foreach (var item in timeouts)
            {
                int receivingStep = item.Key;
                List<TimeoutCommand> currStepReceivedTimeouts = item.Value;
                currStepReceivedTimeouts.ForEach(timeout =>
                {
                    ZeroLagCommand command = replay.GetTimeoutedCommand(timeout);
                    int currCommandLag = receivingStep - command.stepInd;
                    maxLagSteps = Math.Max(currCommandLag, maxLagSteps);
                });
            }
            return maxLagSteps * settings.fixedDt;
        }

        public int CalcMaxReceivingStep()
        {
            int sendingCommandsLastStep = 0;
            foreach (var sendingStep in commands.Keys)
                sendingCommandsLastStep = Math.Max(sendingCommandsLastStep, sendingStep);
            foreach (var sendingStep in timeouts.Keys)
                sendingCommandsLastStep = Math.Max(sendingCommandsLastStep, sendingStep);
            return sendingCommandsLastStep;
        }
    }
}