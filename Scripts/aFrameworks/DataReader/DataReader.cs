using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using GNScripter.src;

public partial class DataReader : Singleton<DataReader>
{
    private Dictionary<string, CsvTable> tables = new();
    private static Random rng = new Random((int)GD.Randi());

    public override void _EnterTree()
    {
        base._EnterTree();
    }

    public void LoadCsv(string path, string tag)
    {
        try
        {
            using var fs = new FileAccessStream(path, Godot.FileAccess.ModeFlags.Read);
            using var reader = new StreamReader(fs);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null
            });

            var table = new CsvTable(csv, tag);
            tables[tag] = table;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[CSV] 加载失败: {path} -> {e.Message}");
        }
    }

    public static T Get<T>(string tag, string key, string column, T defaultValue = default)
    {
        if (!Instance.tables.TryGetValue(tag, out var table))
            return defaultValue;

        if (!table.TryGetRow(key, out var row))
            return defaultValue;

        if (!row.TryGet(column, out var raw))
            return defaultValue;

        if (string.IsNullOrEmpty(raw))
            return defaultValue;

        return CsvConvert.Convert(raw, defaultValue);
    }

    public static Dictionary<string, string> GetColumn(string tag, string column)
    {
        if (!Instance.tables.TryGetValue(tag, out var table))
            return new Dictionary<string, string>();

        return table.GetColumn(column);
    }

    public static List<string> FindKeys(string tag, Dictionary<string, string> conditions)
    {
        if (!Instance.tables.TryGetValue(tag, out var table))
            return new List<string>();

        return table.FindKeys(conditions);
    }

    public static List<string> FindKeysNumeric(string tag, string column, string op, float value)
    {
        if (!Instance.tables.TryGetValue(tag, out var table))
            return new List<string>();

        return table.FindKeysNumeric(column, op, value);
    }

    public static List<string> FindByTag(string tag, string column, string tagValue)
    {
        if (!Instance.tables.TryGetValue(tag, out var table))
            return new List<string>();

        return table.FindByTag(column, tagValue);
    }

    public static List<string> SortKeys(string tag, List<string> keys, string column, bool desc = false)
    {
        if (!Instance.tables.TryGetValue(tag, out var table))
            return keys;

        return table.SortKeys(keys, column, desc);
    }

    public static string RandomPick(List<string> keys)
    {
        if (keys == null || keys.Count == 0)
            return null;

        return keys[rng.Next(keys.Count)];
    }

    public static string RandomPickByWeight(string tag, List<string> keys, string weightColumn)
    {
        if (!Instance.tables.TryGetValue(tag, out var table))
            return null;

        return table.RandomPickByWeight(keys, weightColumn);
    }

    public static QueryBuilder Query(string tag)
    {
        if (!Instance.tables.TryGetValue(tag, out var table))
            return null;

        return new QueryBuilder(table);
    }

    public class CsvTable
    {
        private Dictionary<string, int> headerMap = new();
        private Dictionary<string, CsvRow> rows = new();

        public CsvTable(CsvReader csv, string tag)
        {
            Load(csv);
        }

        private void Load(CsvReader csv)
        {
            csv.Read();
            csv.ReadHeader();

            for (int i = 0; i < csv.HeaderRecord.Length; i++)
                headerMap[csv.HeaderRecord[i]] = i;

            while (csv.Read())
            {
                var values = new string[headerMap.Count];
                for (int i = 0; i < values.Length; i++)
                    values[i] = csv.GetField(i) ?? "";

                var key = values[0];
                if (string.IsNullOrEmpty(key))
                    continue;

                if (rows.ContainsKey(key))
                    continue;

                rows[key] = new CsvRow(values, headerMap);
            }
        }

        public bool TryGetRow(string key, out CsvRow row)
            => rows.TryGetValue(key, out row);

        public Dictionary<string, string> GetColumn(string column)
        {
            Dictionary<string, string> result = new();
            foreach (var kv in rows)
                if (kv.Value.TryGet(column, out var v))
                    result[kv.Key] = v;
            return result;
        }

        public List<string> FindKeys(Dictionary<string, string> conditions)
        {
            List<string> result = new();
            foreach (var kv in rows)
            {
                bool ok = true;
                foreach (var cond in conditions)
                {
                    if (!kv.Value.TryGet(cond.Key, out var v) || v != cond.Value)
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok) result.Add(kv.Key);
            }
            return result;
        }

        public List<string> FindKeysNumeric(string column, string op, float value)
        {
            List<string> result = new();
            foreach (var kv in rows)
            {
                if (!kv.Value.TryGet(column, out var raw))
                    continue;

                if (!float.TryParse(raw, out var num))
                    continue;

                bool match = op switch
                {
                    ">" => num > value,
                    "<" => num < value,
                    ">=" => num >= value,
                    "<=" => num <= value,
                    "==" => num == value,
                    "!=" => num != value,
                    _ => false
                };

                if (match)
                    result.Add(kv.Key);
            }
            return result;
        }

        public List<string> FindByTag(string column, string tag)
        {
            List<string> result = new();
            foreach (var kv in rows)
            {
                if (!kv.Value.TryGet(column, out var raw))
                    continue;

                var parts = raw.Split(',');
                foreach (var p in parts)
                {
                    if (p.Trim() == tag)
                    {
                        result.Add(kv.Key);
                        break;
                    }
                }
            }
            return result;
        }

        public List<string> SortKeys(List<string> keys, string column, bool desc)
        {
            keys.Sort((a, b) =>
            {
                rows[a].TryGet(column, out var va);
                rows[b].TryGet(column, out var vb);

                if (float.TryParse(va, out var na) && float.TryParse(vb, out var nb))
                    return desc ? nb.CompareTo(na) : na.CompareTo(nb);

                return desc ? string.Compare(vb, va) : string.Compare(va, vb);
            });
            return keys;
        }

        public string RandomPickByWeight(List<string> keys, string column)
        {
            float total = 0;
            List<(string key, float weight)> pool = new();

            foreach (var k in keys)
            {
                if (!rows[k].TryGet(column, out var raw))
                    continue;

                if (!float.TryParse(raw, out var w))
                    continue;

                total += w;
                pool.Add((k, w));
            }

            float roll = (float)(rng.NextDouble() * total);
            float acc = 0;

            foreach (var p in pool)
            {
                acc += p.weight;
                if (roll <= acc)
                    return p.key;
            }

            return pool.Count > 0 ? pool[0].key : null;
        }

        public IEnumerable<string> AllKeys() => rows.Keys;
        public CsvRow GetRow(string key) => rows[key];
    }

    public class CsvRow
    {
        private string[] values;
        private Dictionary<string, int> headerMap;

        public CsvRow(string[] values, Dictionary<string, int> headerMap)
        {
            this.values = values;
            this.headerMap = headerMap;
        }

        public bool TryGet(string column, out string value)
        {
            if (!headerMap.TryGetValue(column, out var index))
            {
                value = "";
                return false;
            }

            value = index < values.Length ? values[index] : "";
            return true;
        }
    }

    public class QueryBuilder
    {
        private CsvTable table;
        private List<string> keys;

        public QueryBuilder(CsvTable table)
        {
            this.table = table;
            keys = new List<string>(table.AllKeys());
        }

        public QueryBuilder Where(string column, string value)
        {
            keys = table.FindKeys(new Dictionary<string, string> { { column, value } });
            return this;
        }

        public QueryBuilder WhereNumeric(string column, string op, float val)
        {
            keys = table.FindKeysNumeric(column, op, val);
            return this;
        }

        public QueryBuilder WithTag(string column, string tag)
        {
            keys = table.FindByTag(column, tag);
            return this;
        }

        public QueryBuilder SortBy(string column, bool desc = false)
        {
            keys = table.SortKeys(keys, column, desc);
            return this;
        }

        public string RandomOne()
        {
            return DataReader.RandomPick(keys);
        }

        public List<string> ToList() => keys;
    }

    static class CsvConvert
    {
        public static T Convert<T>(string raw, T def)
        {
            try
            {
                var type = typeof(T);
                if (type == typeof(string))
                    return (T)(object)raw;

                if (type.IsEnum)
                {
                    if (Enum.TryParse(type, raw, true, out var e))
                        return (T)e;
                }
                else
                {
                    return (T)System.Convert.ChangeType(raw, type);
                }
            }
            catch { }
            return def;
        }
    }
}
