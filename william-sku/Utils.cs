using CsvHelper;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using william_sku.Data;
using william_sku.Models;

namespace william_sku
{
    internal class Utils
    {
        public static void ExportToExcel(DataTable dataTable, string filePath, Dictionary<string, Header> headers)
        {
            // Enable Excel Package License (EPPlus requires this starting from version 5.x)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (ExcelPackage package = new ExcelPackage())
            {
                // Add a worksheet to the package
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("MCRecords");

                // Load the DataTable into the worksheet
                worksheet.Cells["A1"].LoadFromDataTable(dataTable, true);
                var columns = worksheet.Dimension.Columns;
                for (int col = 1; col <= columns; col++)
                {
                    var name = worksheet.Cells[1, col].GetValue<string>();

                    if (headers.TryGetValue(name, out var header))
                    {
                        worksheet.Cells[1, col].Value = header.Display;
                    }
                    else
                    {
                        worksheet.Cells[1, col].Value = "Unknown Column";
                    }
                }

                // Save the package to the specified file path
                FileInfo file = new FileInfo(filePath);
                package.SaveAs(file);
            }
        }
        public static string GetAppData()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataPath = Path.Combine(appData, "WilliamSKUs");
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }
            return dataPath;
        }

        public static string GetDbPath()
        {
            var dbPath = Path.Combine(GetAppData(), "William.db");
            return dbPath;
        }

        private static DataTable CsvToDataTable(string filePath, IEnumerable<Header> headers)
        {
            var colMapping = headers.ToDictionary(h => h.Display);

            // Create a DataTable
            var dataTable = new DataTable();

            // Populate the DataTable from CSV
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                // Read the header row to define columns
                csv.Read();
                csv.ReadHeader();

                var dateColumns = new List<int>();

                for (int i = 0; i < csv.HeaderRecord.Length; i++)
                {
                    var columnName = csv.HeaderRecord[i];
                    if (!colMapping.ContainsKey(columnName))
                        continue;

                    var headerColumn = colMapping[columnName];
                    var databaseColumnName = headerColumn.Name;

                    var dataColumn = dataTable.Columns.Add(databaseColumnName);
                    dataColumn.Caption = headerColumn.Display;


                }

                // Read the data rows and populate the DataTable

                while (csv.Read())
                {
                    DataRow row = dataTable.NewRow();
                    foreach (var header in csv.HeaderRecord)
                    {
                        if (!colMapping.ContainsKey(header))
                            continue;

                        var headerColumn = colMapping[header];
                        var value = csv.GetField(header);

                        if (headerColumn.Name == "AddedDate" || headerColumn.Name == "LastUpdate")
                        {
                            if (DateOnly.TryParse(value, out var dateValue))
                                value = dateValue.ToString("yyyy-MM-dd");
                        }

                        row[headerColumn.Name] = value;
                    }

                    dataTable.Rows.Add(row);
                }
            }

            return dataTable;
        }

        public static DataTable WorksheetToDataTable(string filename, IEnumerable<Header> headers)
        {
            if (Path.GetExtension(filename).ToUpper().EndsWith(".CSV"))
                return CsvToDataTable(filename, headers);


            var colMapping = headers.ToDictionary(h => h.Display);

            var fileInfo = new FileInfo(filename);
            using var package = new ExcelPackage(fileInfo);
            using var worksheet = package.Workbook.Worksheets.First();

            var dataTable = new DataTable("MCRecords");

            // Get dimensions of the worksheet
            var rows = worksheet.Dimension.Rows;
            var columns = worksheet.Dimension.Columns;

            // Add columns to DataTable
            int startRow = 2;
            var skippedColumns = new List<int>();
            for (int col = 1; col <= columns; col++)
            {
                var columnName = worksheet.Cells[1, col].Text;
                if (colMapping.TryGetValue(columnName, out var headerObj))
                {
                    var dbColumnName = headerObj.Name;
                    var dataColumn = dataTable.Columns.Add(dbColumnName);
                    dataColumn.Caption = headerObj?.Display;
                }
                else
                {
                    skippedColumns.Add(col);
                }
            }

            // Add rows to DataTable
            for (int row = startRow; row <= rows; row++)
            {
                var dataRow = dataTable.NewRow();
                var currentCol = 1;

                for (int col = 1; col <= columns; col++)
                {
                    if (skippedColumns.Contains(col))
                        continue;

                    var value = worksheet.Cells[row, col].Text;
                    dataRow[currentCol - 1] = value;
                    currentCol++;
                }

                dataTable.Rows.Add(dataRow);
            }

            return dataTable;
        }
    }
}
