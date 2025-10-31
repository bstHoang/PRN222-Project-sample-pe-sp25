// MiddlewareTool/Services/ExcelLogger.cs
using ClosedXML.Excel;
using MiddlewareTool.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

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
                        worksheet.Cell(1, 7).Value = "Stage";  // New: Add Stage column
                        worksheet.Row(1).Style.Font.Bold = true;
                        worksheet.Columns().AdjustToContents();

                        var stagesWorksheet = workbook.Worksheets.Add("Stages");
                        stagesWorksheet.Cell(1, 1).Value = "Stage";
                        stagesWorksheet.Cell(1, 2).Value = "Timestamp";
                        stagesWorksheet.Cell(1, 3).Value = "Input";
                        stagesWorksheet.Row(1).Style.Font.Bold = true;
                        stagesWorksheet.Columns().AdjustToContents();

                        var clientStagesWorksheet = workbook.Worksheets.Add("ClientStages");
                        clientStagesWorksheet.Cell(1, 1).Value = "Stage";
                        clientStagesWorksheet.Cell(1, 2).Value = "Timestamp";
                        clientStagesWorksheet.Cell(1, 3).Value = "ClientOutput";
                        clientStagesWorksheet.Cell(1, 4).Value = "ServerOutput";
                        clientStagesWorksheet.Row(1).Style.Font.Bold = true;
                        clientStagesWorksheet.Columns().AdjustToContents();

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
                        worksheet.Cell(newRow, 7).Value = "";  // Stage will be assigned later
                        workbook.Save();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing to Excel log: {ex.Message}");
                }
            }
        }

        public void AppendStagesToExcel(List<(int Stage, string Input, string Timestamp)> stages)
        {
            if (string.IsNullOrEmpty(_excelLogPath)) return;
            lock (_excelLock)
            {
                try
                {
                    using (var workbook = new XLWorkbook(_excelLogPath))
                    {
                        var worksheet = workbook.Worksheet("Stages");
                        int startRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                        for (int i = 0; i < stages.Count; i++)
                        {
                            int row = startRow + i + 1;
                            worksheet.Cell(row, 1).Value = stages[i].Stage;
                            worksheet.Cell(row, 2).Value = stages[i].Timestamp;
                            worksheet.Cell(row, 3).Value = stages[i].Input;
                        }
                        worksheet.Columns().AdjustToContents();
                        workbook.Save();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing stages to Excel log: {ex.Message}");
                }
            }
        }

        public void AppendClientStagesToExcel(List<(int Stage, DateTime Timestamp, string ClientOutput, string ServerOutput)> stageCaptures)
        {
            if (string.IsNullOrEmpty(_excelLogPath)) return;
            lock (_excelLock)
            {
                try
                {
                    using (var workbook = new XLWorkbook(_excelLogPath))
                    {
                        var worksheet = workbook.Worksheet("ClientStages");
                        int startRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                        for (int i = 0; i < stageCaptures.Count; i++)
                        {
                            int row = startRow + i + 1;
                            worksheet.Cell(row, 1).Value = stageCaptures[i].Stage;
                            worksheet.Cell(row, 2).Value = stageCaptures[i].Timestamp.ToString("HH:mm:ss.fff");
                            worksheet.Cell(row, 3).Value = stageCaptures[i].ClientOutput;
                            worksheet.Cell(row, 4).Value = stageCaptures[i].ServerOutput;
                        }
                        worksheet.Columns().AdjustToContents();
                        workbook.Save();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing client stages to Excel log: {ex.Message}");
                }
            }
        }

        // New: Method to assign stages to logs based on timestamps
        public void AssignStagesToLogs(List<(int Stage, DateTime Timestamp)> stageTimestamps)
        {
            if (string.IsNullOrEmpty(_excelLogPath)) return;
            lock (_excelLock)
            {
                try
                {
                    using (var workbook = new XLWorkbook(_excelLogPath))
                    {
                        var worksheet = workbook.Worksheet("Logs");
                        var stages = stageTimestamps.OrderBy(s => s.Timestamp).ToList();
                        if (stages.Count == 0) return;

                        TimeSpan tolerance = TimeSpan.FromMilliseconds(500);  // Increased tolerance to 500ms to handle timing discrepancies

                        int lastRow = worksheet.LastRowUsed().RowNumber();
                        for (int row = 2; row <= lastRow; row++)
                        {
                            string tsStr = worksheet.Cell(row, 1).GetString();
                            if (DateTime.TryParseExact(tsStr, "HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime logTs))
                            {
                                // Find the largest stage where stage.Timestamp <= logTs
                                var candidate = stages.Where(s => s.Timestamp <= logTs).OrderByDescending(s => s.Timestamp).FirstOrDefault();
                                int stage = candidate.Stage;

                                // Check if there's a next stage within tolerance (to handle cases where log.ts slightly before stage.ts)
                                int currentIndex = stages.IndexOf(candidate);
                                if (currentIndex + 1 < stages.Count)
                                {
                                    var nextStage = stages[currentIndex + 1];
                                    if (nextStage.Timestamp - logTs < tolerance && nextStage.Timestamp > logTs)
                                    {
                                        stage = nextStage.Stage;
                                    }
                                }

                                worksheet.Cell(row, 7).Value = stage;
                            }
                        }
                        worksheet.Columns().AdjustToContents();
                        workbook.Save();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error assigning stages to logs: {ex.Message}");
                }
            }
        }
    }
}