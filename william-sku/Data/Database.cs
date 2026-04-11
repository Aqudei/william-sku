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


        PreRun();
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
    public void SyncSchema(bool allowDestructiveChanges = false)
    {
        var headers = ListHeaders()
            .Select(h => h.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        using var connection = GetOpenConnection();

        // System columns (always keep)
        var systemColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            //PRIMARY_KEY,
            //TIMESTAMP_ADDED,
            //TIMESTAMP_UPDATED,
            "Id"
        };

        // 1. Get current DB columns
        var dbColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var cmd = new SqliteCommand("PRAGMA table_info(MCRecords);", connection))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                dbColumns.Add(reader["name"].ToString());
            }
        }

        // 2. ADD missing columns
        var columnsToAdd = headers
            .Except(dbColumns)
            .Except(systemColumns)
            .ToList();

        foreach (var col in columnsToAdd)
        {
            var alter = $"ALTER TABLE MCRecords ADD COLUMN \"{col}\" TEXT;";
            using var cmd = new SqliteCommand(alter, connection);
            cmd.ExecuteNonQuery();
        }


        if (!allowDestructiveChanges)
            return;

        // Refresh dbColumns after adding
        foreach (var col in columnsToAdd)
            dbColumns.Add(col);

        // 3. REMOVE extra columns (excluding system)
        var columnsToRemove = dbColumns
            .Except(headers)
            .Except(systemColumns)
            .ToList();

        if (!columnsToRemove.Any())
            return; // nothing to rebuild

        // Final columns to keep
        var finalColumns = dbColumns
            .Except(columnsToRemove)
            .ToList();

        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Rename old table
            new SqliteCommand(
                "ALTER TABLE MCRecords RENAME TO MCRecords_old;",
                connection,
                transaction
            ).ExecuteNonQuery();

            // 2. Recreate table
            var columnDefs = new List<string>();

            foreach (var col in finalColumns)
            {
                if (col.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    columnDefs.Add("Id INTEGER PRIMARY KEY AUTOINCREMENT");
                else
                    columnDefs.Add($"\"{col}\" TEXT");
            }

            var createSql = $"CREATE TABLE MCRecords ({string.Join(",", columnDefs)});";

            new SqliteCommand(createSql, connection, transaction)
                .ExecuteNonQuery();

            // 3. Copy data
            var columnList = string.Join(",", finalColumns.Select(c => $"\"{c}\""));

            var copySql = $@"
            INSERT INTO MCRecords ({columnList})
            SELECT {columnList} FROM MCRecords_old;
        ";

            new SqliteCommand(copySql, connection, transaction)
                .ExecuteNonQuery();

            // 4. Drop old table
            new SqliteCommand("DROP TABLE MCRecords_old;", connection, transaction)
                .ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void PreRun()
    {
        using var connection = GetOpenConnection();
        var cmd = "CREATE UNIQUE INDEX IF NOT EXISTS idx_headers_name ON Headers(Name)";
        using var createOndexCommand = new SqliteCommand(cmd, connection);
        createOndexCommand.ExecuteNonQuery();


        var commandText = @"
            INSERT INTO Headers (Name, Display, Range, Required)
            VALUES (@Name, @Display, @Range, @Required)
            ON CONFLICT(Name) DO UPDATE SET
                Display = excluded.Display,
                Range = excluded.Range,
                Required = excluded.Required;
        ";

        using var createRequiredHeadersCommand = new SqliteCommand(commandText, connection);
        createRequiredHeadersCommand.Parameters.AddWithValue("@Name", "UPDATED");
        createRequiredHeadersCommand.Parameters.AddWithValue("@Display", "UPDATED");
        createRequiredHeadersCommand.Parameters.AddWithValue("@Range", true);
        createRequiredHeadersCommand.Parameters.AddWithValue("@Required", true);
        createRequiredHeadersCommand.ExecuteNonQuery();
    }

    private void CreateInitialHeadersTable()
    {
        using var connection = GetOpenConnection();

        var createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Headers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT UNIQUE,              
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
        using var connection = GetOpenConnection();

        // 1. Get actual columns (case-insensitive)
        var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var pragmaCmd = new SqliteCommand("PRAGMA table_info(MCRecords);", connection))
        using (var reader = pragmaCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                columnNames.Add(reader["name"].ToString());
            }
        }

        // System columns (never update dynamically)
        var systemColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PRIMARY_KEY,
            TIMESTAMP_ADDED,
            TIMESTAMP_UPDATED,
            "Id"
        };

        // 2. Filter only valid + non-system columns
        var validColumns = workingColumns
            .Where(c => columnNames.Contains(c) && !systemColumns.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 3. Check existence USING SAME CONNECTION
        bool exist;
        using (var checkCmd = new SqliteCommand(
            $"SELECT 1 FROM MCRecords WHERE \"{PRIMARY_KEY}\" = @{PRIMARY_KEY} LIMIT 1;",
            connection))
        {
            checkCmd.Parameters.AddWithValue($"@{PRIMARY_KEY}", pkValue);
            exist = checkCmd.ExecuteScalar() != null;
        }

        string query;

        if (exist)
        {
            var setParts = new List<string>
        {
            $"\"{TIMESTAMP_UPDATED}\" = @{TIMESTAMP_UPDATED}"
        };

            setParts.AddRange(validColumns.Select(c => $"\"{c}\" = @{c}"));

            query = $@"
                UPDATE MCRecords SET 
                    {string.Join(",", setParts)}
                WHERE ""{PRIMARY_KEY}"" = @{PRIMARY_KEY};
            ";
        }
        else
        {
            var insertColumns = new List<string> { PRIMARY_KEY, TIMESTAMP_ADDED, TIMESTAMP_UPDATED };
            insertColumns.AddRange(validColumns);

            var columnSql = string.Join(",", insertColumns.Select(c => $"\"{c}\""));
            var valueSql = string.Join(",", insertColumns.Select(c => $"@{c}"));

            query = $@"
            INSERT INTO MCRecords ({columnSql})
            VALUES ({valueSql});
        ";
        }

        using var command = new SqliteCommand(query, connection);

        // PK
        command.Parameters.AddWithValue($"@{PRIMARY_KEY}", pkValue);

        if (exist)
            command.Parameters.AddWithValue($"@{TIMESTAMP_UPDATED}", DateTime.Now.ToString("yyyy-MM-dd"));
        else
            command.Parameters.AddWithValue($"@{TIMESTAMP_ADDED}", DateTime.Now.ToString("yyyy-MM-dd"));

        // 4. Safe parameter binding
        foreach (var col in validColumns)
        {
            object value = DBNull.Value;

            if (row.Table.Columns.Contains(col) && row[col] != null)
                value = row[col];

            command.Parameters.AddWithValue($"@{col}", value ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
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

        // 1. Get actual columns from MCRecords
        var columnNames = new HashSet<string>();

        using (var pragmaCmd = new SqliteCommand("PRAGMA table_info(MCRecords);", connection))
        using (var reader = pragmaCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                columnNames.Add(reader["name"].ToString());
            }
        }

        // 2. Filter headers to only valid columns
        var validHeaders = headers
            .Where(h => columnNames.Contains(h.Name))
            .ToArray();

        // Optional: handle case where nothing is valid
        if (!validHeaders.Any())
            throw new Exception("No valid columns found in MCRecords.");


        var missing = headers
            .Where(h => !columnNames.Contains(h.Name))
            .Select(h => h.Name)
            .ToList();


        var commandText = $"SELECT {string.Join(',', validHeaders.Select(h => h.Name))} FROM MCRecords";

        using var command = new SqliteCommand(commandText, connection);
        using var dataReader = command.ExecuteReader();

        var dataTable = new DataTable();
        dataTable.Load(dataReader);

        return dataTable;
    }


    public void Delete(object[] pkValues)
    {
        using var connection = GetOpenConnection();

        // Dynamically create placeholders for each primary key value
        var placeholders = string.Join(", ", pkValues.Select((_, index) => $"@pk{index}"));
        var commandText = $"DELETE FROM MCRecords WHERE {PRIMARY_KEY} IN ({placeholders})";

        using var command = new SqliteCommand(commandText, connection);

        // Add parameters for each primary key value
        for (int i = 0; i < pkValues.Length; i++)
        {
            command.Parameters.AddWithValue($"@pk{i}", pkValues[i]);
        }

        var affected = command.ExecuteNonQuery();
        connection?.Close();
    }

    public void Delete(object pkValue)
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
        if (string.IsNullOrWhiteSpace(pkValue))
            return;

        using var connection = GetOpenConnection();

        // 1. Get actual columns from MCRecords
        var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var pragmaCmd = new SqliteCommand("PRAGMA table_info(MCRecords);", connection))
        using (var reader = pragmaCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                columnNames.Add(reader["name"].ToString());
            }
        }

        // 2. System columns we should not update dynamically
        var systemColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PRIMARY_KEY,
            TIMESTAMP_ADDED,
            TIMESTAMP_UPDATED,
            "Id"
        };

        // 3. Filter only valid + non-system columns
        var validColumns = workingColumns
            .Where(c => columnNames.Contains(c) && !systemColumns.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!validColumns.Any())
            return; // nothing to update

        // 4. Check existence using the same connection
        bool exist;
        using (var checkCmd = new SqliteCommand(
            $"SELECT 1 FROM MCRecords WHERE \"{PRIMARY_KEY}\" = @{PRIMARY_KEY} LIMIT 1;",
            connection))
        {
            checkCmd.Parameters.AddWithValue($"@{PRIMARY_KEY}", pkValue);
            exist = checkCmd.ExecuteScalar() != null;
        }

        if (!exist)
            return;

        // 5. Build safe update query
        var setParts = new List<string>
        {
            $"\"{TIMESTAMP_UPDATED}\" = @{TIMESTAMP_UPDATED}"
        };

        setParts.AddRange(validColumns.Select(c => $"\"{c}\" = @{c}"));

        var updateQuery = $@"
            UPDATE MCRecords SET 
                {string.Join(",", setParts)}
            WHERE ""{PRIMARY_KEY}"" = @{PRIMARY_KEY};
        ";

        Debug.WriteLine(updateQuery);

        using var command = new SqliteCommand(updateQuery, connection);

        // 6. Add parameters
        command.Parameters.AddWithValue($"@{PRIMARY_KEY}", pkValue);
        command.Parameters.AddWithValue($"@{TIMESTAMP_UPDATED}", DateTime.Now.ToString("yyyy-MM-dd"));

        foreach (var col in validColumns)
        {
            object value = DBNull.Value;

            if (row.Table.Columns.Contains(col) && row[col] != null)
                value = row[col];

            command.Parameters.AddWithValue($"@{col}", value ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
    }
}