using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// The expression an element of a query stands for, with the parameters of the
/// projections replaced by the elements of their sources: a constant
/// <see cref="ElementRow"/> for a row of a stored table (optional after
/// <c>DefaultIfEmpty</c>), the constructor call of an anonymous type, value tuple
/// or record for its members, the initializer of an object, a
/// <see cref="SequenceElement"/> for a grouping, or null when it is not known. The
/// operators that return elements of their source, the <c>...By</c> ones among
/// them, keep its element, and so does a set operation, whose sides have one
/// element (<see cref="RowSetOperations"/> rejects rows of different tables).
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
        nameof(Queryable.DistinctBy),
        nameof(Queryable.ExceptBy),
        nameof(Queryable.IntersectBy),
        nameof(Queryable.UnionBy),
        nameof(Queryable.MinBy),
        nameof(Queryable.MaxBy),
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
    /// The row of a stored table an element is, when it is one.
    /// </summary>
    public static ElementRow? RowOf(Expression? element)
    {
        return Resolved(element) is ConstantExpression { Value: ElementRow row }
            ? row
            : null;
    }

    /// <summary>
    /// The table whose row an element is, when it is one.
    /// </summary>
    public static ITable? TableOf(Expression? element)
    {
        return RowOf(element)?.Table;
    }

    /// <summary>
    /// Whether the elements of two queries have one shape: rows of one table, or
    /// constructed objects whose rows, member by member, are rows of one table.
    /// </summary>
    public static bool AreOfOneShape(Expression first, Expression second)
    {
        return HaveOneShape(Of(first), Of(second));
    }

    /// <summary>
    /// Whether the initializer of an object assigns the member a constructor
    /// parameter initializes, by name: the assignment replaces the argument, which
    /// the translator then does not project.
    /// </summary>
    public static bool IsReplaced(
        ParameterInfo parameter,
        MemberInitExpression initialization
    )
    {
        return initialization
            .Bindings.OfType<MemberAssignment>()
            .Any(x =>
                string.Equals(x.Member.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
            );
    }

    /// <summary>
    /// The element, with the member of a constructed object replaced by the
    /// argument or assignment that initializes it — by name for an anonymous type
    /// or an initializer, by the name of the constructor parameter for a value
    /// tuple or a record — and the key of a grouping by the key it is grouped by.
    /// </summary>
    public static Expression? Resolved(Expression? element)
    {
        if (element is not MemberExpression { Expression: not null } member)
        {
            return element;
        }
        Expression? bound = Resolved(member.Expression) switch
        {
            SequenceElement sequence
                when member.Member.Name == nameof(IGrouping<,>.Key) => sequence.Key,
            NewExpression creation => Argument(creation, member.Member),
            MemberInitExpression initialization => Assigned(
                initialization,
                member.Member
            ),
            _ => null,
        };
        return bound is null ? element : Resolved(bound);
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

    private static Expression? Argument(NewExpression creation, MemberInfo member)
    {
        int index =
            creation.Members is ReadOnlyCollection<MemberInfo> members
                ? members.Select(x => x.Name).ToList().IndexOf(member.Name)
                : Array.FindIndex(
                    creation.Constructor?.GetParameters() ?? [],
                    x => string.Equals(x.Name, member.Name, StringComparison.OrdinalIgnoreCase)
                );
        return index < 0 ? null : creation.Arguments[index];
    }

    private static Expression? Assigned(
        MemberInitExpression initialization,
        MemberInfo member
    )
    {
        return initialization
                .Bindings.OfType<MemberAssignment>()
                .FirstOrDefault(x => x.Member.Name == member.Name)
                ?.Expression ?? Argument(initialization.NewExpression, member);
    }

    private static bool HaveOneShape(Expression? first, Expression? second)
    {
        Expression? left = Resolved(first);
        Expression? right = Resolved(second);
        if (
            left is ConstantExpression { Value: ElementRow }
            || right is ConstantExpression { Value: ElementRow }
        )
        {
            return left is ConstantExpression { Value: ElementRow one }
                && right is ConstantExpression { Value: ElementRow other }
                && new TableHash(one.Table).SequenceEqual(new TableHash(other.Table));
        }
        IReadOnlyList<Expression>? these = Components(left);
        IReadOnlyList<Expression>? those = Components(right);
        return these is null
            || those is null
            || (
                these.Count == those.Count
                && these.Zip(those).All(x => HaveOneShape(x.First, x.Second))
            );
    }

    /// <summary>
    /// The expressions a constructed object is built from, when it is one.
    /// </summary>
    private static IReadOnlyList<Expression>? Components(Expression? element)
    {
        return element switch
        {
            NewExpression creation => creation.Arguments,
            MemberInitExpression initialization =>
            [
                .. initialization.NewExpression.Arguments.Where(
                    (_, index) =>
                        !IsReplaced(
                            initialization.NewExpression.Constructor!.GetParameters()[index],
                            initialization
                        )
                ),
                .. initialization.Bindings.OfType<MemberAssignment>().Select(x => x.Expression),
            ],
            _ => null,
        };
    }

    private static Expression? Of(Expression? query)
    {
        return Resolved(query) switch
        {
            ConstantExpression { Value: PostgreSqlStoredTableDataSet table } =>
                Expression.Constant(new ElementRow(table.TableSchema), typeof(IRow)),
            SequenceElement sequence => sequence.Element,
            MethodCallExpression call when IsOperator(call) => Of(call),
            _ => null,
        };
    }

    private static bool IsOperator(MethodCallExpression call)
    {
        return call.Method.DeclaringType == typeof(Queryable)
            || call.Method.DeclaringType == typeof(Enumerable);
    }

    private static Expression? Of(MethodCallExpression call)
    {
        string name = call.Method.Name;
        return RowPreserving.Contains(name) || SetOperations.Contains(name)
            ? Of(call.Arguments[0])
            : name switch
            {
                nameof(Queryable.Select) => Projected(call, 1),
                nameof(Queryable.Join) or nameof(Queryable.GroupJoin) => Projected(
                    call,
                    4
                ),
                nameof(Queryable.SelectMany) when call.Arguments.Count == 3 => Projected(
                    call,
                    2
                ),
                nameof(Queryable.SelectMany) => Collection(call),
                nameof(Queryable.GroupBy) => Grouped(call),
                nameof(Queryable.DefaultIfEmpty) when call.Arguments.Count == 1 =>
                    Optional(Of(call.Arguments[0])),
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
    /// The element of a <c>GroupBy</c>: what its result selector builds from the key
    /// and the group, or else the grouping itself.
    /// </summary>
    private static Expression? Grouped(MethodCallExpression call)
    {
        int result = ResultSelector(call);
        return result > 0
            ? Projected(call, result)
            : new SequenceElement(
                call.Type.GetGenericArguments()[0],
                GroupedElement(call),
                Projected(call, 1)
            );
    }

    /// <summary>
    /// The index of the result selector of a <c>GroupBy</c>, the lambda of a key
    /// and a group, or -1 when it has none.
    /// </summary>
    private static int ResultSelector(MethodCallExpression call)
    {
        for (int index = 2; index < call.Arguments.Count; index++)
        {
            if (Lambda(call.Arguments[index]) is { Parameters.Count: 2 })
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// The element of the groups of a <c>GroupBy</c>: what its element selector, a
    /// lambda of one parameter, projects, or else the element of its source.
    /// </summary>
    private static Expression? GroupedElement(MethodCallExpression call)
    {
        return call.Arguments.Count > 2
            && Lambda(call.Arguments[2]) is { Parameters.Count: 1 }
            ? Projected(call, 2)
            : Of(call.Arguments[0]);
    }

    /// <summary>
    /// The element of a <c>DefaultIfEmpty</c>: a row that may be missing.
    /// </summary>
    private static Expression? Optional(Expression? element)
    {
        return RowOf(element) is ElementRow row
            ? Expression.Constant(new ElementRow(row.Table, true), typeof(IRow))
            : element;
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
        LambdaExpression? lambda = Lambda(call.Arguments[index]);
        return (call.Method.Name, index) switch
        {
            (nameof(Queryable.Join) or nameof(Queryable.GroupJoin), 3) =>
            [
                Of(call.Arguments[1]),
            ],
            (nameof(Queryable.Join), 4) => [outer, Of(call.Arguments[1])],
            (nameof(Queryable.GroupJoin), 4) when lambda is not null =>
            [
                outer,
                new SequenceElement(lambda.Parameters[1].Type, Of(call.Arguments[1]), null),
            ],
            (nameof(Queryable.SelectMany), 2) => [outer, Collection(call)],
            (nameof(Queryable.GroupBy), _) when lambda is { Parameters.Count: 2 } =>
            [
                Projected(call, 1),
                new SequenceElement(
                    lambda.Parameters[1].Type,
                    GroupedElement(call),
                    Projected(call, 1)
                ),
            ],
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
