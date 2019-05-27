namespace ZeroLag
{
    public interface IZeroLagGameConfig<T, S> where T : PredictableModel<T, S>
    {
        T CreateZeroModel(ObjectPool pool, bool replicationAllowed);
    }
}