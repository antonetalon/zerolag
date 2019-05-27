namespace ZeroLag
{
    public partial class TimeoutCommand : Command
    {   
        public int targetCommandStep { get; private set; }
        public long targetCommandHash { get; private set; }
        public ActionOnTimeout whatToDo { get; private set; }
        public int executeLaterStep { get; private set; }        
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