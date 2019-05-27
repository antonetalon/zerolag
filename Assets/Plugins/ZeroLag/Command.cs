namespace ZeroLag
{
    public abstract class Command
    {
        public Command() { }
        public virtual bool logged => true;
        public abstract long CalculateHash();
    }
}