using System.Text;

namespace PortableRdpManager;

internal sealed class RdpDocument
{
    private readonly List<RdpLine> _lines = [];

    public string? FilePath { get; set; }

    public static RdpDocument Create() => new();

    public static RdpDocument Load(string path)
    {
        var document = new RdpDocument { FilePath = Path.GetFullPath(path) };
        foreach (var line in File.ReadAllLines(path, Encoding.Unicode))
            document._lines.Add(RdpLine.Parse(line));

        return document;
    }

    public string GetString(string key, string defaultValue = "")
    {
        var line = Find(key);
        return line?.Value ?? defaultValue;
    }

    public int GetInt(string key, int defaultValue = 0) =>
        int.TryParse(GetString(key), out var value) ? value : defaultValue;

    public bool GetBool(string key, bool defaultValue = false) =>
        GetInt(key, defaultValue ? 1 : 0) != 0;

    public void SetString(string key, string? value) => Set(key, "s", value ?? "");
    public void SetInt(string key, int value) => Set(key, "i", value.ToString());
    public void SetBool(string key, bool value) => SetInt(key, value ? 1 : 0);

    public void Save(string path)
    {
        var fullPath = Path.GetFullPath(path);
        File.WriteAllLines(fullPath, _lines.Select(line => line.Serialize()), Encoding.Unicode);
        FilePath = fullPath;
    }

    public string ToRawText() =>
        string.Join(Environment.NewLine, _lines.Select(line => line.Serialize()));

    public void ReplaceRawText(string text)
    {
        _lines.Clear();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            _lines.Add(RdpLine.Parse(line.TrimEnd('\r')));
    }

    private RdpLine? Find(string key) =>
        _lines.LastOrDefault(line => line.IsSetting &&
            string.Equals(line.Key, key, StringComparison.OrdinalIgnoreCase));

    private void Set(string key, string type, string value)
    {
        var line = Find(key);
        if (line is null)
            _lines.Add(new RdpLine(key, type, value));
        else
        {
            line.Type = type;
            line.Value = value;
        }
    }

    private sealed class RdpLine(string raw)
    {
        public string Raw { get; } = raw;
        public string Key { get; private set; } = "";
        public string Type { get; set; } = "";
        public string Value { get; set; } = "";
        public bool IsSetting { get; private set; }

        public RdpLine(string key, string type, string value) : this("")
        {
            Key = key;
            Type = type;
            Value = value;
            IsSetting = true;
        }

        public static RdpLine Parse(string raw)
        {
            var first = raw.IndexOf(':');
            var second = first < 0 ? -1 : raw.IndexOf(':', first + 1);
            if (first <= 0 || second != first + 2)
                return new RdpLine(raw);

            var type = raw.Substring(first + 1, 1);
            if (type is not ("s" or "i" or "b"))
                return new RdpLine(raw);

            return new RdpLine(raw[..first], type, raw[(second + 1)..]);
        }

        public string Serialize() => IsSetting ? $"{Key}:{Type}:{Value}" : Raw;
    }
}
