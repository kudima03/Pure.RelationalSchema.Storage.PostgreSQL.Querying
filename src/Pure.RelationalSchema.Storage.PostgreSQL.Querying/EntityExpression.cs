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
/// and the count and keys of <c>row.Cells</c> become the columns of the row's table.
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
        return base.VisitMethodCall(node);
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
