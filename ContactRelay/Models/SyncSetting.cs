namespace ContactRelay.Models;

public sealed class SyncSetting
{
    public string SettingKey { get; init; } = "";

    public string? SettingValue { get; init; }

    public string ValueType { get; init; } = "String";
}
