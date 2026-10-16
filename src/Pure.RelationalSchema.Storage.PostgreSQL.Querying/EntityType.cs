using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying;

/// <summary>
/// A type generated for a stored table, so that the translator, which maps a table
/// from the element type of a query root, names the table and its columns by their
/// hashes. It is abstract: its properties are only read by the translator, never
/// invoked.
/// </summary>
internal sealed record EntityType
{
    private static readonly ModuleBuilder Module = AssemblyBuilder
        .DefineDynamicAssembly(
            new AssemblyName(
                "Pure.RelationalSchema.Storage.PostgreSQL.Querying.Entities"
            ),
            AssemblyBuilderAccess.Run
        )
        .DefineDynamicModule("Entities");

    private static readonly Dictionary<(string Schema, string Table), Type> Types = [];

    private readonly ITable _table;

    private readonly IString _schemaName;

    public EntityType(ITable table, IString schemaName)
    {
        _table = table;
        _schemaName = schemaName;
    }

    public Type Value
    {
        get
        {
            string schema = _schemaName.TextValue;
            (string, string) key = (
                schema,
                new HexString(new TableHash(_table)).TextValue
            );
            lock (Types)
            {
                if (!Types.TryGetValue(key, out Type? type))
                {
                    type = Emitted(schema, Types.Count);
                    Types.Add(key, type);
                }
                return type;
            }
        }
    }

    private Type Emitted(string schema, int number)
    {
        TypeBuilder type = Module.DefineType(
            number.ToString(CultureInfo.InvariantCulture),
            TypeAttributes.Public | TypeAttributes.Abstract
        );
        type.SetCustomAttribute(
            new CustomAttributeBuilder(
                typeof(TableAttribute).GetConstructor([typeof(string)])!,
                [new TrimmedHash(new HexString(new TableHash(_table))).TextValue],
                schema.Length == 0
                    ? []
                    :
                    [
                        typeof(TableAttribute).GetProperty(
                            nameof(TableAttribute.Schema)
                        )!,
                    ],
                schema.Length == 0 ? [] : [schema]
            )
        );
        foreach (
            ColumnProperty column in _table.Columns.Select(x => new ColumnProperty(x))
        )
        {
            PropertyBuilder property = type.DefineProperty(
                column.Name,
                PropertyAttributes.None,
                column.Type,
                Type.EmptyTypes
            );
            property.SetGetMethod(
                type.DefineMethod(
                    column.Name,
                    MethodAttributes.Public
                        | MethodAttributes.Abstract
                        | MethodAttributes.Virtual
                        | MethodAttributes.SpecialName,
                    column.Type,
                    Type.EmptyTypes
                )
            );
            property.SetCustomAttribute(
                new CustomAttributeBuilder(
                    typeof(ColumnAttribute).GetConstructor([typeof(string)])!,
                    [column.ColumnName]
                )
            );
        }
        return type.CreateType();
    }
}
