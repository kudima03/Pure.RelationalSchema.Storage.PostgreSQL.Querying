using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Replaces each largest query operator that reads one schema only, and can be
/// translated, with its result read in SQL. What is left, such as operators that
/// combine schemas, runs in memory over those results.
/// </summary>
internal sealed class SchemaSubqueries : ExpressionVisitor
{
    public override Expression? Visit(Expression? node)
    {
        return
            node is MethodCallExpression call
            && call.Method.DeclaringType == typeof(Queryable)
            && new SchemaQuery(call).Result() is Expression result
            ? result
            : base.Visit(node);
    }
}
