using System.Linq.Expressions;
using Pure.Primitives.Abstractions.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// Rewrites a tree over stored tables of one schema into the tree the translator
/// maps: each table becomes a root of its <see cref="EntityType"/>, and each
/// <c>row.Cells[column].Value.TextValue</c> becomes the text of the column property.
/// </summary>
internal sealed class EntityExpression : ExpressionVisitor
{
    private readonly Dictionary<object, Expression> _roots = new(
        ReferenceEqualityComparer.Instance
    );

    private readonly Dictionary<string, (Type Entity, ColumnProperty Column)> _columns =
    [];

    public EntityExpression(IEnumerable<PostgreSqlStoredTableDataSet> tables)
    {
        foreach (PostgreSqlStoredTableDataSet table in tables)
        {
            Type entity = new EntityType(table.TableSchema, table.SchemaName).Value;
            _roots[table] = new TableRoot(entity).Expression;
            foreach (IColumn column in table.TableSchema.Columns)
            {
                ColumnProperty property = new(column);
                _ = _columns.TryAdd(property.Name, (entity, property));
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

    protected override Expression VisitMember(MemberExpression node)
    {
        return Cell(node) is MethodCallExpression cell
            ? Text(((MemberExpression)cell.Object!).Expression!, cell.Arguments[0])
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
                        Object: MemberExpression
                        {
                            Member.Name: nameof(IRow.Cells)
                        } cells,
                    } cell,
                },
            } when cells.Member.DeclaringType == typeof(IRow) => cell,
            _ => null,
        };
    }

    private Expression Text(Expression row, Expression column)
    {
        (Type Entity, ColumnProperty Column) mapped = Mapped(column);
        return mapped.Column.Text(
            Expression.Property(
                Expression.Convert(Visit(row), mapped.Entity),
                mapped.Entity.GetProperty(mapped.Column.Name)!
            )
        );
    }

    private (Type Entity, ColumnProperty Column) Mapped(Expression column)
    {
        return _columns.TryGetValue(
            new ColumnProperty((IColumn)new ClosureValue(column).Value!).Name,
            out (Type Entity, ColumnProperty Column) mapped
        )
            ? mapped
            : throw new NotSupportedException();
    }
}
