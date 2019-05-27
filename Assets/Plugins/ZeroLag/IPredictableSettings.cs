using FixMath.NET;

namespace ZeroLag
{
    public interface IPredictableSettings<T, S> 
        where T : PredictableModel<T, S> where S : IPredictableSettings<T, S>, new()
    {
        T CreateZeroModel(bool replicationAllowed);
        Fix64 fixedDt { get; }
        Fix64 maxAllowedLag { get; }
        int maxLagInSteps { get; }
        long CalculateHash();
    }
}