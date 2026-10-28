using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Pure.Primitives.Abstractions.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Rewrites a tree over stored tables into the tree the translator maps: each table
/// becomes a root of its <see cref="EntityType"/>, each
/// <c>row.Cells[column].Value.TextValue</c> becomes the text of the column property,
/// a parse of that text (<c>Guid.Parse(text)</c>, <c>DateOnly.Parse(text,
/// InvariantCulture)</c>, ...), bare or guarded by <c>string.IsNullOrEmpty(text) ?
/// null : ...</c>, becomes the column property itself, read as its type (see
/// <see cref="ColumnProperty.Typed"/>), <c>string.IsNullOrEmpty(text)</c> of such a
/// column becomes its test for NULL, and the count and keys of <c>row.Cells</c>
/// become the columns of the row's table.
/// </summary>
internal sealed class EntityExpression : ExpressionVisitor
{
    private readonly Dictionary<object, Expression> _roots = new(
        ReferenceEqualityComparer.Instance
    );

    private readonly Dictionary<
        string,
        (PropertyInfo Property, ColumnProperty Column)
    > _columns = [];

    private readonly Dictionary<ParameterExpression, Expression> _bindings = [];

    public EntityExpression(IEnumerable<PostgreSqlStoredTableDataSet> tables)
    {
        foreach (PostgreSqlStoredTableDataSet table in tables)
        {
            EntityType entity = EntityType.Of(
                table.TableSchema,
                table.SchemaName.TextValue
            );
            _roots[table] = new TableRoot(entity).Expression;
            foreach (IColumn column in table.TableSchema.Columns)
            {
                ColumnProperty property = new(column);
                _ = _columns.TryAdd(
                    property.Name,
                    (entity.Property(property.Name), property)
                );
            }
        }
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        return
            node.Value is not null && _roots.TryGetValue(node.Value, out Expression? root)
            ? root
            : node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(Queryable))
        {
            IReadOnlyDictionary<ParameterExpression, Expression> bindings =
                QueryElement.Bindings(node);
            foreach (KeyValuePair<ParameterExpression, Expression> binding in bindings)
            {
                _bindings[binding.Key] = binding.Value;
            }
        }
        if (Parsed(node) is MethodCallExpression parsed)
        {
            Expression? typed = Typed(parsed, node.Type, false);
            if (typed is not null)
            {
                return typed;
            }
        }
        if (Empty(node) is MethodCallExpression tested)
        {
            Type column = Column(tested).Column.Type;
            Expression? property = Typed(
                tested,
                Nullable.GetUnderlyingType(column) ?? column,
                true
            );
            if (property is not null)
            {
                return Expression.Equal(
                    property,
                    Expression.Constant(null, property.Type)
                );
            }
        }
        return base.VisitMethodCall(node);
    }

    /// <summary>
    /// A cell read as a typed value, null where the text is empty, is the column
    /// property: <c>string.IsNullOrEmpty(text) ? null : (T?)T.Parse(text)</c>.
    /// </summary>
    protected override Expression VisitConditional(ConditionalExpression node)
    {
        return Empty(node.Test) is MethodCallExpression tested
            && node.IfTrue is ConstantExpression { Value: null }
            && Unconverted(node.IfFalse, node.Type) is MethodCallExpression parse
            && Parsed(parse) is MethodCallExpression parsed
            && SameCell(tested, parsed)
            && Typed(parsed, Nullable.GetUnderlyingType(node.Type) ?? node.Type, true)
                is Expression typed
            ? typed.Type == node.Type ? typed : Expression.Convert(typed, node.Type)
            : base.VisitConditional(node);
    }

    /// <summary>
    /// The cell whose text a static <c>Parse</c> of a scalar type reads, with any
    /// other argument a constant (the invariant culture, number or date styles that
    /// assume no local time).
    /// </summary>
    private static MethodCallExpression? Parsed(MethodCallExpression node)
    {
        return node is { Object: null, Method.Name: nameof(int.Parse) }
            && node.Arguments.Count > 0
            && node.Arguments[0] is MemberExpression text
            && node.Type == node.Method.DeclaringType
            && node.Arguments.Skip(1).All(IsInvariant)
            ? Cell(text)
            : null;
    }

    /// <summary>
    /// Whether an argument of a parse, a constant or a static member such as
    /// <c>CultureInfo.InvariantCulture</c>, reads the text as the cell writes it.
    /// </summary>
    private static bool IsInvariant(Expression option)
    {
        return option is ConstantExpression or MemberExpression { Expression: null }
            && new ClosureValue(option).Value switch
            {
                IFormatProvider provider => ReferenceEquals(
                    provider,
                    CultureInfo.InvariantCulture
                ),
                DateTimeStyles styles => (styles & DateTimeStyles.AssumeLocal) == 0,
                NumberStyles => true,
                _ => false,
            };
    }

    /// <summary>
    /// The cell whose text <c>string.IsNullOrEmpty</c> tests.
    /// </summary>
    private static MethodCallExpression? Empty(Expression node)
    {
        return node
            is MethodCallExpression
        {
            Method: { Name: nameof(string.IsNullOrEmpty), DeclaringType: Type owner },
            Arguments: [MemberExpression text],
        }
            && owner == typeof(string)
            ? Cell(text)
            : null;
    }

    private static Expression Unconverted(Expression node, Type type)
    {
        Expression value = node;
        while (
            value is UnaryExpression { NodeType: ExpressionType.Convert } conversion
            && (value.Type == type || Nullable.GetUnderlyingType(value.Type) is null)
        )
        {
            value = conversion.Operand;
        }
        return value;
    }

    /// <summary>
    /// Whether two cell reads read the same column of the same row.
    /// </summary>
    private static bool SameCell(MethodCallExpression first, MethodCallExpression second)
    {
        return Same(first.Arguments[0], second.Arguments[0])
            && Same(
                ((MemberExpression)first.Object!).Expression!,
                ((MemberExpression)second.Object!).Expression!
            );
    }

    private static bool Same(Expression first, Expression second)
    {
        return ReferenceEquals(first, second)
            || (first, second) switch
            {
                (ConstantExpression left, ConstantExpression right) => Equals(
                    left.Value,
                    right.Value
                ),
                (MemberExpression left, MemberExpression right) =>
                    left.Member == right.Member
                    && left.Expression is not null
                    && right.Expression is not null
                    && Same(left.Expression, right.Expression),
                (UnaryExpression left, UnaryExpression right) =>
                    left.NodeType == right.NodeType
                    && left.Type == right.Type
                    && Same(left.Operand, right.Operand),
                (MethodCallExpression left, MethodCallExpression right) =>
                    left.Method == right.Method
                    && (left.Object, right.Object) switch
                    {
                        (null, null) => true,
                        (Expression target, Expression other) => Same(target, other),
                        _ => false,
                    }
                    && left.Arguments.Count == right.Arguments.Count
                    && left.Arguments.Zip(right.Arguments).All(pair => Same(pair.First, pair.Second)),
                _ => false,
            };
    }

    /// <summary>
    /// The column property of a cell read as the parsed type, when it is read so.
    /// </summary>
    private Expression? Typed(MethodCallExpression cell, Type parsed, bool nullable)
    {
        (PropertyInfo Property, ColumnProperty Column) mapped = Column(cell);
        return mapped.Column.Typed(Property(cell, mapped.Property), parsed, nullable);
    }

    private (PropertyInfo Property, ColumnProperty Column) Column(
        MethodCallExpression cell
    )
    {
        return Mapped(Visit(cell.Arguments[0]));
    }

    private MemberExpression Property(MethodCallExpression cell, PropertyInfo property)
    {
        Expression row = ((MemberExpression)cell.Object!).Expression!;
        return Expression.Property(
            Expression.Convert(Visit(row), property.DeclaringType!),
            property
        );
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        return Cell(node) is MethodCallExpression cell
                ? Text(((MemberExpression)cell.Object!).Expression!, cell.Arguments[0])
            : Cells(node) is MemberExpression cells ? Columns(node, cells.Expression!)
            : base.VisitMember(node);
    }

    private static MethodCallExpression? Cell(MemberExpression node)
    {
        return node switch
        {
            {
                Member.Name: nameof(IString.TextValue),
                Expression: MemberExpression
                {
                    Member.Name: nameof(ICell.Value),
                    Expression: MethodCallExpression
                    {
                        Object: MemberExpression cells
                    } cell,
                },
            } when IsCells(cells) => cell,
            _ => null,
        };
    }

    /// <summary>
    /// The <c>row.Cells</c> whose count or keys the node reads.
    /// </summary>
    private static MemberExpression? Cells(MemberExpression node)
    {
        return node
            is
        {
            Member.Name: nameof(IReadOnlyCollection<>.Count)
                    or nameof(IReadOnlyDictionary<,>.Keys),
            Expression: MemberExpression cells,
        }
            && IsCells(cells)
            ? cells
            : null;
    }

    private static bool IsCells(MemberExpression node)
    {
        return node.Member.Name == nameof(IRow.Cells)
            && node.Member.DeclaringType == typeof(IRow);
    }

    private Expression Text(Expression row, Expression column)
    {
        (PropertyInfo Property, ColumnProperty Column) mapped = Mapped(Visit(column));
        return mapped.Column.Text(
            Expression.Property(
                Expression.Convert(Visit(row), mapped.Property.DeclaringType!),
                mapped.Property
            )
        );
    }

    private (PropertyInfo Property, ColumnProperty Column) Mapped(Expression column)
    {
        return _columns.TryGetValue(
            new ColumnProperty((IColumn)new ClosureValue(column).Value!).Name,
            out (PropertyInfo Property, ColumnProperty Column) mapped
        )
            ? mapped
            : throw new NotSupportedException();
    }

    /// <summary>
    /// The count or keys of the cells of a row, which are the columns of its table.
    /// </summary>
    private ConstantExpression Columns(MemberExpression node, Expression row)
    {
        ITable table =
            QueryElement.TableOf(new Substitution(_bindings).Visit(row))
            ?? throw new NotSupportedException();
        IColumn[] columns = [.. table.Columns];
        return Expression.Constant(
            node.Member.Name == nameof(IReadOnlyDictionary<,>.Keys)
                ? columns
                : columns.Length,
            node.Type
        );
    }
}
