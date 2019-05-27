
namespace ZeroLag
{
    public abstract partial class ZeroLagCommand : Command
    {
        public string playerId;
        public int stepInd;
        public void OnTimedout(TimeoutCommand timeout)
        {
            if (timeout.whatToDo == ActionOnTimeout.ExecuteLater)
                stepInd = timeout.executeLaterStep;
            else
                stepInd = -1;
        }
        public virtual ActionOnTimeout WhatToDoOnTimeout()
        {
            return ActionOnTimeout.Cancel;
        }
        public ZeroLagCommand() { }
        public override string ToString()
        {
            return string.Format("type = {0} playerId={1}, stepInd={2}, hash = {3}", this.GetType().Name, playerId, stepInd, CalculateHashWithPriority());
        }
    }
}