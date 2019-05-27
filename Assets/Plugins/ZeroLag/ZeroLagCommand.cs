
namespace ZeroLag
{
    public abstract partial class ZeroLagCommand : Command
    {
        [GenIgnore] public long hashWithPriority;
        public string playerId;
        public int stepInd;
        // from 0 to 3 is valid
        [GenIgnore(GenTaskFlags.Hash)] public byte priority;
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

        public long CalculateHashWithPriority()
        {
            var baseVal = CalculateHash();
            ulong hash = (ulong)baseVal;
            hash = hash >> 3;
            hash |= ((ulong)priority) << 61;
            return (long)hash;
        }
    }
}