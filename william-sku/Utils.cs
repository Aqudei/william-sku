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

        public static DataTable WorksheetToDataTable(string filename, bool hasHeader, IEnumerable<Header> headers)
        {
            var colMapping = headers.ToDictionary(h => h.Display);

            var fileInfo = new FileInfo(filename);
            using var package = new ExcelPackage(fileInfo);
            using var worksheet = package.Workbook.Worksheets.First();

            var dataTable = new DataTable();

            // Get dimensions of the worksheet
            var rows = worksheet.Dimension.Rows;
            var columns = worksheet.Dimension.Columns;

            // Add columns to DataTable
            int startRow = hasHeader ? 2 : 1;
            for (int col = 1; col <= columns; col++)
            {
                var columnName = hasHeader ? worksheet.Cells[1, col].Text : $"Column{col}";
                var processedColumnName = columnName;
                if (!columnName.StartsWith("Column"))
                {
                    processedColumnName = colMapping[columnName].Name;
                }

                var dataColumn = dataTable.Columns.Add(processedColumnName);
                dataColumn.Caption = columnName;
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
