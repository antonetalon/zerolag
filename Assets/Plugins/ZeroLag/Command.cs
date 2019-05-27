namespace ZeroLag
{
    public abstract class Command
    {
        public Command() { }
        public virtual bool logged { get { return true; } }
        public abstract long CalculateHash();
    }
}