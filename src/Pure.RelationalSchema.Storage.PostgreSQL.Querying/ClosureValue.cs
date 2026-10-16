using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

internal sealed record ClosureValue
{
    private readonly Expression _expression;

    public ClosureValue(Expression expression)
    {
        _expression = expression;
    }

    public object? Value =>
        _expression is ConstantExpression constant
            ? constant.Value
            : Expression
                .Lambda<Func<object?>>(Expression.Convert(_expression, typeof(object)))
                .Compile()();
}
