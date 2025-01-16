using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using william_sku.Models;
using static OfficeOpenXml.ExcelErrorValue;

namespace william_sku.Data
{
    internal class Database
    {
        private string _connectionString;

        public Database()
        {
            var baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WillimSKUs");

            if (!Directory.Exists(baseDirectory))
            {
                Directory.CreateDirectory(baseDirectory);
            }

            string dbPath = Path.Combine(baseDirectory, "William.db");
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
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string createTableQuery = @"
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

            connection.Close();
        }

        private void CreateInitialHeadersTable()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Headers (
                    Name TEXT PRIMARY KEY,              
                    Display TEXT,              
                    Required INTEGER NOT NULL CHECK (Required IN (0, 1)),
                    Range INTEGER NOT NULL CHECK (Range IN (0, 1))            
                );
            ";

            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }

            var headersList = new List<Header> {
                new Header
                {
                    Name = "MCNumber",
                    Display = "MC#",
                    Range = true,
                    Required = true,
                },
                new Header
                {
                    Name = "Status",
                    Display = "Status",
                    Range = false,
                    Required = false,
                },
                new Header
                {
                    Name = "EntityType",
                    Display = "Entity Type",
                    Range = false,
                    Required = false,
                },
                new Header
                {
                    Name = "OperatingStatus",
                    Display = "Operating Status",
                    Range = false,
                    Required = false,
                },
                new Header
                {
                    Name = "OutOfServiceDate",
                    Display = "Out of Service Date",
                    Range = false,
                    Required = false,
                },
                new Header
                {
                    Name = "LegalName",
                    Display = "Legal Name",
                    Range = false,
                    Required = true,
                },
                new Header
                {
                    Name = "DBAName",
                    Display = "DBA Name",
                    Range = false,
                    Required = false,
                },
                new Header
                {
                    Name = "PhysicalAddress",
                    Display = "Physical Address",
                    Range = false,
                    Required = true,
                },
                new Header
                {
                    Name = "Phone",
                    Display = "Phone",
                    Range = false,
                    Required = false,
                },
                new Header
                {
                    Name = "Email",
                    Display = "email",
                    Range = false,
                    Required = false,
                },
                new Header
                {
                    Name = "MailingAddress",
                    Display = "Mailing Address",
                    Range = false,
                    Required = false,
                },
                new Header
                {
                    Name = "USDOTNumber",
                    Display = "US DOT Number",
                    Range = true,
                    Required = true,
                },
                new Header
                {
                    Name = "PowerUnits",
                    Display = "Power Units",
                    Range = true,
                    Required = false,
                },
                new Header
                {
                    Name = "Drivers",
                    Display = "Drivers",
                    Range = true,
                    Required = false,
                },
                new Header
                {
                    Name = "AddedDate",
                    Display = "added date",
                    Range = true,
                    Required = true,
                },
                new Header
                {
                    Name = "LastUpdate",
                    Display = "last update",
                    Range = true,
                    Required = true,
                },
            };

            foreach (var header in headersList)
            {
                var commandText = "INSERT INTO Headers (Name,Display,Range,Required) VALUES (@Name,@Display,@Range,@Required)";
                var command = new SqliteCommand(commandText, connection);
                command.Parameters.AddWithValue("@Name", header.Name);
                command.Parameters.AddWithValue("@Display", header.Display);
                command.Parameters.AddWithValue("@Range", header.Range);
                command.Parameters.AddWithValue("@Required", header.Required);
                command.ExecuteNonQuery();
            }

