using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// The query root the translator sees in place of a stored table: only its element
/// type, a generated <see cref="EntityType"/>, is read. It is never enumerated. It is
/// a class, not a record, as the text of a record would print its own expression.
/// </summary>
internal sealed class TableRoot(Type elementType) : IQueryable<IRow>
{
    public Type ElementType { get; } = elementType;

    public Expression Expression => Expression.Constant(this, typeof(IQueryable<IRow>));

    public IQueryProvider Provider => throw new NotSupportedException();

    public IEnumerator<IRow> GetEnumerator()
    {
        throw new NotSupportedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
