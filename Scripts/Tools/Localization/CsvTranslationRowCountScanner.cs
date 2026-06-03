using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

[Tool]
[GlobalClass]
public partial class CsvTranslationRowCountScanner : Resource
{
    [Export]
    public string OutputReportPath { get; set; } = "res://Temp/CsvTranslationRowCountReport.txt";

    [Export]
    public Array<string> TranslationCsvPaths { get; set; } = new();

    [Export]
    public bool IgnoreEmptyLines { get; set; } = true;

    [Export]
    public bool IgnoreCommentLines { get; set; } = true;

    [Export]
    public string CommentPrefix { get; set; } = "#";

    [ExportToolButton("校验配置")]
    public Callable ValidateConfigButton => Callable.From(ValidateConfig);

    [ExportToolButton("预览缺失检查")]
    public Callable PreviewCheckButton => Callable.From(PreviewMismatchedRows);

    [ExportToolButton("生成缺失报告")]
    public Callable GenerateReportButton => Callable.From(GenerateMismatchReport);

    public void ValidateConfig()
    {
        if (!TryValidateConfig(out string message))
        {
            GD.PrintErr($"[CsvTranslationRowCountScanner] 配置校验失败：{message}");
            return;
        }

        GD.Print("[CsvTranslationRowCountScanner] 配置校验通过。");
    }

    public void PreviewMismatchedRows()
    {
        if (!TryValidateConfig(out string message))
        {
            GD.PrintErr($"[CsvTranslationRowCountScanner] 配置校验失败：{message}");
            return;
        }

        ScanResult result = Scan();
        PrintMismatches(result);

        GD.Print(
            $"[CsvTranslationRowCountScanner] 预览完成：检查 CSV {result.ScannedCsvCount} 个，" +
            $"存在问题的 CSV {result.FilesWithMismatchCount} 个，" +
            $"异常行总数 {result.TotalMismatchRowCount} 个。");
    }

    public void GenerateMismatchReport()
    {
        if (!TryValidateConfig(out string message))
        {
            GD.PrintErr($"[CsvTranslationRowCountScanner] 配置校验失败：{message}");
            return;
        }

        ScanResult result = Scan();
        PrintMismatches(result);

        string report = BuildReport(result);
        if (!TryWriteReport(report, out string writeMessage))
        {
            GD.PrintErr($"[CsvTranslationRowCountScanner] 报告写入失败：{writeMessage}");
            return;
        }

        GD.Print(
            $"[CsvTranslationRowCountScanner] 报告已生成：{OutputReportPath}，" +
            $"存在问题的 CSV {result.FilesWithMismatchCount} 个，" +
            $"异常行总数 {result.TotalMismatchRowCount} 个。");
    }

