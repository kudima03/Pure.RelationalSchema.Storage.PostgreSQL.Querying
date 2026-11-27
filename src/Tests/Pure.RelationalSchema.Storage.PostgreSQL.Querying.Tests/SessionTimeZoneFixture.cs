using System.Data;
using Npgsql;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// The tables of an <see cref="ExecutionFixture"/> of its own, and connections of
/// their own to the same database whose session <c>TimeZone</c> is not UTC: one
/// fourteen hours ahead of UTC (<c>Pacific/Kiritimati</c>, so that a moment late in
/// the day falls on the next day, even past the year 9999) and one three hours and a
/// half behind it with daylight saving time (<c>America/St_Johns</c>, so that a
/// moment early in the day falls on the day before, even before the year 1). The
/// connection of the execution fixture keeps the zone of the container, UTC.
/// </summary>
public sealed record SessionTimeZoneFixture : IDisposable
{
    public const string Ahead = "Pacific/Kiritimati";

    public const string Behind = "America/St_Johns";

    private readonly Dictionary<string, NpgsqlConnection> _connections;

    public SessionTimeZoneFixture()
    {
        Execution = new ExecutionFixture();
        _connections = new Dictionary<string, NpgsqlConnection>
        {
            [Ahead] = Zoned(Ahead),
            [Behind] = Zoned(Behind),
        };
    }

    public ExecutionFixture Execution { get; }

    /// <summary>
    /// The connection whose session runs in the zone.
    /// </summary>
    public IDbConnection Connection(string zone)
    {
        return _connections[zone];
    }

    public void Dispose()
    {
        foreach (NpgsqlConnection connection in _connections.Values)
        {
            connection.Dispose();
        }
        Execution.Dispose();
    }

    private NpgsqlConnection Zoned(string zone)
    {
        NpgsqlConnection connection = (NpgsqlConnection)
            ((ICloneable)Execution.Connection).Clone();

        connection.Open();

        using NpgsqlCommand command = connection.CreateCommand();

        command.CommandText = $"SET TimeZone = '{zone}';";

        _ = command.ExecuteNonQuery();

        command.CommandText = "SHOW TimeZone;";

        Assert.Equal(zone, command.ExecuteScalar());

        return connection;
    }
}
