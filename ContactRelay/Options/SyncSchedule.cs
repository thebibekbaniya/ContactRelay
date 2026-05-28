namespace ContactRelay.Options;

public static class SyncSchedule
{
    public static bool TryGetDailyRunTime(SyncWorkerOptions options, out TimeSpan dailyRunTime)
    {
        if (TryParseCronDailySchedule(options.Schedule, out dailyRunTime))
        {
            return true;
        }

        return TimeSpan.TryParse(options.DailyRunTime, out dailyRunTime)
               && dailyRunTime >= TimeSpan.Zero
               && dailyRunTime < TimeSpan.FromDays(1);
    }

    private static bool TryParseCronDailySchedule(string? schedule, out TimeSpan dailyRunTime)
    {
        dailyRunTime = default;

        if (string.IsNullOrWhiteSpace(schedule))
        {
            return false;
        }

        var fields = schedule.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != 6)
        {
            return false;
        }

        if (!int.TryParse(fields[0], out var second)
            || !int.TryParse(fields[1], out var minute)
            || !int.TryParse(fields[2], out var hour))
        {
            return false;
        }

        if (second is < 0 or > 59 || minute is < 0 or > 59 || hour is < 0 or > 23)
        {
            return false;
        }

        if (fields[3] != "*" || fields[4] != "*" || fields[5] != "*")
        {
            return false;
        }

        dailyRunTime = new TimeSpan(hour, minute, second);
        return true;
    }
}
