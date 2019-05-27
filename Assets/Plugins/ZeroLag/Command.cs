using System.IO;

namespace ZeroLag
{
    public partial class Command
    {
        public Command() { }
        public virtual bool logged { get { return true; } }
    }
}