using System.Data;
using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Reads no column and gives the default value of a type: the argument of a
/// constructor that the initializer of the object replaces, which the translator
/// does not project.
/// </summary>
internal sealed record DefaultReader : IResultReader
{
    private readonly Type _type;

    public DefaultReader(Type type)
    {
        _type = type;
    }

    public int Width => 0;

    public object? Read(IDataRecord record, int offset)
    {
        return new ClosureValue(Expression.Default(_type)).Value;
    }
}
