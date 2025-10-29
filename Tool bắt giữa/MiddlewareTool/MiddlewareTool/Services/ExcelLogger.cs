// MiddlewareTool/Services/ExcelLogger.cs
using ClosedXML.Excel;
using System;
using System.IO;

namespace MiddlewareTool.Services
{
    public class ExcelLogger
    {
        private static readonly object _excelLock = new object();
        private string _excelLogPath = "";

        public void SetupExcelLogFile(string path)
        {
            _excelLogPath = path;
            lock (_excelLock)
            {
                if (!File.Exists(path))
                {
                    string directory = Path.GetDirectoryName(path);
                    if (directory != null && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Logs");
                        worksheet.Cell(1, 1).Value = "Timestamp";
                        worksheet.Cell(1, 2).Value = "Method/Direction";
                        worksheet.Cell(1, 3).Value = "URL/Data Preview";
                        worksheet.Cell(1, 4).Value = "Status/Bytes";
                        worksheet.Cell(1, 5).Value = "Request Body / Data";
                        worksheet.Cell(1, 6).Value = "Response Body";
                        worksheet.Row(1).Style.Font.Bold = true;
                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(path);
                    }
                }
            }
        }

        public void AppendToExcelLog(LoggedRequest logEntry)
        {
            if (string.IsNullOrEmpty(_excelLogPath)) return;
            lock (_excelLock)
            {
                try
                {
                    using (var workbook = new XLWorkbook(_excelLogPath))
                    {
                        var worksheet = workbook.Worksheet(1);
                        int newRow = worksheet.LastRowUsed().RowNumber() + 1;
                        worksheet.Cell(newRow, 1).Value = logEntry.Timestamp;
                        worksheet.Cell(newRow, 2).Value = logEntry.Method;
                        worksheet.Cell(newRow, 3).Value = logEntry.Url;
                        worksheet.Cell(newRow, 4).Value = logEntry.StatusCode;
                        worksheet.Cell(newRow, 5).Value = logEntry.RequestBody;
                        worksheet.Cell(newRow, 6).Value = logEntry.ResponseBody;
                        workbook.Save();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing to Excel log: {ex.Message}");
                }
            }
        }
    }
}