    private bool TryValidateConfig(out string message)
    {
        if (string.IsNullOrWhiteSpace(OutputReportPath))
        {
            message = "输出路径不能为空。";
            return false;
        }

        string normalizedOutput = NormalizeResPath(OutputReportPath);
        if (!normalizedOutput.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            message = "输出路径必须位于项目内，并以 res:// 开头。";
            return false;
        }

        if (!normalizedOutput.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            message = "输出文件建议且当前要求为 .txt。";
            return false;
        }

        if (!IsPathInsideProject(normalizedOutput))
        {
            message = "输出路径不在项目目录内。";
            return false;
        }

        if (TranslationCsvPaths == null || TranslationCsvPaths.Count == 0)
        {
            message = "至少需要配置一个 CSV 路径。";
            return false;
        }

        for (int i = 0; i < TranslationCsvPaths.Count; i++)
        {
            string csvPath = NormalizeResPath(TranslationCsvPaths[i]);
            if (string.IsNullOrWhiteSpace(csvPath))
            {
                message = $"第 {i + 1} 个 CSV 路径为空。";
                return false;
            }

            if (!csvPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                message = $"CSV 路径必须以 res:// 开头：{csvPath}";
                return false;
            }

            if (!csvPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                message = $"CSV 路径不是 .csv：{csvPath}";
                return false;
            }

            if (!Godot.FileAccess.FileExists(csvPath))
            {
                message = $"CSV 文件不存在：{csvPath}";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private ScanResult Scan()
    {
        SortedDictionary<string, List<MismatchRecord>> mismatchesByFile =
            new(StringComparer.OrdinalIgnoreCase);

        int totalMismatchRowCount = 0;

        for (int i = 0; i < TranslationCsvPaths.Count; i++)
        {
            string csvPath = NormalizeResPath(TranslationCsvPaths[i]);
            List<MismatchRecord> mismatches = ScanCsv(csvPath);
            if (mismatches.Count == 0)
            {
                continue;
            }

            mismatchesByFile[csvPath] = mismatches;
            totalMismatchRowCount += mismatches.Count;
        }

        return new ScanResult
        {
            ScannedCsvCount = TranslationCsvPaths.Count,
            FilesWithMismatchCount = mismatchesByFile.Count,
            TotalMismatchRowCount = totalMismatchRowCount,
            MismatchesByFile = mismatchesByFile
        };
    }

    private List<MismatchRecord> ScanCsv(string csvPath)
    {
        List<MismatchRecord> mismatches = new();

        using Godot.FileAccess file = Godot.FileAccess.Open(csvPath, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[CsvTranslationRowCountScanner] 无法打开 CSV：{csvPath}");
            return mismatches;
        }

        int expectedCellCount = -1;
        int lineNumber = 0;
        bool headerHandled = false;

        while (!file.EofReached())
        {
            string line = file.GetLine();
            lineNumber++;

            if (ShouldSkipLine(line))
            {
                continue;
            }

            List<string> cells = ParseCsvCells(line);
            if (!headerHandled)
            {
                if (cells.Count > 0 && cells[0].Length > 0)
                {
                    cells[0] = cells[0].TrimStart('\uFEFF');
                }

                expectedCellCount = cells.Count;
                headerHandled = true;
                continue;
            }

            if (cells.Count == expectedCellCount)
            {
                continue;
            }

            string keyName = cells.Count > 0 ? cells[0] : string.Empty;
            mismatches.Add(new MismatchRecord
            {
                LineNumber = lineNumber,
                KeyName = keyName,
                ExpectedCellCount = expectedCellCount,
                ActualCellCount = cells.Count
            });
        }

        return mismatches;
    }

    private bool ShouldSkipLine(string line)
    {
        if (IgnoreEmptyLines && string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        if (!IgnoreCommentLines)
        {
            return false;
        }

        string trimmedLine = line?.TrimStart() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedLine))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(CommentPrefix))
        {
            return false;
        }

        return trimmedLine.StartsWith(CommentPrefix, StringComparison.Ordinal);
    }

    private void PrintMismatches(ScanResult result)
    {
        if (result.MismatchesByFile.Count == 0)
        {
            GD.Print("[CsvTranslationRowCountScanner] 未发现列数不一致的翻译行。");
            return;
        }

        foreach (KeyValuePair<string, List<MismatchRecord>> pair in result.MismatchesByFile)
        {
            GD.Print($"[CsvTranslationRowCountScanner] 文件：{pair.Key}");

            for (int i = 0; i < pair.Value.Count; i++)
            {
                MismatchRecord record = pair.Value[i];
                GD.PrintErr(
                    $"  行号 {record.LineNumber}，Key: {record.KeyName}，" +
                    $"首行列数 {record.ExpectedCellCount}，当前列数 {record.ActualCellCount}");
            }

            GD.Print("  Keys:");
            for (int i = 0; i < pair.Value.Count; i++)
            {
                GD.Print(pair.Value[i].KeyName);
            }
        }
    }

    private string BuildReport(ScanResult result)
    {
        StringBuilder sb = new();

        sb.AppendLine("# CSV Translation Row Count Report");
        sb.AppendLine($"# Generated At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Output Path: {NormalizeResPath(OutputReportPath)}");
        sb.AppendLine($"# Scanned Csv Count: {result.ScannedCsvCount}");
        sb.AppendLine($"# Files With Mismatch: {result.FilesWithMismatchCount}");
        sb.AppendLine($"# Total Mismatch Rows: {result.TotalMismatchRowCount}");
        sb.AppendLine();

        sb.AppendLine("## Translation CSV Sources");
        for (int i = 0; i < TranslationCsvPaths.Count; i++)
        {
            sb.AppendLine($"- {NormalizeResPath(TranslationCsvPaths[i])}");
        }

        sb.AppendLine();

        if (result.MismatchesByFile.Count == 0)
        {
            sb.AppendLine("## Result");
            sb.AppendLine("No mismatched row counts found.");
            return sb.ToString();
        }

        sb.AppendLine("## Mismatched Rows By File");
        sb.AppendLine();

        foreach (KeyValuePair<string, List<MismatchRecord>> pair in result.MismatchesByFile)
        {
            sb.AppendLine($"### {pair.Key}");
            for (int i = 0; i < pair.Value.Count; i++)
            {
                MismatchRecord record = pair.Value[i];
                sb.AppendLine(
                    $"- Line {record.LineNumber}, Key: {record.KeyName}, Expected: {record.ExpectedCellCount}, Actual: {record.ActualCellCount}");
            }

            sb.AppendLine();
            sb.AppendLine("Keys:");
            for (int i = 0; i < pair.Value.Count; i++)
            {
                sb.AppendLine(pair.Value[i].KeyName);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private bool TryWriteReport(string content, out string message)
    {
        string normalizedPath = NormalizeResPath(OutputReportPath);
        if (!IsPathInsideProject(normalizedPath))
        {
            message = "输出路径不在项目目录内。";
            return false;
        }

        string absolutePath = ProjectSettings.GlobalizePath(normalizedPath);
        absolutePath = Path.GetFullPath(absolutePath);

        string? directory = Path.GetDirectoryName(absolutePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            message = "无法确定输出目录。";
            return false;
        }

        Error dirError = DirAccess.MakeDirRecursiveAbsolute(directory);
        if (dirError != Error.Ok)
        {
            message = $"创建输出目录失败：{dirError}";
            return false;
        }

        using Godot.FileAccess file = Godot.FileAccess.Open(absolutePath, Godot.FileAccess.ModeFlags.Write);
        if (file == null)
        {
            message = $"无法打开输出文件：{absolutePath}";
            return false;
        }

        file.StoreString(content);
        message = string.Empty;
        return true;
    }

    private bool IsPathInsideProject(string resPath)
    {
        if (!resPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string projectRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string targetPath = Path.GetFullPath(ProjectSettings.GlobalizePath(resPath));

        return targetPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeResPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string normalized = path.Replace('\\', '/').Trim();
        while (normalized.Contains("//"))
        {
            normalized = normalized.Replace("//", "/");
        }

        if (normalized.StartsWith("res:/", StringComparison.OrdinalIgnoreCase)
            && !normalized.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Replace("res:/", "res://");
        }

        return normalized;
    }

    private static List<string> ParseCsvCells(string line)
    {
        List<string> cells = new();
        if (line == null)
        {
            return cells;
        }

        StringBuilder cell = new();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                continue;
            }

            cell.Append(c);
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }

    private sealed class ScanResult
    {
        public int ScannedCsvCount { get; set; }
        public int FilesWithMismatchCount { get; set; }
        public int TotalMismatchRowCount { get; set; }
        public SortedDictionary<string, List<MismatchRecord>> MismatchesByFile { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class MismatchRecord
    {
        public int LineNumber { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public int ExpectedCellCount { get; set; }
        public int ActualCellCount { get; set; }
    }
}
