using PortalHunter.GameRoot;
using StreamsFromNetwork;
using System.IO;

namespace ZeroLag
{
    [GenHashing, GenSerialization, GenTask(GenTaskFlags.PolymorphicConstruction | GenTaskFlags.JsonSerialization)]
    public partial class Command : INetworkData
    {
        public Command() { }
        public virtual bool logged { get { return true; } }
        [CanBeNull] public Timings timings;
    }
}