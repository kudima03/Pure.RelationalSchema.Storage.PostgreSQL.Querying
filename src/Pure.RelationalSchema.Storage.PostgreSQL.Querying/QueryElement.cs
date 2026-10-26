using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// The expression an element of a query stands for, with the parameters of the
/// projections replaced by the elements of their sources: a constant
/// <see cref="ElementRow"/> for a row of a stored table, the constructor call of an
/// anonymous type or value tuple for its members, or null when it is not known.
/// </summary>
internal sealed record QueryElement
{
    private static readonly HashSet<string> RowPreserving =
    [
        nameof(Queryable.First),
        nameof(Queryable.FirstOrDefault),
        nameof(Queryable.Single),
        nameof(Queryable.SingleOrDefault),
        nameof(Queryable.Last),
        nameof(Queryable.LastOrDefault),
        nameof(Queryable.ElementAt),
        nameof(Queryable.ElementAtOrDefault),
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
        nameof(Queryable.Cast),
        nameof(Queryable.OfType),
    ];

    private static readonly HashSet<string> SetOperations =
    [
        nameof(Queryable.Concat),
        nameof(Queryable.Union),
        nameof(Queryable.Intersect),
        nameof(Queryable.Except),
    ];

    public QueryElement(Expression query)
    {
        Value = query;
    }

    public Expression? Value => Of(field);

    public static bool IsSetOperation(MethodCallExpression call)
    {
        return call.Method.DeclaringType == typeof(Queryable)
            && SetOperations.Contains(call.Method.Name);
    }

    /// <summary>
    /// The table whose row an element is, when it is one.
    /// </summary>
    public static ITable? TableOf(Expression? element)
    {
        return Resolved(element) is ConstantExpression { Value: ElementRow row }
            ? row.Table
            : null;
    }

    /// <summary>
    /// Whether the elements of two queries are rows of one table.
    /// </summary>
    public static bool AreRowsOfOneTable(Expression first, Expression second)
    {
        return TableOf(Of(first)) is ITable left
            && TableOf(Of(second)) is ITable right
            && new TableHash(left).SequenceEqual(new TableHash(right));
    }

    /// <summary>
    /// The element, with the member of a constructed object replaced by the
    /// argument that initializes it.
    /// </summary>
    public static Expression? Resolved(Expression? element)
    {
        if (
            element is not MemberExpression { Expression: not null } member
            || Resolved(member.Expression)
                is not NewExpression { Members: not null } owner
        )
        {
            return element;
        }
        int index = owner
            .Members.Select(x => x.Name)
            .ToList()
            .IndexOf(member.Member.Name);
        return index < 0 ? element : Resolved(owner.Arguments[index]);
    }

    /// <summary>
    /// The elements that the parameters of the lambdas of a query operator stand for.
    /// </summary>
    public static IReadOnlyDictionary<ParameterExpression, Expression> Bindings(
        MethodCallExpression call
    )
    {
        Dictionary<ParameterExpression, Expression> bindings = [];
        for (int index = 1; index < call.Arguments.Count; index++)
        {
            if (Lambda(call.Arguments[index]) is LambdaExpression lambda)
            {
                foreach (
                    KeyValuePair<ParameterExpression, Expression> binding in Bound(
                        lambda,
                        Sources(call, index)
                    )
                )
                {
                    bindings[binding.Key] = binding.Value;
                }
            }
        }
        return bindings;
    }

    private static Expression? Of(Expression? query)
    {
        return Resolved(query) switch
        {
            ConstantExpression { Value: PostgreSqlStoredTableDataSet table } =>
                Expression.Constant(new ElementRow(table.TableSchema), typeof(IRow)),
            MethodCallExpression call
                when call.Method.DeclaringType == typeof(Queryable) => Of(call),
            _ => null,
        };
    }

    private static Expression? Of(MethodCallExpression call)
    {
        string name = call.Method.Name;
        return RowPreserving.Contains(name)
            ? Of(call.Arguments[0])
            : SetOperations.Contains(name)
            ? AreRowsOfOneTable(call.Arguments[0], call.Arguments[1])
                ? Of(call.Arguments[0])
                : null
            : name switch
            {
                nameof(Queryable.Select) => Projected(call, 1),
                nameof(Queryable.Join) => Projected(call, 4),
                nameof(Queryable.SelectMany) when call.Arguments.Count == 3 => Projected(
                    call,
                    2
                ),
                nameof(Queryable.SelectMany) => Collection(call),
                _ => null,
            };
    }

    /// <summary>
    /// The body of the lambda at the index, over the elements of its sources.
    /// </summary>
    private static Expression? Projected(MethodCallExpression call, int index)
    {
        return Lambda(call.Arguments[index]) is LambdaExpression lambda
            ? new Substitution(Bound(lambda, Sources(call, index))).Visit(lambda.Body)
            : null;
    }

    /// <summary>
    /// The element of the collection a <c>SelectMany</c> flattens.
    /// </summary>
    private static Expression? Collection(MethodCallExpression call)
    {
        return Projected(call, 1) is Expression collection ? Of(collection) : null;
    }

    /// <summary>
    /// The elements the parameters of the lambda at the index stand for, in order.
    /// </summary>
    private static IReadOnlyList<Expression?> Sources(
        MethodCallExpression call,
        int index
    )
    {
        Expression? outer = Of(call.Arguments[0]);
        return (call.Method.Name, index) switch
        {
            (nameof(Queryable.Join) or nameof(Queryable.GroupJoin), 3) =>
            [
                Of(call.Arguments[1]),
            ],
            (nameof(Queryable.Join), 4) => [outer, Of(call.Arguments[1])],
            (nameof(Queryable.SelectMany), 2) => [outer, Collection(call)],
            _ => [outer],
        };
    }

    private static IReadOnlyDictionary<ParameterExpression, Expression> Bound(
        LambdaExpression lambda,
        IReadOnlyList<Expression?> sources
    )
    {
        Dictionary<ParameterExpression, Expression> bindings = [];
        for (
            int index = 0;
            index < Math.Min(lambda.Parameters.Count, sources.Count);
            index++
        )
        {
            if (sources[index] is Expression source)
            {
                bindings[lambda.Parameters[index]] = source;
            }
        }
        return bindings;
    }

    private static LambdaExpression? Lambda(Expression argument)
    {
        return argument is UnaryExpression { NodeType: ExpressionType.Quote } quote
            ? quote.Operand as LambdaExpression
            : argument as LambdaExpression;
    }
}
