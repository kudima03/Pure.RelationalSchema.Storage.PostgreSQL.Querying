using System.Linq.Expressions;
using System.Reflection;
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
    private static readonly MethodInfo Concat = typeof(string).GetMethod(
        nameof(string.Concat),
        [typeof(string), typeof(string)]
    )!;

    private static readonly Dictionary<Type, MethodInfo> ToStrings = new()
    {
        [typeof(int)] = typeof(int).GetMethod(nameof(ToString), Type.EmptyTypes)!,
        [typeof(long)] = typeof(long).GetMethod(nameof(ToString), Type.EmptyTypes)!,
        [typeof(decimal)] = typeof(decimal).GetMethod(nameof(ToString), Type.EmptyTypes)!,
        [typeof(Guid)] = typeof(Guid).GetMethod(nameof(ToString), Type.EmptyTypes)!,
    };

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
            "datetime" => typeof(DateTime),
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
                ToStrings[Type]
            ),
            "datetime" => DateTimeText(property),
            _ => throw new NotSupportedException(),
        };
    }

    /// <summary>
    /// The invariant general format of a date and time, <c>MM/dd/yyyy HH:mm:ss</c>,
    /// from its parts, as the translator renders no formatting of dates.
    /// </summary>
    private static Expression DateTimeText(Expression property)
    {
        Expression[] parts =
        [
            Padded(property, nameof(DateTime.Month), 2),
            Expression.Constant("/"),
            Padded(property, nameof(DateTime.Day), 2),
            Expression.Constant("/"),
            Padded(property, nameof(DateTime.Year), 4),
            Expression.Constant(" "),
            Padded(property, nameof(DateTime.Hour), 2),
            Expression.Constant(":"),
            Padded(property, nameof(DateTime.Minute), 2),
            Expression.Constant(":"),
            Padded(property, nameof(DateTime.Second), 2),
        ];
        return parts.Aggregate((text, part) => Expression.Add(text, part, Concat));
    }

    /// <summary>
    /// The text of a part of a date and time, padded with zeros to the width.
    /// </summary>
    private static Expression Padded(Expression property, string part, int width)
    {
        MemberExpression value = Expression.Property(
            property,
            typeof(DateTime).GetProperty(part)!
        );
        Expression text = Expression.Call(value, ToStrings[typeof(int)]);
        for (int digits = width - 1; digits >= 1; digits--)
        {
            text = Expression.Condition(
                Expression.LessThan(
                    value,
                    Expression.Constant((int)Math.Pow(10, digits))
                ),
                Expression.Add(
                    Expression.Constant(new string('0', width - digits)),
                    Expression.Call(value, ToStrings[typeof(int)]),
                    Concat
                ),
                text
            );
        }
        return text;
    }
}
