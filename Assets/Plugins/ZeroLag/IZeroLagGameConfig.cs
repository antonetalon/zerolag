using Alive;

namespace ZeroLag
{
    public interface IZeroLagGameConfig<T, S> : ISerializable where T : PredictableModel<T, S>
    {
        T CreateZeroModel(ObjectPool pool, bool replicationAllowed);
    }
}