namespace WinServicesRAG.Core.Helper;

public class RuntimeDataHelper
{
    private static readonly Dictionary<string, object> _data = new Dictionary<string, object>();

    public static void SetData(string key, object value)
    {
        _data[key: key] = value;
    }

    public static object? GetData(string key)
    {
        return _data.GetValueOrDefault(key: key);
    }

    public static object GetData(string key, object defaultValue)
    {
        return _data.GetValueOrDefault(key: key, defaultValue: defaultValue);
    }
}
