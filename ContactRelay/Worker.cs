using Microsoft.Extensions.Options;
using ContactRelay.Options;
using ContactRelay.Services;

namespace ContactRelay;

public sealed class Worker(
    ISyncOrchestrator orchestrator,
    IOptions<SyncWorkerOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly SyncWorkerOptions _options = options.Value;
    private readonly SemaphoreSlim _runLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Directory contact sync worker starting. DryRun={DryRun} DailyRunTime={DailyRunTime}",
            _options.DryRun,
            _options.Schedule ?? _options.DailyRunTime);

        if (!_options.Enabled)
        {
            logger.LogWarning("Directory contact sync worker is disabled by configuration.");
            return;
        }

        if (_options.RunOnStartup) await RunSafelyAsync(stoppingToken);

        if (SyncSchedule.TryGetDailyRunTime(_options, out var dailyRunTime))
        {
            await RunDailyScheduleAsync(dailyRunTime, stoppingToken);
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes)));

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await RunSafelyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

        logger.LogInformation("Directory contact sync worker stopped.");
    }

    private async Task RunDailyScheduleAsync(TimeSpan dailyRunTime, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var delay = GetDelayUntilNextRun(dailyRunTime);
                logger.LogInformation("Next directory contact sync is scheduled in {Delay} at {RunTime}.", delay,
                    dailyRunTime);

                await Task.Delay(delay, stoppingToken);
                await RunSafelyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

        logger.LogInformation("Directory contact sync worker stopped.");
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        if (!await _runLock.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            logger.LogWarning("Skipping directory contact sync run because a previous run is still active.");
            return;
        }

        try
        {
            await orchestrator.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                "Directory contact sync run failed. Details were recorded by the sync orchestrator. ErrorType={ErrorType}",
                ex.GetType().Name);
            throw;
        }
        finally
        {
            _runLock.Release();
        }
    }

    private static TimeSpan GetDelayUntilNextRun(TimeSpan dailyRunTime)
    {
        var now = DateTime.Now;
        var nextRun = now.Date.Add(dailyRunTime);
        if (nextRun <= now) nextRun = nextRun.AddDays(1);

        return nextRun - now;
    }
}
