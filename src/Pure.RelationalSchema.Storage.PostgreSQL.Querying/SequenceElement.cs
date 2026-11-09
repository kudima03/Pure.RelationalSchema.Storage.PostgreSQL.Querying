using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// An element of a query that is a sequence itself — a grouping, or the group a
/// <c>GroupBy</c> or <c>GroupJoin</c> hands to its result selector — standing in
/// the element expression for the lambda parameter that holds it (see
/// <see cref="QueryElement"/>): its own element is that of the rows it holds, and
/// its key, when it has one, the key they are grouped by.
/// </summary>
internal sealed class SequenceElement(Type type, Expression? element, Expression? key) : Expression
{
    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type { get; } = type;

    public Expression? Element { get; } = element;

    public Expression? Key { get; } = key;

    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        return this;
    }
}
