using Multiplayer;

namespace ZeroLag
{
    [BattleNetworkChannelUsage(BattleNetworkChannel.AllCostServer2Client)]
    public partial class TimeoutCommand : Command
    {   
        [GenInclude] public int targetCommandStep { get; private set; }
        [GenInclude] public long targetCommandHash { get; private set; }
        [GenInclude] public ActionOnTimeout whatToDo { get; private set; }
        [GenInclude] public int executeLaterStep { get; private set; }        
        public TimeoutCommand(ZeroLagCommand command, int minModifyableStep)
        {
            targetCommandStep = command.stepInd;
            targetCommandHash = command.CalculateHashWithPriority();
            whatToDo = command.WhatToDoOnTimeout();
            if (whatToDo == ActionOnTimeout.Cancel)
                executeLaterStep = -1;
            else
                executeLaterStep = minModifyableStep;
        }
        public TimeoutCommand() { }
    }
}