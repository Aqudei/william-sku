using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using Microsoft.Data.Sqlite;
using NLog;
using william_sku.Models;

namespace william_sku.Data;

public class Database
{
    public const string PRIMARY_KEY = "DOT";
    public const string TIMESTAMP_ADDED = "ADDED";
    public const string TIMESTAMP_UPDATED = "UPDATED";


    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public Database()
    {
        var baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WillimSKUs");

        if (!Directory.Exists(baseDirectory)) Directory.CreateDirectory(baseDirectory);

        var dbPath = Path.Combine(baseDirectory, "William.db");
        _connectionString = $"Data Source={dbPath};";

        if (!File.Exists(dbPath))
            CreateTables();
    }


    private void CreateTables()
    {
        CreateInitialTable();
        CreateInitialHeadersTable();
    }

    private void CreateInitialTable()
    {
        using var connection = GetOpenConnection();

        var createTableQuery = $@"
                CREATE TABLE IF NOT EXISTS MCRecords (
                    {PRIMARY_KEY} TEXT PRIMARY KEY,
                    {TIMESTAMP_ADDED} TEXT,
                    {TIMESTAMP_UPDATED} TEXT
                );
            ";

        using (var command = new SqliteCommand(createTableQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        connection?.Close();
    }

    private void CreateInitialHeadersTable()
    {
        using var connection = GetOpenConnection();

        var createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Headers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT,              
                    Display TEXT,              
                    Required INTEGER NOT NULL CHECK (Required IN (0, 1)),
                    Range INTEGER NOT NULL CHECK (Range IN (0, 1)),
                    OrderIndex INTEGER DEFAULT 0
                );
            ";

        using (var command = new SqliteCommand(createTableQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        var headersList = new List<Header>
        {
            new()
            {
                Name = PRIMARY_KEY,
                Display = PRIMARY_KEY,
                Range = true,
                Required = true
            },
            new()
            {
                Name = TIMESTAMP_ADDED,
                Display = TIMESTAMP_ADDED,
                Range = true,
                Required = true
            },
            new()
            {
                Name = TIMESTAMP_UPDATED,
                Display = TIMESTAMP_UPDATED,
                Range = true,
                Required = true
            }
        };

        foreach (var header in headersList)
        {
            var commandText =
                "INSERT INTO Headers (Name,Display,Range,Required) VALUES (@Name,@Display,@Range,@Required)";
            var command = new SqliteCommand(commandText, connection);
            command.Parameters.AddWithValue("@Name", header.Name);
            command.Parameters.AddWithValue("@Display", header.Display);
            command.Parameters.AddWithValue("@Range", header.Range);
            command.Parameters.AddWithValue("@Required", header.Required);
            command.ExecuteNonQuery();
        }

        connection?.Close();
    }

    public bool CheckRecordExist(string dotNumber)
    {
        var findQuery = $"SELECT * FROM MCRecords WHERE {PRIMARY_KEY}=@{PRIMARY_KEY}";

        using var connection = GetOpenConnection();

        using var command = new SqliteCommand(findQuery, connection);
        command.Parameters.AddWithValue($"@{PRIMARY_KEY}", dotNumber);
        using var reader = command.ExecuteReader();

        var exist = reader.HasRows;

        connection?.Close();
        return exist;
    }

    public Header? GetHeader(int headerId)
    {
        var findQuery = "SELECT * FROM Headers WHERE Id=@Id";

        using var connection = GetOpenConnection();

        using var command = new SqliteCommand(findQuery, connection);
        command.Parameters.AddWithValue("@Id", headerId);
        using var reader = command.ExecuteReader();

        Header? ret = null;

        while (reader.Read())
        {
            ret = new Header
            {
                Name = reader.GetFieldValue<string>("Name"),
                Display = reader.GetFieldValue<string>("Display"),
                Id = reader.GetFieldValue<int>("Id"),
                Required = reader.GetFieldValue<bool>("Required"),
                OrderIndex = reader.GetFieldValue<int>("OrderIndex"),
                Range = reader.GetFieldValue<bool>("Range")
            };
            break;
        }

        connection?.Close();
        return ret;
    }

    public void UpdateOrCreate(string pkValue, DataRow row, IEnumerable<string> workingColumns)
    {
        var exist = CheckRecordExist(pkValue);
        var insertOrUpdateQuery = "";
        if (exist)
        {
            insertOrUpdateQuery = @$" 
                UPDATE MCRecords SET 
                    {TIMESTAMP_UPDATED}=@{TIMESTAMP_UPDATED},{string.Join(",", workingColumns.Select(h => $"{h} = @{h}"))}
                WHERE {PRIMARY_KEY}=@{PRIMARY_KEY};
            ";
        }
        else
        {
            insertOrUpdateQuery = @$" 
                INSERT INTO MCRecords (
                    {PRIMARY_KEY},{TIMESTAMP_ADDED},{string.Join(',', workingColumns)}
                ) VALUES (
                   @{PRIMARY_KEY},@{TIMESTAMP_ADDED},{string.Join(',', workingColumns.Select(h => "@" + h))}
                );
            ";
        }

        using var connection = GetOpenConnection();

        using var command = new SqliteCommand(insertOrUpdateQuery, connection);
        command.Parameters.AddWithValue($"@{PRIMARY_KEY}", pkValue);
        if (exist)
            command.Parameters.AddWithValue($"@{TIMESTAMP_UPDATED}", DateTime.Now.Date.ToString("yyyy-MM-dd"));
        else
            command.Parameters.AddWithValue($"@{TIMESTAMP_ADDED}", DateTime.Now.Date.ToString("yyyy-MM-dd"));

        foreach (var workingColumn in workingColumns)
            if (!command.Parameters.Contains($"@{workingColumn}"))
            {
                var value = row[workingColumn];
                if (value == null)
                    command.Parameters.AddWithValue($"@{workingColumn}", DBNull.Value);
                else
                    command.Parameters.AddWithValue($"@{workingColumn}", value);
            }

        var affected = command.ExecuteNonQuery();
        connection?.Close();
    }

    public IEnumerable<Header> ListHeaders()
    {
        using var connection = GetOpenConnection();

        var commandText = "SELECT * FROM Headers ORDER BY OrderIndex";
        using var command = new SqliteCommand(commandText, connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var header = new Header
            {
                Name = reader.GetString("Name"),
                Display = reader.GetString("Display"),
                Range = reader.GetBoolean("Range"),
                Required = reader.GetBoolean("Required"),
                OrderIndex = reader.GetInt32("OrderIndex"),
                Id = reader.GetInt32("Id"),
            };
            yield return header;
        }

        reader.Close();
        connection?.Close();
    }

    public DataTable ListItemsAsDataTable()
    {
        var headers = ListHeaders().OrderBy(i => i.OrderIndex).ToArray();

        using var connection = GetOpenConnection();

        var commandText = $"SELECT {string.Join(',', headers.Select(h => h.Name))} FROM MCRecords";
        using var command = new SqliteCommand(commandText, connection);
        var reader = command.ExecuteReader();

        var dataTable = new DataTable();
        dataTable.Load(reader);

        connection?.Close();

        return dataTable;
    }

    internal void Delete(object pkValue)
    {
        using var connection = GetOpenConnection();

        var commandText = $"DELETE FROM MCRecords WHERE {PRIMARY_KEY}=@{PRIMARY_KEY}";
        using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue($"@{PRIMARY_KEY}", pkValue);
        var affected = command.ExecuteNonQuery();
        connection?.Close();
    }

    internal void SaveHeader(Header header)
    {
        var exist = header.Id > 0;

        if (!exist)
        {
            using var connection = GetOpenConnection();
            using var transaction = connection?.BeginTransaction();
            var insertCommandText =
                "INSERT INTO Headers (Name,Display,Range,Required,OrderIndex) VALUES (@Name,@Display,@Range,@Required,@OrderIndex)";
            var insertCommand = new SqliteCommand(insertCommandText, connection, transaction);
            insertCommand.Parameters.AddWithValue("@Name", header.Name);
            insertCommand.Parameters.AddWithValue("@Display", header.Display);
            insertCommand.Parameters.AddWithValue("@Range", header.Range);
            insertCommand.Parameters.AddWithValue("@Required", header.Required);
            insertCommand.Parameters.AddWithValue("@OrderIndex", 1000);

            insertCommand.ExecuteNonQuery();

            var alterCommandText = $"ALTER TABLE MCRecords ADD COLUMN {header.Name} TEXT";
            var alterCommand = new SqliteCommand(alterCommandText, connection, transaction);
            alterCommand.ExecuteNonQuery();
            transaction?.Commit();
            connection?.Close();
        }
        else
        {
            var dbHeader = GetHeader(header.Id);

            using var connection = GetOpenConnection();
            using var transaction = connection?.BeginTransaction();
            var updateCommandText = """
                                    UPDATE Headers 
                                    SET Name=@Name,Display=@Display,Range=@Range,Required=@Required
                                    WHERE Id=@Id
                                    """;
            var updateCommand = new SqliteCommand(updateCommandText, connection, transaction);
            updateCommand.Parameters.AddWithValue("@Name", header.Name);
            updateCommand.Parameters.AddWithValue("@Display", header.Display);
            updateCommand.Parameters.AddWithValue("@Range", header.Range);
            updateCommand.Parameters.AddWithValue("@Required", header.Required);
            updateCommand.Parameters.AddWithValue("@Id", header.Id);
            updateCommand.ExecuteNonQuery();

            var alterCommandText = $"ALTER TABLE MCRecords RENAME COLUMN {dbHeader.Name} TO {header.Name}";
            var alterCommand = new SqliteCommand(alterCommandText, connection, transaction);
            alterCommand.ExecuteNonQuery();
            transaction?.Commit();
            connection?.Close();
        }
    }


    private SqliteConnection? GetOpenConnection()
    {
        if (_connection is { State: ConnectionState.Open })
            return _connection;

        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        return _connection;
    }

    internal void DeleteHeader(Header header)
    {
        using var connection = GetOpenConnection();

        using var transaction = connection?.BeginTransaction();

        var insertCommandText = "DELETE FROM Headers WHERE Name=@Name";
        var insertCommand = new SqliteCommand(insertCommandText, connection, transaction);
        insertCommand.Parameters.AddWithValue("@Name", header.Name);
        insertCommand.ExecuteNonQuery();

        var alterCommandText = $"ALTER TABLE MCRecords DROP COLUMN {header.Name}";
        var alterCommand = new SqliteCommand(alterCommandText, connection, transaction);
        alterCommand.ExecuteNonQuery();

        transaction?.Commit();
        connection?.Close();
    }

    internal DataTable ListItemsBetweenDatesAsDataTable(string header, string searchFrom, string searchTo)
    {
        var headers = ListHeaders().OrderBy(h => h.OrderIndex).ToArray();

        using var connection = GetOpenConnection();

        var commandText = $"SELECT {string.Join(',', headers.Select(h => h.Name))} FROM MCRecords WHERE {header} BETWEEN @SearchFrom AND @SearchTo";
        using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@SearchFrom", searchFrom);
        command.Parameters.AddWithValue("@SearchTo", searchTo);
        var reader = command.ExecuteReader();
        var dataTable = new DataTable();
        dataTable.Load(reader);

        connection?.Close();

        return dataTable;
    }

    public void SaveColumnOrdering(string[] orderedHeaders)
    {
        using var connection = GetOpenConnection();

        for (var i = 0; i < orderedHeaders.Length; i++)
        {
            var header = orderedHeaders[i];

            var commandText = "UPDATE Headers SET OrderIndex=@OrderIndex WHERE Name=@Name";
            var command = new SqliteCommand(commandText, connection);
            command.Parameters.AddWithValue("@OrderIndex", i);
            command.Parameters.AddWithValue("@Name", header);
            var affected = command.ExecuteNonQuery();
        }

        connection?.Close();
    }

    public void UpdateOnly(string? pkValue, DataRow row, IEnumerable<string> workingColumns)
    {
        var exist = CheckRecordExist(pkValue);
        var updateQuery = "";
        if (!exist)
            return;

        updateQuery = $"""
                                UPDATE MCRecords SET 
                                    {TIMESTAMP_UPDATED}=@{TIMESTAMP_UPDATED},{string.Join(",", workingColumns.Select(h => $"{h} = @{h}"))}
                                WHERE {PRIMARY_KEY}=@{PRIMARY_KEY};         
                           """;

        Debug.WriteLine(updateQuery);

        using var connection = GetOpenConnection();
        using var command = new SqliteCommand(updateQuery, connection);

        command.Parameters.AddWithValue($"@{PRIMARY_KEY}", pkValue);
        command.Parameters.AddWithValue($"@{TIMESTAMP_UPDATED}", DateTime.Now.Date.ToString("yyyy-MM-dd"));

        foreach (var workingColumn in workingColumns)
            if (!command.Parameters.Contains($"@{workingColumn}"))
            {
                var value = row.Field<object>(workingColumn);
                command.Parameters.AddWithValue($"@{workingColumn}", value ?? DBNull.Value);
            }

        var affected = command.ExecuteNonQuery();
        connection?.Close();
    }
}