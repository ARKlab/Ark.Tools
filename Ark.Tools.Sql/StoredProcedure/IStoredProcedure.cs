using System.Threading.Tasks;

namespace Ark.Tools.Sql.StoredProcedure
{
    public interface IStoredProcedure<TResult>
    {
        TResult LastResult { get; }
        TResult Execute();
        Task<TResult> ExecuteAsync();
    }

    public interface IStoredProcedure<TResult, TParameter>
    {
        TResult LastResult { get; }
        TResult Execute(TParameter param);
        Task<TResult> ExecuteAsync(TParameter param);
    }
}