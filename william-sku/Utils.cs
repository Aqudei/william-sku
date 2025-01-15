using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(dataTable.TableName);

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

        public static DataTable WorksheetToDataTable(string filename, IEnumerable<Header> headers)
        {
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
            for (int col = 1; col <= columns; col++)
            {
                var columnName = worksheet.Cells[1, col].Text;

                if (!colMapping.ContainsKey(columnName))
                    continue;

                var headerColumn = colMapping[columnName];
                var databaseColumnName = headerColumn.Name;

                var dataColumn = dataTable.Columns.Add(databaseColumnName);
                dataColumn.Caption = headerColumn.Display;
            }

            // Add rows to DataTable
            for (int row = startRow; row <= rows; row++)
            {
                var dataRow = dataTable.NewRow();
                for (int col = 1; col <= columns; col++)
                {
                    dataRow[col - 1] = worksheet.Cells[row, col].Text;
                }
                dataTable.Rows.Add(dataRow);
            }


            return dataTable;
        }
    }
}
