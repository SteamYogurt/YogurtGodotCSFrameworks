using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

[Tool]
[GlobalClass]
public partial class MissingTranslationScanner : Resource
{
    [Export]
    public string OutputReportPath { get; set; } = "res://Temp/MissingTranslationsReport.txt";

    [Export]
    public Array<string> TranslationCsvPaths { get; set; } = new();

    [Export]
    public Array<string> ExcludedPaths { get; set; } = new();

    [ExportToolButton("校验配置")]
    public Callable ValidateConfigButton => Callable.From(ValidateConfig);

    [ExportToolButton("预览缺失统计")]
    public Callable PreviewMissingButton => Callable.From(PreviewMissingTranslations);

    [ExportToolButton("生成缺失报告")]
    public Callable GenerateReportButton => Callable.From(GenerateMissingTranslationReport);

    private static readonly Regex TrRegex = new(
        @"(?<!\w)Tr\s*\(\s*(?:@""(?<verbatim>(?:""""|[^""])*)""|""(?<normal>(?:\\.|[^""\\])*)"")",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public void ValidateConfig()
    {
        if (!TryValidateConfig(out string message))
        {
            GD.PrintErr($"[MissingTranslationScanner] 配置校验失败：{message}");
            return;
        }

        GD.Print("[MissingTranslationScanner] 配置校验通过。");
    }

    public void PreviewMissingTranslations()
    {
        if (!TryValidateConfig(out string message))
        {
            GD.PrintErr($"[MissingTranslationScanner] 配置校验失败：{message}");
            return;
        }

        ScanResult result = Scan();

        GD.Print(
            $"[MissingTranslationScanner] 预览完成：脚本 {result.ScannedScriptCount} 个，" +
            $"已加载翻译 Key {result.KnownTranslationCount} 个，" +
            $"存在缺失的脚本 {result.FilesWithMissingCount} 个，" +
            $"缺失 Key 总数 {result.TotalMissingKeyCount} 个。");

        foreach (KeyValuePair<string, SortedSet<string>> pair in result.MissingByScript)
        {
            GD.Print($"  - {pair.Key}: 缺失 {pair.Value.Count} 个");
        }
    }

    public void GenerateMissingTranslationReport()
    {
        if (!TryValidateConfig(out string message))
        {
            GD.PrintErr($"[MissingTranslationScanner] 配置校验失败：{message}");
            return;
        }

        ScanResult result = Scan();
        string report = BuildReport(result);

        if (!TryWriteReport(report, out string writeMessage))
        {
            GD.PrintErr($"[MissingTranslationScanner] 报告写入失败：{writeMessage}");
            return;
        }

        GD.Print(
            $"[MissingTranslationScanner] 报告已生成：{OutputReportPath}，" +
            $"存在缺失的脚本 {result.FilesWithMissingCount} 个，" +
            $"缺失 Key 总数 {result.TotalMissingKeyCount} 个。");
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
            message = "至少需要配置一个翻译源 CSV。";
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

        if (ExcludedPaths != null)
        {
            for (int i = 0; i < ExcludedPaths.Count; i++)
            {
                string excluded = NormalizeResPath(ExcludedPaths[i]);
                if (string.IsNullOrWhiteSpace(excluded))
                {
                    continue;
                }

                if (!excluded.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                {
                    message = $"排除路径必须以 res:// 开头：{excluded}";
                    return false;
                }
            }
        }

        message = string.Empty;
        return true;
    }

    private ScanResult Scan()
    {
        HashSet<string> knownKeys = LoadTranslationKeys();
        List<string> csFiles = new();
        CollectCsFiles("res://", csFiles);

        SortedDictionary<string, SortedSet<string>> missingByScript =
            new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < csFiles.Count; i++)
        {
            string scriptPath = csFiles[i];
            HashSet<string> trKeys = ExtractTrKeysFromScript(scriptPath);
            if (trKeys.Count == 0)
            {
                continue;
            }

            SortedSet<string> missingSet = new(StringComparer.Ordinal);
            foreach (string key in trKeys)
            {
                if (!knownKeys.Contains(key))
                {
                    missingSet.Add(key);
                }
            }

            if (missingSet.Count > 0)
            {
                missingByScript[scriptPath] = missingSet;
            }
        }

        int totalMissing = 0;
        foreach (KeyValuePair<string, SortedSet<string>> pair in missingByScript)
        {
            totalMissing += pair.Value.Count;
        }

        return new ScanResult
        {
            ScannedScriptCount = csFiles.Count,
            KnownTranslationCount = knownKeys.Count,
            MissingByScript = missingByScript,
            TotalMissingKeyCount = totalMissing,
            FilesWithMissingCount = missingByScript.Count
        };
    }

    private HashSet<string> LoadTranslationKeys()
    {
        HashSet<string> keys = new(StringComparer.Ordinal);

        for (int i = 0; i < TranslationCsvPaths.Count; i++)
        {
            string csvPath = NormalizeResPath(TranslationCsvPaths[i]);
            if (string.IsNullOrWhiteSpace(csvPath) || !Godot.FileAccess.FileExists(csvPath))
            {
                continue;
            }

            using Godot.FileAccess file = Godot.FileAccess.Open(csvPath, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"[MissingTranslationScanner] 无法打开 CSV：{csvPath}");
                continue;
            }

            bool isFirstLine = true;
            while (!file.EofReached())
            {
                string line = file.GetLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    isFirstLine = false;
                    continue;
                }

                string firstCell = ParseFirstCsvCell(line);
                if (isFirstLine)
                {
                    firstCell = firstCell.TrimStart('\uFEFF');
                }

                isFirstLine = false;

                if (!string.IsNullOrWhiteSpace(firstCell))
                {
                    keys.Add(firstCell);
                }
            }
        }

        return keys;
    }

    private void CollectCsFiles(string directoryPath, List<string> result)
    {
        directoryPath = NormalizeResPath(directoryPath);
        if (IsExcluded(directoryPath))
        {
            return;
        }

        DirAccess dir = DirAccess.Open(directoryPath);
        if (dir == null)
        {
            return;
        }

        dir.ListDirBegin();
        while (true)
        {
            string name = dir.GetNext();
            if (string.IsNullOrEmpty(name))
            {
                break;
            }

            if (name == "." || name == "..")
            {
                continue;
            }

            string childPath = CombineResPath(directoryPath, name);

            if (dir.CurrentIsDir())
            {
                if (!IsExcluded(childPath))
                {
                    CollectCsFiles(childPath, result);
                }

                continue;
            }

            if (childPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(childPath);
            }
        }

        dir.ListDirEnd();
    }

    private HashSet<string> ExtractTrKeysFromScript(string scriptPath)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);

        using Godot.FileAccess file = Godot.FileAccess.Open(scriptPath, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[MissingTranslationScanner] 无法读取脚本：{scriptPath}");
            return keys;
        }

        string content = file.GetAsText();
        if (string.IsNullOrWhiteSpace(content))
        {
            return keys;
        }

        MatchCollection matches = TrRegex.Matches(content);
        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            string value;

            Group verbatimGroup = match.Groups["verbatim"];
            if (verbatimGroup.Success)
            {
                value = verbatimGroup.Value.Replace("\"\"", "\"");
            }
            else
            {
                value = Regex.Unescape(match.Groups["normal"].Value);
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                keys.Add(value);
            }
        }

        return keys;
    }

    private string BuildReport(ScanResult result)
    {
        StringBuilder sb = new();

        sb.AppendLine("# Missing Translation Report");
        sb.AppendLine($"# Generated At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Output Path: {NormalizeResPath(OutputReportPath)}");
        sb.AppendLine($"# Scanned Scripts: {result.ScannedScriptCount}");
        sb.AppendLine($"# Known Translation Keys: {result.KnownTranslationCount}");
        sb.AppendLine($"# Files With Missing: {result.FilesWithMissingCount}");
        sb.AppendLine($"# Total Missing Keys: {result.TotalMissingKeyCount}");
        sb.AppendLine();

        sb.AppendLine("## Translation CSV Sources");
        for (int i = 0; i < TranslationCsvPaths.Count; i++)
        {
            sb.AppendLine($"- {NormalizeResPath(TranslationCsvPaths[i])}");
        }

        sb.AppendLine();
        sb.AppendLine("## Excluded Paths");
        if (ExcludedPaths == null || ExcludedPaths.Count == 0)
        {
            sb.AppendLine("- <none>");
        }
        else
        {
            for (int i = 0; i < ExcludedPaths.Count; i++)
            {
                sb.AppendLine($"- {NormalizeResPath(ExcludedPaths[i])}");
            }
        }

        sb.AppendLine();

        if (result.MissingByScript.Count == 0)
        {
            sb.AppendLine("## Result");
            sb.AppendLine("No missing translation keys found.");
            return sb.ToString();
        }

        sb.AppendLine("## Missing Keys By Script");
        sb.AppendLine();

        foreach (KeyValuePair<string, SortedSet<string>> pair in result.MissingByScript)
        {
            sb.AppendLine($"### {pair.Key}");
            foreach (string key in pair.Value)
            {
                sb.AppendLine(key);
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

    private bool IsExcluded(string path)
    {
        string normalizedPath = NormalizeResPath(path);

        if (ExcludedPaths == null || ExcludedPaths.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < ExcludedPaths.Count; i++)
        {
            string excluded = NormalizeResPath(ExcludedPaths[i]);
            if (string.IsNullOrWhiteSpace(excluded))
            {
                continue;
            }

            if (IsSameOrChildPath(normalizedPath, excluded))
            {
                return true;
            }
        }

        return false;
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

    private static bool IsSameOrChildPath(string path, string parent)
    {
        path = NormalizeResPath(path).TrimEnd('/');
        parent = NormalizeResPath(parent).TrimEnd('/');

        return path.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string CombineResPath(string left, string right)
    {
        left = NormalizeResPath(left).TrimEnd('/');
        right = NormalizeResPath(right).TrimStart('/');
        return $"{left}/{right}";
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

    private static string ParseFirstCsvCell(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        int index = 0;
        StringBuilder sb = new();

        if (line[0] == '"')
        {
            index = 1;
            while (index < line.Length)
            {
                char c = line[index];

                if (c == '"')
                {
                    if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        sb.Append('"');
                        index += 2;
                        continue;
                    }

                    break;
                }

                sb.Append(c);
                index++;
            }

            return sb.ToString().Trim();
        }

        while (index < line.Length)
        {
            char c = line[index];
            if (c == ',')
            {
                break;
            }

            sb.Append(c);
            index++;
        }

        return sb.ToString().Trim();
    }

    private sealed class ScanResult
    {
        public int ScannedScriptCount { get; set; }
        public int KnownTranslationCount { get; set; }
        public int FilesWithMissingCount { get; set; }
        public int TotalMissingKeyCount { get; set; }
        public SortedDictionary<string, SortedSet<string>> MissingByScript { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}