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
                    Name TEXT PRIMARY KEY                
                );
            ";

            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }


            string[] headers =
            [
                "Status",
                "EntityType",
                "OperatingStatus",
                "OutOfServiceDate",
                "LegalName",
                "DBAName",
                "PhysicalAddress",
                "Phone",
                "Email",
                "MailingAddress",
                "USDOTNumber",
                "PowerUnits",
                "Drivers",
                "AddedDate",
                "LastUpdate"
            ];

            foreach (string header in headers)
            {
                var commandText = "INSERT INTO Headers (Name) VALUES (@Name)";
                var command = new SqliteCommand(commandText, connection);
                command.Parameters.AddWithValue("@Name", header);
                command.ExecuteNonQuery();
            }

            connection.Close();
        }

        internal void UpdateOrCreate(object mcNum, DataRow row)
        {

            var headers = ListHeaders();

            string insertOrReplaceQuery = (@$" 
                INSERT OR REPLACE INTO MCRecords (
                    MCNumber,{string.Join(',', headers)}
                ) VALUES (
                   @MCNumber,{string.Join(',', headers.Select(h => "@" + h))}
                );
            ");
            Debug.WriteLine(insertOrReplaceQuery);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = new SqliteCommand(insertOrReplaceQuery, connection);
            command.Parameters.AddWithValue($"@MCNumber", mcNum);

            foreach (var header in headers)
            {
                var value = row[header];
                command.Parameters.AddWithValue($"@{header}", value);
            }
            var affected = command.ExecuteNonQuery();
            Debug.WriteLine($"Affected Rows: {affected}");
            connection.Close();
        }

        public IEnumerable<string> ListHeaders()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var commandText = "SELECT Name FROM Headers";
            using var command = new SqliteCommand(commandText, connection);
            var reader = command.ExecuteReader();

            while (reader.Read())
            {
                yield return reader.GetString("Name");
            }

            connection.Close();
        }

        public DataTable ListItems()
        {
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
    }
}
