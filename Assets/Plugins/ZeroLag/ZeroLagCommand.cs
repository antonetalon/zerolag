namespace ZeroLag
{
    public abstract partial class ZeroLagCommand : Command
    {
        public string playerId;
        public int stepInd;
        public long hash;        
        public virtual ActionOnTimeout WhatToDoOnTimeout() => ActionOnTimeout.Cancel;

        public void OnTimedout(TimeoutCommand timeout)
        {
            if (timeout.whatToDo == ActionOnTimeout.ExecuteLater)
                stepInd = timeout.executeLaterStep;
            else
                stepInd = -1;
        }
        public ZeroLagCommand() { }
        public override string ToString() 
            => string.Format("type = {0} playerId={1}, stepInd={2}, hash = {3}", 
                this.GetType().Name, playerId, stepInd, CalculateHash());
    }
}