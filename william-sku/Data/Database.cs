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

        var createTableQuery = @"
                CREATE TABLE IF NOT EXISTS MCRecords (
                    MCNumber TEXT PRIMARY KEY,
                    Status TEXT,
                    EntityType TEXT,
                    OperatingStatus TEXT,
                    OutOfServiceDate TEXT,
                    LegalName TEXT,
                    DBAName TEXT,
                    PhysicalAddress TEXT,
                    Phone TEXT,
                    Email TEXT,
                    MailingAddress TEXT,
                    USDOTNumber TEXT,
                    PowerUnits TEXT,
                    Drivers TEXT,
                    AddedDate TEXT,
                    LastUpdate TEXT
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
                    Id INTEGER PRIMARY KEY AUTOINCREMENT
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
                Name = "MCNumber",
                Display = "MC#",
                Range = true,
                Required = true
            },
            new()
            {
                Name = "Status",
                Display = "Status",
                Range = false,
                Required = false
            },
            new()
            {
                Name = "EntityType",
                Display = "Entity Type",
                Range = false,
                Required = false
            },
            new()
            {
                Name = "OperatingStatus",
                Display = "Operating Status",
                Range = false,
                Required = false
            },
            new()
            {
                Name = "OutOfServiceDate",
                Display = "Out of Service Date",
                Range = false,
                Required = false
            },
            new()
            {
                Name = "LegalName",
                Display = "Legal Name",
                Range = false,
                Required = true
            },
            new()
            {
                Name = "DBAName",
                Display = "DBA Name",
                Range = false,
                Required = false
            },
            new()
            {
                Name = "PhysicalAddress",
                Display = "Physical Address",
                Range = false,
                Required = true
            },
            new()
            {
                Name = "Phone",
                Display = "Phone",
                Range = false,
                Required = false
            },
            new()
            {
                Name = "Email",
                Display = "email",
                Range = false,
                Required = false
            },
            new()
            {
                Name = "MailingAddress",
                Display = "Mailing Address",
                Range = false,
                Required = false
            },
            new()
            {
                Name = "USDOTNumber",
                Display = "US DOT Number",
                Range = true,
                Required = true
            },
            new()
            {
                Name = "PowerUnits",
                Display = "Power Units",
                Range = true,
                Required = false
            },
            new()
            {
                Name = "Drivers",
                Display = "Drivers",
                Range = true,
                Required = false
            },
            new()
            {
                Name = "AddedDate",
                Display = "added date",
                Range = true,
                Required = true
            },
            new()
            {
                Name = "LastUpdate",
                Display = "last update",
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

    public bool CheckExistItem(string mcNumber)
    {
        var findQuery = "SELECT * FROM MCRecords WHERE MCNumber=@MCNumber";

        using var connection = GetOpenConnection();

        using var command = new SqliteCommand(findQuery, connection);
        command.Parameters.AddWithValue("@MCNumber", mcNumber);
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

    public void UpdateOrCreate(string mcNum, DataRow row)
    {
        try
        {
            var ignoredColumns = new List<string> { "MCNumber", "AddedDate", "LastUpdate" };
            var headers = ListHeaders().Select(h => h.Name).Where(h => !ignoredColumns.Contains(h)).ToList();
            var workingColumns = row.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).Intersect(headers)
                .ToHashSet();

            var exist = CheckExistItem(mcNum);
            var insertOrUpdateQuery = "";
            if (exist)
            {
                workingColumns.Add("LastUpdate");
                insertOrUpdateQuery = @$" 
                UPDATE MCRecords SET 
                    {string.Join(",", workingColumns.Select(h => $"{h} = @{h}"))}
                WHERE MCNumber=@MCNumber;
            ";
            }
            else
            {
                workingColumns.Add("AddedDate");
                insertOrUpdateQuery = @$" 
                INSERT INTO MCRecords (
                    MCNumber,{string.Join(',', workingColumns)}
                ) VALUES (
                   @MCNumber,{string.Join(',', workingColumns.Select(h => "@" + h))}
                );
            ";
            }

            Debug.WriteLine(insertOrUpdateQuery);

            using var connection = GetOpenConnection();

            using var command = new SqliteCommand(insertOrUpdateQuery, connection);
            command.Parameters.AddWithValue("@MCNumber", mcNum);
            if (exist)
                command.Parameters.AddWithValue("@LastUpdate", DateTime.Now.Date.ToString("yyyy-MM-dd"));
            else
                command.Parameters.AddWithValue("@AddedDate", DateTime.Now.Date.ToString("yyyy-MM-dd"));

            foreach (var workingColumn in workingColumns)
                if (!command.Parameters.Contains($"@{workingColumn}"))
                {
                    var value = row[workingColumn];
                    if (value == null)
                        command.Parameters.AddWithValue($"@{workingColumn}", DBNull.Value);
                    else
                        command.Parameters.AddWithValue($"@{workingColumn}", value);
                }

            Debug.WriteLine("Parameters:");
            foreach (SqliteParameter item in command.Parameters)
                Debug.WriteLine($"Name:{item.ParameterName}, Value:{item.Value}");

            var affected = command.ExecuteNonQuery();
            connection?.Close();
        }
        catch (Exception e)
        {
            Logger.Error(e);
            throw;
        }
    }

    public IEnumerable<Header> ListHeaders()
    {
        using var connection = GetOpenConnection();

        var commandText = "SELECT * FROM Headers ORDER BY OrderIndex";
        using var command = new SqliteCommand(commandText, connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
            yield return new Header
            {
                Name = reader.GetString("Name"),
                Display = reader.GetString("Display"),
                Range = reader.GetBoolean("Range"),
                Required = reader.GetBoolean("Required"),
                OrderIndex = reader.GetInt32("OrderIndex")
            };

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

    internal void Delete(object mcNum)
    {
        using var connection = GetOpenConnection();

        var commandText = "DELETE FROM MCRecords WHERE MCNumber=@MCNumber";
        using var command = new SqliteCommand(commandText, connection);
        command.Parameters.AddWithValue("@MCNumber", mcNum);
        var affected = command.ExecuteNonQuery();
        connection?.Close();
    }

    internal void SaveHeader(Header header)
    {
        var exist = header.Id > 0;

        using var connection = GetOpenConnection();
        using var transaction = connection?.BeginTransaction();
        if (!exist)
        {
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
        }
        else
        {
            var dbHeader = GetHeader(header.Id);

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
            updateCommand.ExecuteNonQuery();

            var alterCommandText = $"ALTER TABLE MCRecords RENAME COLUMN {dbHeader.Name} TO {header.Name}";
            var alterCommand = new SqliteCommand(alterCommandText, connection, transaction);
            alterCommand.ExecuteNonQuery();
        }

        transaction?.Commit();
        connection?.Close();
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

    public void UpdateOnly(string? mcNum, DataRow row)
    {
        try
        {
            var ignoredColumns = new List<string> { "MCNumber", "AddedDate", "LastUpdate" };
            var headers = ListHeaders().Select(h => h.Name).Where(h => !ignoredColumns.Contains(h)).ToArray();
            var workingColumns = row.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).Intersect(headers)
                .ToHashSet();

            var exist = CheckExistItem(mcNum);
            var updateQuery = "";
            if (!exist)
                return;

            workingColumns.Add("LastUpdate");
            updateQuery = $"""
                            
                                           UPDATE MCRecords SET 
                                               {string.Join(",", workingColumns.Select(h => $"{h} = @{h}"))}
                                           WHERE MCNumber=@MCNumber;
                                       
                           """;

            Debug.WriteLine(updateQuery);

            using var connection = GetOpenConnection();
            using var command = new SqliteCommand(updateQuery, connection);

            command.Parameters.AddWithValue("@MCNumber", mcNum);
            command.Parameters.AddWithValue("@LastUpdate", DateTime.Now.Date.ToString("yyyy-MM-dd"));

            foreach (var workingColumn in workingColumns)
                if (!command.Parameters.Contains($"@{workingColumn}"))
                {
                    var value = row.Field<object>(workingColumn);
                    command.Parameters.AddWithValue($"@{workingColumn}", value ?? DBNull.Value);
                }

            Debug.WriteLine("Parameters:");
            foreach (SqliteParameter item in command.Parameters)
                Debug.WriteLine($"Name:{item.ParameterName}, Value:{item.Value}");

            var affected = command.ExecuteNonQuery();
            connection?.Close();
        }
        catch (Exception e)
        {
            Logger.Error(e);
            throw;
        }
    }
}