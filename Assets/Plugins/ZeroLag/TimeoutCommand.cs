namespace ZeroLag
{
    public class TimeoutCommand : Command
    {   
        public int targetCommandStep { get; private set; }
        public long targetCommandHash { get; private set; }
        public ActionOnTimeout whatToDo { get; private set; }
        public int executeLaterStep { get; private set; }        
        public TimeoutCommand(ZeroLagCommand command, int minModifyableStep)
        {
            targetCommandStep = command.stepInd;
            targetCommandHash = command.CalculateHash();
            whatToDo = command.WhatToDoOnTimeout();
            if (whatToDo == ActionOnTimeout.Cancel)
                executeLaterStep = -1;
            else
                executeLaterStep = minModifyableStep;
        }
        public TimeoutCommand() { }

        public override long CalculateHash()
        {
            long hash = 345093625;
            hash += hash << 11; hash ^= hash >> 7;
            hash += targetCommandStep;
            hash += hash << 11; hash ^= hash >> 7;
            hash += targetCommandHash;
            hash += hash << 11; hash ^= hash >> 7;
            hash += (long)whatToDo;
            hash += hash << 11; hash ^= hash >> 7;
            hash += executeLaterStep;
            hash += hash << 11; hash ^= hash >> 7;
            return hash;
        }
    }
}