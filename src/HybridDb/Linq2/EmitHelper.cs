using HybridDb.Linq2.Ast;

namespace HybridDb.Linq2
{
    public static class EmitHelper
    {
        public static SqlStatementEmitter.Result Emit(this SqlStatementEmitter.Result result, Expression expression)
        {
            return SqlStatementEmitter.Emit(result, expression);
        }
    }
}