            connection.Close();
        }

        internal void UpdateOrCreate(object mcNum, DataRow row)
        {

            var headers = ListHeaders().Where(h => h.Name != "MCNumber").ToArray();

            string insertOrReplaceQuery = (@$" 
                INSERT OR REPLACE INTO MCRecords (
                    MCNumber,{string.Join(',', headers.Select(h => h.Name))}
                ) VALUES (
                   @MCNumber,{string.Join(',', headers.Select(h => "@" + h.Name))}
                );
            ");
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = new SqliteCommand(insertOrReplaceQuery, connection);
            command.Parameters.AddWithValue($"@MCNumber", mcNum);

            foreach (var header in headers)
            {
                if (row.Table.Columns.Contains(header.Name))
                {
                    var value = row[header.Name];
                    command.Parameters.AddWithValue($"@{header.Name}", value);
                } else
                {
                    command.Parameters.AddWithValue($"@{header.Name}", DBNull.Value);
                }
            }
            var affected = command.ExecuteNonQuery();
            connection.Close();
        }

        public IEnumerable<Header> ListHeaders()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var commandText = "SELECT * FROM Headers";
            using var command = new SqliteCommand(commandText, connection);
            var reader = command.ExecuteReader();

            while (reader.Read())
            {
                yield return new Header
                {
                    Name = reader.GetString("Name"),
                    Display = reader.GetString("Display"),
                    Range = reader.GetBoolean("Range"),
                    Required = reader.GetBoolean("Required"),
                };
            }

            connection.Close();
        }

        public DataTable ListItemsAsDataTable()
        {
            var headers = ListHeaders().ToArray();
            var colMapping = headers.ToDictionary(h => h.Name);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var commandText = "SELECT * FROM MCRecords";
            using var command = new SqliteCommand(commandText, connection);
            var reader = command.ExecuteReader();

            var dataTable = new DataTable();
            dataTable.Load(reader);

            connection.Close();

            return dataTable;
        }

        internal void Delete(object mcNum)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var commandText = "DELETE FROM MCRecords WHERE MCNumber=@MCNumber";
            using var command = new SqliteCommand(commandText, connection);
            command.Parameters.AddWithValue("@MCNumber", mcNum);
            var affected = command.ExecuteNonQuery();
            connection.Close();
        }

        internal void SaveNewHeader(Header newHeader)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var insertCommandText = "INSERT INTO Headers (Name,Display,Range,Required) VALUES (@Name,@Display,@Range,@Required)";
            var insertCommand = new SqliteCommand(insertCommandText, connection, transaction);
            insertCommand.Parameters.AddWithValue("@Name", newHeader.Name);
            insertCommand.Parameters.AddWithValue("@Display", newHeader.Display);
            insertCommand.Parameters.AddWithValue("@Range", newHeader.Range);
            insertCommand.Parameters.AddWithValue("@Required", newHeader.Required);
            insertCommand.ExecuteNonQuery();

            var alterCommandText = $"ALTER TABLE MCRecords ADD COLUMN {newHeader.Name} TEXT";
            var alterCommand = new SqliteCommand(alterCommandText, connection, transaction);
            alterCommand.ExecuteNonQuery();

            transaction.Commit();
            connection.Close();
        }

        internal void DeleteHeader(Header header)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            var insertCommandText = "DELETE FROM Headers WHERE Name=@Name";
            var insertCommand = new SqliteCommand(insertCommandText, connection, transaction);
            insertCommand.Parameters.AddWithValue("@Name", header.Name);
            insertCommand.ExecuteNonQuery();

            var alterCommandText = $"ALTER TABLE MCRecords DROP COLUMN {header.Name}";
            var alterCommand = new SqliteCommand(alterCommandText, connection, transaction);
            alterCommand.ExecuteNonQuery();

            transaction.Commit();
            connection.Close();
        }

        internal DataTable ListItemsBetweenDatesAsDataTable(string header, string searchFrom, string searchTo)
        {
            var headers = ListHeaders().ToArray();
            var colMapping = headers.ToDictionary(h => h.Name);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var commandText = $"SELECT * FROM MCRecords WHERE {header} BETWEEN @SearchFrom AND @SearchTo";
            using var command = new SqliteCommand(commandText, connection);
            command.Parameters.AddWithValue("@SearchFrom", searchFrom);
            command.Parameters.AddWithValue("@SearchTo", searchTo);
            var reader = command.ExecuteReader();
            var dataTable = new DataTable();
            dataTable.Load(reader);

            connection.Close();

            return dataTable;
        }
    }
}
