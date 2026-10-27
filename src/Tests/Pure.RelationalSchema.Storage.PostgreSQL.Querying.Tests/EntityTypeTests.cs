using System.Collections;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using Pure.Primitives.Abstractions.Char;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

using Column = Column.Column;
using Table = Table.Table;

/// <summary>
/// The entity type of a table is built from reflection objects the translator reads:
/// a type named after the table hash with the table attribute, and a readable
/// property per column with the column attribute, whose getter cannot be invoked.
/// </summary>
public sealed record EntityTypeTests
{
    private static readonly IColumn Id = new Column(
        new String("Id"),
        new LongColumnType()
    );

    private static readonly ITable Things = new Table(new String("Things"), [Id], []);

    private static EntityType Entity => EntityType.Of(Things, "lab");

    private static PropertyInfo Property =>
        Entity.Property(new HexString(new ColumnHash(Id)).TextValue);

    private static MethodInfo Getter => Property.GetGetMethod(false)!;

    [Fact]
    public void IsBuiltOncePerTableAndSchema()
    {
        Assert.Same(Entity, EntityType.Of(Things, "lab"));
        Assert.NotSame(Entity, EntityType.Of(Things, "shop"));
        Assert.True(Entity.Equals((object)Entity));
        Assert.False(Entity.Equals(EntityType.Of(Things, "shop")));
        Assert.Equal(Entity.GetHashCode(), EntityType.Of(Things, "lab").GetHashCode());
    }

    [Fact]
    public void IsNamedAfterTheTableHashWithTheTableAttribute()
    {
        HexString hash = new(new TableHash(Things));

        Assert.Equal(hash.TextValue, Entity.Name);
        TableAttribute table = Assert.IsType<TableAttribute>(
            Assert.Single(Entity.GetCustomAttributes(false))
        );
        Assert.Equal(new TrimmedHash(hash).TextValue, table.Name);
        Assert.Equal("lab", table.Schema);
        Assert.Null(
            Assert
                .IsType<TableAttribute>(
                    Assert.Single(
                        EntityType
                            .Of(Things, "")
                            .GetCustomAttributes(typeof(TableAttribute), false)
                    )
                )
                .Schema
        );
        Assert.Empty(Entity.GetCustomAttributes(typeof(ColumnAttribute), false));
        Assert.True(Entity.IsDefined(typeof(Attribute), false));
        Assert.False(Entity.IsDefined(typeof(ColumnAttribute), false));
        Assert.Empty(Entity.GetCustomAttributesData());
    }

    [Fact]
    public void HasAReadablePropertyPerColumn()
    {
        PropertyInfo property = Assert.Single(
            Entity.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        );

        Assert.Same(Property, property);
        Assert.Equal(typeof(long?), property.PropertyType);
        Assert.Same(Entity, property.DeclaringType);
        Assert.Same(Entity, property.ReflectedType);
        Assert.Same(typeof(ElementRow).Module, property.Module);
        Assert.Equal(PropertyAttributes.None, property.Attributes);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
        Assert.Null(property.GetSetMethod(true));
        Assert.Empty(property.GetIndexParameters());
        Assert.Same(Getter, Assert.Single(property.GetAccessors(true)));
        _ = Assert.Throws<NotSupportedException>(() => property.GetValue(null));
        _ = Assert.Throws<NotSupportedException>(() => property.SetValue(null, null));
        ColumnAttribute column = Assert.IsType<ColumnAttribute>(
            Assert.Single(property.GetCustomAttributes(false))
        );
        Assert.Equal(
            new TrimmedHash(new HexString(new ColumnHash(Id))).TextValue,
            column.Name
        );
        _ = Assert.Single(property.GetCustomAttributes(typeof(ColumnAttribute), false));
        Assert.Empty(property.GetCustomAttributes(typeof(TableAttribute), false));
        Assert.True(property.IsDefined(typeof(ColumnAttribute), false));
        Assert.Empty(property.GetCustomAttributesData());
    }

