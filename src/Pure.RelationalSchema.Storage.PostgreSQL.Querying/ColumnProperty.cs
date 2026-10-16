using System.Linq.Expressions;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.HashCodes;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// The property of a generated entity type that stands for a column in the
/// expression tree handed to the translator.
/// </summary>
internal sealed record ColumnProperty
{
    private readonly IColumn _column;

    public ColumnProperty(IColumn column)
    {
        _column = column;
    }

    public string Name => new HexString(new ColumnHash(_column)).TextValue;

    public string ColumnName =>
        new TrimmedHash(new HexString(new ColumnHash(_column))).TextValue;

    public Type Type =>
        _column.Type.Name.TextValue switch
        {
            "int" or "ushort" => typeof(int),
            "long" or "uint" => typeof(long),
            "ulong" => typeof(decimal),
            "bool" => typeof(bool),
            "uuid" => typeof(Guid),
            _ => typeof(string),
        };

    /// <summary>
    /// The cell text of the column, as the rows read it, built from the property
    /// so that the translator renders it in SQL.
    /// </summary>
    public Expression Text(Expression property)
    {
        return _column.Type.Name.TextValue switch
        {
            "string" => property,
            "bool" => Expression.Condition(
                property,
                Expression.Constant(bool.TrueString),
                Expression.Constant(bool.FalseString)
            ),
            "int" or "ushort" or "long" or "uint" or "ulong" or "uuid" => Expression.Call(
                property,
                Type.GetMethod(nameof(ToString), Type.EmptyTypes)!
            ),
            _ => throw new NotSupportedException(),
        };
    }
}
