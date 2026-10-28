using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.HashCodes;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// The property of a generated entity type that stands for a column in the
/// expression tree handed to the translator. Its type is nullable, as any column may
/// hold NULL, which the cell text gives as empty text.
/// </summary>
internal sealed record ColumnProperty
{
    private static readonly MethodInfo Concat = typeof(string).GetMethod(
        nameof(string.Concat),
        [typeof(string), typeof(string)]
    )!;

    private static readonly MethodInfo Replace = typeof(string).GetMethod(
        nameof(string.Replace),
        [typeof(string), typeof(string)]
    )!;

    private static readonly MethodInfo Magnitude = typeof(Math).GetMethod(
        nameof(Math.Abs),
        [typeof(double)]
    )!;

    private static readonly MethodInfo Numeric = typeof(decimal).GetMethod(
        nameof(decimal.Parse),
        [typeof(string), typeof(IFormatProvider)]
    )!;

    private static readonly Dictionary<Type, MethodInfo> ToStrings = new()
    {
        [typeof(int)] = typeof(int).GetMethod(nameof(ToString), Type.EmptyTypes)!,
        [typeof(long)] = typeof(long).GetMethod(nameof(ToString), Type.EmptyTypes)!,
        [typeof(decimal)] = typeof(decimal).GetMethod(nameof(ToString), Type.EmptyTypes)!,
        [typeof(double)] = typeof(double).GetMethod(nameof(ToString), Type.EmptyTypes)!,
        [typeof(Guid)] = typeof(Guid).GetMethod(nameof(ToString), Type.EmptyTypes)!,
    };

    private static readonly ConstantExpression Empty = Expression.Constant(string.Empty);

    private static readonly Dictionary<Type, Type> Numbers = new()
    {
        [typeof(int)] = typeof(int?),
        [typeof(long)] = typeof(long?),
        [typeof(decimal)] = typeof(decimal?),
        [typeof(double)] = typeof(double?),
    };

    private readonly IColumn _column;

    public ColumnProperty(IColumn column)
    {
        _column = column;
    }

    public string Name => new HexString(new ColumnHash(_column)).TextValue;

    public string ColumnName =>
        new TrimmedHash(new HexString(new ColumnHash(_column))).TextValue;

    /// <summary>
    /// The property type of the column, nullable as any column may hold NULL. A
    /// <c>float</c> column is a double to the translator, which writes no float as
    /// text; the SQL casts the real column itself. A <c>determined_hash</c> column
    /// is text to the translator, whose bytes are read as UTF-8 text, and so is a
    /// column of a type the translator does not map, whose cells cannot be
    /// translated.
    /// </summary>
    public Type Type =>
        TypeName switch
        {
            "bool" => typeof(bool?),
            "int" or "ushort" => typeof(int?),
            "long" or "uint" => typeof(long?),
            "ulong" => typeof(decimal?),
            "uuid" => typeof(Guid?),
            "datetime" => typeof(DateTime?),
            "date" => typeof(DateOnly?),
            "time" => typeof(TimeOnly?),
            "double" or "float" => typeof(double?),
            _ => typeof(string),
        };

    private string TypeName => _column.Type.Name.TextValue;

    /// <summary>
    /// The value of the column read as the type its cell text is parsed to: the
    /// property itself for a nullable read, or its value (see <see cref="Value"/>,
    /// never NULL to the translator) converted to the parsed type for a bare one;
    /// null when the type is not the column's or a wider number. A <c>double</c> or
    /// <c>float</c> column is not read so: the text PostgreSQL writes for it is the
    /// shortest that reads back, which a numeric cast does not give, and a text
    /// column has no parse.
    /// </summary>
    public Expression? Typed(Expression property, Type parsed, bool nullable)
    {
        Type? column = Nullable.GetUnderlyingType(Type);
        if (
            column is null
            || TypeName is "double" or "float"
            || !(
                column == parsed
                || (Numbers.ContainsKey(column) && Numbers.ContainsKey(parsed))
            )
        )
        {
            return null;
        }
        if (nullable)
        {
            return column == parsed
                ? property
                : Expression.Convert(property, Numbers[parsed]);
        }
        UnaryExpression value = Value(property);
        return column == parsed ? value : Expression.Convert(value, parsed);
    }

    /// <summary>
    /// The cell text of the column, as the rows read it, built from the property
    /// so that the translator renders it in SQL: empty text for NULL, otherwise the
    /// invariant text of the value.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The cells of the column's type cannot be translated.
    /// </exception>
    public Expression Text(Expression property)
    {
        return TypeName switch
        {
            "string" or "determined_hash" => Expression.Coalesce(property, Empty),
            "bool" => Guarded(
                property,
                Expression.Condition(
                    Value(property),
                    Expression.Constant(bool.TrueString),
                    Expression.Constant(bool.FalseString)
                )
            ),
            "int" or "ushort" or "long" or "uint" or "ulong" or "uuid" =>
                Expression.Coalesce(Stringified(Value(property)), Empty),
            "datetime" => Guarded(property, DateTimeText(Value(property))),
            "date" => Guarded(property, DateText(Value(property))),
            "time" => Guarded(property, TimeText(Value(property))),
            "double" => Expression.Coalesce(Floating(Value(property), 1e15, 1e17), Empty),
            "float" => Expression.Coalesce(Floating(Value(property), 1e6, 1e9), Empty),
            _ => throw new NotSupportedException(
                $"The cells of the column '{_column.Name.TextValue}' of type "
                    + $"'{TypeName}' cannot be translated to SQL."
            ),
        };
    }