    [Fact]
    public void HasAGetterThatCannotBeInvoked()
    {
        MethodInfo getter = Getter;

        Assert.Equal("get_" + Property.Name, getter.Name);
        Assert.Same(Entity, getter.DeclaringType);
        Assert.Same(Entity, getter.ReflectedType);
        Assert.Same(Property.Module, getter.Module);
        Assert.Equal(typeof(long?), getter.ReturnType);
        Assert.Same(getter.ReturnParameter, getter.ReturnTypeCustomAttributes);
        Assert.Equal(
            MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.SpecialName,
            getter.Attributes
        );
        Assert.Equal(
            CallingConventions.Standard | CallingConventions.HasThis,
            getter.CallingConvention
        );
        Assert.Same(getter, getter.GetBaseDefinition());
        Assert.Equal(MethodImplAttributes.IL, getter.GetMethodImplementationFlags());
        Assert.Empty(getter.GetParameters());
        _ = Assert.Throws<NotSupportedException>(() => getter.MethodHandle);
        _ = Assert.Throws<NotSupportedException>(() => getter.Invoke(null, null));
        Assert.Empty(getter.GetCustomAttributes(false));
        Assert.Empty(getter.GetCustomAttributes(typeof(Attribute), false));
        Assert.False(getter.IsDefined(typeof(Attribute), false));
        Assert.Empty(getter.GetCustomAttributesData());
    }

    [Fact]
    public void ReturnsAParameterWithoutAttributes()
    {
        ParameterInfo result = Getter.ReturnParameter;

        Assert.Equal(typeof(long?), result.ParameterType);
        Assert.Equal(-1, result.Position);
        Assert.Equal(ParameterAttributes.Retval, result.Attributes);
        Assert.Same(Getter, result.Member);
        Assert.Empty(result.GetCustomAttributes(false));
        Assert.Empty(result.GetCustomAttributes(typeof(Attribute), false));
        Assert.False(result.IsDefined(typeof(Attribute), false));
        Assert.Empty(result.GetCustomAttributesData());
    }

    [Fact]
    public void StandsForARowWhoseCellsAreNeverRead()
    {
        ElementRow row = new(Things);

        Assert.Same(Things, row.Table);
        _ = Assert.Throws<NotSupportedException>(() => row.Cells);
    }

    [Fact]
    public void RootsAreNeverEnumerated()
    {
        TableRoot root = new(Entity);

        Assert.Same(Entity, root.ElementType);
        ConstantExpression constant = Assert.IsType<ConstantExpression>(
            root.Expression,
            false
        );
        Assert.Same(root, constant.Value);
        Assert.Equal(typeof(IQueryable<IRow>), constant.Type);
        _ = Assert.Throws<NotSupportedException>(() => root.Provider);
        _ = Assert.Throws<NotSupportedException>(root.GetEnumerator);
        _ = Assert.Throws<NotSupportedException>(() =>
            ((IEnumerable)root).GetEnumerator()
        );
    }

    [Fact]
    public void StatementsAndHashesEnumerateTheirCharacters()
    {
        SelectAllStatement statement = new(Things);
        IEnumerable characters = statement;
        HexString hash = new(new TableHash(Things));
        TrimmedHash trimmed = new(hash);
        IEnumerable trimmedCharacters = trimmed;

        Assert.Equal(
            statement.TextValue,
            string.Concat(statement.Select(x => x.CharValue))
        );
        Assert.Equal(statement.TextValue, Enumerated(characters));
        Assert.StartsWith("SELECT ", statement.TextValue, StringComparison.Ordinal);
        Assert.EndsWith(
            $" FROM \"{hash.TextValue}\";",
            statement.TextValue,
            StringComparison.Ordinal
        );
        Assert.Equal(63, trimmed.TextValue.Length);
        Assert.Equal(trimmed.TextValue, string.Concat(trimmed.Select(x => x.CharValue)));
        Assert.Equal(trimmed.TextValue, Enumerated(trimmedCharacters));
    }

    /// <summary>
    /// The characters of a string enumerated through its non-generic enumerator.
    /// </summary>
    private static string Enumerated(IEnumerable characters)
    {
        IEnumerator enumerator = characters.GetEnumerator();
        List<char> values = [];
        while (enumerator.MoveNext())
        {
            values.Add(((IChar)enumerator.Current!).CharValue);
        }
        return string.Concat(values);
    }
}
