using System.Collections.ObjectModel;
using System.Data;
using System.Linq.Expressions;
using Linq.Expressions.Translation.PostgreSql;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// A query operator whose tree must read stored tables of one schema only. The
/// translator turns it into SQL, which runs as one statement, and its result
/// replaces the operator in the tree.
/// </summary>
internal sealed record SchemaQuery
{
    private static readonly HashSet<string> Elements =
    [
        nameof(Queryable.First),
        nameof(Queryable.FirstOrDefault),
        nameof(Queryable.Single),
        nameof(Queryable.SingleOrDefault),
        nameof(Queryable.Last),
        nameof(Queryable.LastOrDefault),
        nameof(Queryable.ElementAt),
        nameof(Queryable.ElementAtOrDefault),
    ];

    private static readonly HashSet<string> RowPreserving =
    [
        .. Elements,
        nameof(Queryable.Where),
        nameof(Queryable.OrderBy),
        nameof(Queryable.OrderByDescending),
        nameof(Queryable.ThenBy),
        nameof(Queryable.ThenByDescending),
        nameof(Queryable.Skip),
        nameof(Queryable.Take),
        nameof(Queryable.SkipLast),
        nameof(Queryable.TakeLast),
        nameof(Queryable.Distinct),
        nameof(Queryable.Reverse),
    ];

    private static readonly HashSet<string> SetOperations =
    [
        nameof(Queryable.Concat),
        nameof(Queryable.Union),
        nameof(Queryable.Intersect),
        nameof(Queryable.Except),
    ];

    private readonly MethodCallExpression _call;

    public SchemaQuery(MethodCallExpression call)
    {
        _call = call;
    }

    private bool IsSequence => _call.Type.IsAssignableTo(typeof(IQueryable));

    private Type ElementType =>
        IsSequence ? _call.Type.GetGenericArguments()[0] : _call.Type;

    /// <summary>
    /// The result of the operator.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The operator does not read stored tables of one schema only, its result
    /// cannot be read, or the translator cannot turn it into SQL.
    /// </exception>
    public Expression Result()
    {
        IReadOnlyList<IQueryable> roots = new QueryRoots().In(_call);
        PostgreSqlStoredTableDataSet[] tables =
        [
            .. roots.OfType<PostgreSqlStoredTableDataSet>(),
        ];
        if (tables.Length == 0 || tables.Length != roots.Count)
        {
            throw new NotSupportedException(
                $"The query operator {_call.Method.Name} reads sources other than "
                    + "stored tables, so it cannot be translated to SQL."
            );
        }
        if (!tables.All(x => IsSameSchema(tables[0], x)))
        {
            throw new NotSupportedException(
                $"The query operator {_call.Method.Name} reads tables of more than "
                    + "one schema or connection, so it cannot be translated to SQL."
            );
        }
        ResultReader reader = new(ElementType, Table(_call));
        if (!reader.IsSupported)
        {
            throw new NotSupportedException(
                $"The query operator {_call.Method.Name} returns {ElementType}, "
                    + "which cannot be read from a SQL result."
            );
        }
        IReadOnlyList<object?> values = Values(tables[0].Connection, Sql(tables), reader);
        return IsSequence ? Expression.Constant(Typed(values).AsQueryable(), _call.Type)
            : Elements.Contains(_call.Method.Name) ? Element(Typed(values))
            : Aggregate(values[0]);
    }

    private string Sql(IEnumerable<PostgreSqlStoredTableDataSet> tables)
    {
        try
        {
            return new PostgreSqlQuery(
                new EntityExpression(tables).Visit(_call)
            ).TextValue;
        }
        catch (Exception exception)
            when (exception
                    is NotSupportedException
                        or InvalidCastException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or ArgumentException
            )
        {
            throw new NotSupportedException(
                $"The query operator {_call.Method.Name} cannot be translated to SQL.",
                exception
            );
        }
    }

    private IReadOnlyList<object?> Values(
        IDbConnection connection,
        string sql,
        ResultReader reader
    )
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        using IDataReader result = command.ExecuteReader();
        if (result.FieldCount != reader.Width)
        {
            throw new NotSupportedException(
                $"The SQL of the query operator {_call.Method.Name} returns "
                    + $"{result.FieldCount} columns where {reader.Width} are expected."
            );
        }
        Collection<object?> values = [];
        while (result.Read())
        {
            values.Add(reader.Read(result, 0));
        }
        return values;
    }

    private Array Typed(IReadOnlyList<object?> values)
    {
        Array array = Array.CreateInstance(ElementType, values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            array.SetValue(values[index], index);
        }
        return array;
    }

    private MethodCallExpression Element(Array values)
    {
        string name = _call.Method.Name;
        Expression source = Expression.Constant(values);
        return name.StartsWith(nameof(Queryable.ElementAt), StringComparison.Ordinal)
            ? Expression.Call(
                typeof(Enumerable),
                name,
                [ElementType],
                source,
                Expression.Constant(0)
            )
            : Expression.Call(
                typeof(Enumerable),
                name.Replace(
                    nameof(Queryable.Last),
                    nameof(Queryable.First),
                    StringComparison.Ordinal
                ),
                [ElementType],
                source
            );
    }

    private Expression Aggregate(object? value)
    {
        return
            value is null
            && _call.Type.IsValueType
            && Nullable.GetUnderlyingType(_call.Type) is null
            ? Expression.Throw(
                Expression.Constant(
                    new InvalidOperationException("Sequence contains no elements")
                ),
                _call.Type
            )
            : Expression.Constant(value, _call.Type);
    }

    private static bool IsSameSchema(
        PostgreSqlStoredTableDataSet first,
        PostgreSqlStoredTableDataSet second
    )
    {
        return ReferenceEquals(first.Connection, second.Connection)
            && first.SchemaName.TextValue == second.SchemaName.TextValue;
    }

    private static ITable? Table(Expression source)
    {
        return source switch
        {
            ConstantExpression { Value: PostgreSqlStoredTableDataSet table } =>
                table.TableSchema,
            MethodCallExpression call
                when call.Method.DeclaringType == typeof(Queryable)
                    && RowPreserving.Contains(call.Method.Name) => Table(
                call.Arguments[0]
            ),
            MethodCallExpression call
                when call.Method.DeclaringType == typeof(Queryable)
                    && SetOperations.Contains(call.Method.Name)
                    && Table(call.Arguments[0]) is ITable first
                    && Table(call.Arguments[1]) is ITable second
                    && new TableHash(first).SequenceEqual(new TableHash(second)) => first,
            _ => null,
        };
    }
}
