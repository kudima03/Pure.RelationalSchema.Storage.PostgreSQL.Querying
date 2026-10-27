using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Pure.RelationalSchema.Storage.PostgreSQL.Querying.Tests;

/// <summary>
/// A connection that records the statements run through it, each rewritten by the
/// optional rewriting before it runs.
/// </summary>
internal sealed class RecordingConnection(
    IDbConnection connection,
    Func<string, string>? rewriting = null
) : IDbConnection
{
    private readonly IDbConnection _connection = connection;

    private readonly Func<string, string> _rewriting = rewriting ?? (x => x);

    private readonly List<string> _statements = [];

    public IReadOnlyList<string> Statements => _statements;

    [AllowNull]
    public string ConnectionString
    {
        get => _connection.ConnectionString;
        set => _connection.ConnectionString = value;
    }

    public int ConnectionTimeout => _connection.ConnectionTimeout;

    public string Database => _connection.Database;

    public ConnectionState State => _connection.State;

    public IDbTransaction BeginTransaction()
    {
        return _connection.BeginTransaction();
    }

    public IDbTransaction BeginTransaction(IsolationLevel il)
    {
        return _connection.BeginTransaction(il);
    }

    public void ChangeDatabase(string databaseName)
    {
        _connection.ChangeDatabase(databaseName);
    }

    public void Close()
    {
        _connection.Close();
    }

    public IDbCommand CreateCommand()
    {
        return new RecordingCommand(_connection.CreateCommand(), _statements, _rewriting);
    }

    public void Open()
    {
        _connection.Open();
    }

    public void Dispose() { }

    private sealed class RecordingCommand(
        IDbCommand command,
        List<string> statements,
        Func<string, string> rewriting
    ) : IDbCommand
    {
        private readonly IDbCommand _command = command;

        private readonly List<string> _statements = statements;

        private readonly Func<string, string> _rewriting = rewriting;

        [AllowNull]
        public string CommandText
        {
            get => _command.CommandText;
            set => _command.CommandText = value;
        }

        public int CommandTimeout
        {
            get => _command.CommandTimeout;
            set => _command.CommandTimeout = value;
        }

        public CommandType CommandType
        {
            get => _command.CommandType;
            set => _command.CommandType = value;
        }

        public IDbConnection? Connection
        {
            get => _command.Connection;
            set => _command.Connection = value;
        }

        public IDataParameterCollection Parameters => _command.Parameters;

        public IDbTransaction? Transaction
        {
            get => _command.Transaction;
            set => _command.Transaction = value;
        }

        public UpdateRowSource UpdatedRowSource
        {
            get => _command.UpdatedRowSource;
            set => _command.UpdatedRowSource = value;
        }

        public void Cancel()
        {
            _command.Cancel();
        }

        public IDbDataParameter CreateParameter()
        {
            return _command.CreateParameter();
        }

        public int ExecuteNonQuery()
        {
            _statements.Add(CommandText);
            _command.CommandText = _rewriting(CommandText);
            return _command.ExecuteNonQuery();
        }

        public IDataReader ExecuteReader()
        {
            _statements.Add(CommandText);
            _command.CommandText = _rewriting(CommandText);
            return _command.ExecuteReader();
        }

        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            _statements.Add(CommandText);
            _command.CommandText = _rewriting(CommandText);
            return _command.ExecuteReader(behavior);
        }

        public object? ExecuteScalar()
        {
            _statements.Add(CommandText);
            _command.CommandText = _rewriting(CommandText);
            return _command.ExecuteScalar();
        }

        public void Prepare()
        {
            _command.Prepare();
        }

        public void Dispose()
        {
            _command.Dispose();
        }
    }
}
