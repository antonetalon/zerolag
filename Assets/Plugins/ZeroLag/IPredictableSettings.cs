using FixMath.NET;

namespace ZeroLag
{
    public interface IPredictableSettings<T, S> where T : PredictableModel<T, S> 
    {
        T CreateZeroModel(ObjectPool pool, bool replicationAllowed);
        Fix64 fixedDt { get; }
        Fix64 maxAllowedLag { get; }
        int maxLagInSteps { get; }
        long CalculateHash();
    }
}