    /// <summary>
    /// The text of the value of a nullable property when it has one, otherwise
    /// empty text.
    /// </summary>
    private static ConditionalExpression Guarded(Expression property, Expression text)
    {
        return Expression.Condition(
            Expression.NotEqual(property, Expression.Constant(null, property.Type)),
            text,
            Empty
        );
    }

    /// <summary>
    /// The value of a nullable property, which the translator reads as the column.
    /// </summary>
    private static UnaryExpression Value(Expression property)
    {
        return Expression.Convert(property, Nullable.GetUnderlyingType(property.Type)!);
    }

    private static MethodCallExpression Stringified(Expression value)
    {
        return Expression.Call(value, ToStrings[value.Type]);
    }

    /// <summary>
    /// The invariant general format of a date and time, <c>MM/dd/yyyy HH:mm:ss</c>,
    /// from its parts, as the translator renders no formatting of dates.
    /// </summary>
    private static Expression DateTimeText(Expression moment)
    {
        return Joined(
            Padded(moment, Part<DateTime>(nameof(DateTime.Month)), 2),
            Expression.Constant("/"),
            Padded(moment, Part<DateTime>(nameof(DateTime.Day)), 2),
            Expression.Constant("/"),
            Padded(moment, Part<DateTime>(nameof(DateTime.Year)), 4),
            Expression.Constant(" "),
            Padded(moment, Part<DateTime>(nameof(DateTime.Hour)), 2),
            Expression.Constant(":"),
            Padded(moment, Part<DateTime>(nameof(DateTime.Minute)), 2),
            Expression.Constant(":"),
            Padded(moment, Part<DateTime>(nameof(DateTime.Second)), 2)
        );
    }

    /// <summary>
    /// The invariant short format of a date, <c>MM/dd/yyyy</c>, from its parts.
    /// </summary>
    private static Expression DateText(Expression day)
    {
        return Joined(
            Padded(day, Part<DateOnly>(nameof(DateOnly.Month)), 2),
            Expression.Constant("/"),
            Padded(day, Part<DateOnly>(nameof(DateOnly.Day)), 2),
            Expression.Constant("/"),
            Padded(day, Part<DateOnly>(nameof(DateOnly.Year)), 4)
        );
    }

    /// <summary>
    /// The invariant short format of a time, <c>HH:mm</c>, from its parts.
    /// </summary>
    private static Expression TimeText(Expression time)
    {
        return Joined(
            Padded(time, Part<TimeOnly>(nameof(TimeOnly.Hour)), 2),
            Expression.Constant(":"),
            Padded(time, Part<TimeOnly>(nameof(TimeOnly.Minute)), 2)
        );
    }

    /// <summary>
    /// The shortest round-trip text of a floating-point number, as .NET writes it.
    /// PostgreSQL writes the exponent in lower case, and uses the exponent form from
    /// a lower magnitude than .NET: in between, the text goes through a numeric,
    /// which writes the same digits in the plain form.
    /// </summary>
    private static ConditionalExpression Floating(
        Expression value,
        double lowest,
        double highest
    )
    {
        MethodCallExpression magnitude = Expression.Call(Magnitude, value);
        MethodCallExpression text = Stringified(value);
        return Expression.Condition(
            Expression.AndAlso(
                Expression.GreaterThanOrEqual(magnitude, Expression.Constant(lowest)),
                Expression.LessThan(magnitude, Expression.Constant(highest))
            ),
            Stringified(
                Expression.Call(
                    Numeric,
                    text,
                    Expression.Constant(CultureInfo.InvariantCulture)
                )
            ),
            Expression.Call(
                text,
                Replace,
                Expression.Constant("e"),
                Expression.Constant("E")
            )
        );
    }

    private static PropertyInfo Part<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(string name)
    {
        return typeof(T).GetProperty(name)!;
    }

    private static Expression Joined(params Expression[] parts)
    {
        return parts.Aggregate((text, part) => Expression.Add(text, part, Concat));
    }

    /// <summary>
    /// The text of a part of a date or time, padded with zeros to the width.
    /// </summary>
    private static Expression Padded(Expression moment, PropertyInfo part, int width)
    {
        MemberExpression value = Expression.Property(moment, part);
        Expression text = Stringified(value);
        for (int digits = width - 1; digits >= 1; digits--)
        {
            text = Expression.Condition(
                Expression.LessThan(
                    value,
                    Expression.Constant((int)Math.Pow(10, digits))
                ),
                Expression.Add(
                    Expression.Constant(new string('0', width - digits)),
                    Stringified(value),
                    Concat
                ),
                text
            );
        }
        return text;
    }
}
