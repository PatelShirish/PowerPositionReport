using PowerPositionReportService.Utils;
using Services;

namespace PowerPositionReportService
{
    public class PowerPositionReportWorker : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IPowerService _powerService;
        private readonly ILogger<PowerPositionReportWorker> _logger;
        private readonly ITimeProvider _timeProvider;
        private readonly IFileWriter _fileWriter;
        private readonly TimeZoneInfo _londonTimeZone;
        private readonly string _outputDirectory;
        private readonly int _intervalMinutes;
        private readonly int _retryCount;
        private readonly int _retryDelaySeconds;

        public PowerPositionReportWorker(IConfiguration configuration,
                      IPowerService powerService,
                      ILogger<PowerPositionReportWorker> logger,
                      ITimeProvider timeProvider,
                      IFileWriter fileWriter)
        {
            _configuration = configuration;
            _powerService = powerService;
            _logger = logger;
            _timeProvider = timeProvider;
            _fileWriter = fileWriter;

            // Use the "GMT Standard Time" zone for London.
            _londonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

            _outputDirectory = _configuration["OutputDirectory"] ?? "C:\\PowerPositionReports";
            if (!int.TryParse(_configuration["IntervalMinutes"], out _intervalMinutes))
            {
                _intervalMinutes = 15;
            }

            // Retry settings.
            if (!int.TryParse(_configuration["RetryCount"], out _retryCount))
            {
                _retryCount = 3;
            }
            if (!int.TryParse(_configuration["RetryDelaySeconds"], out _retryDelaySeconds))
            {
                _retryDelaySeconds = 2;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Power Position Service started at: {time}", _timeProvider.Now);
            
            // Start immediately.
            DateTime nextRun = _timeProvider.Now;

            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime now = _timeProvider.Now;
                if (now < nextRun)
                {
                    TimeSpan delay = nextRun - now;
                    _logger.LogInformation("Waiting {delay} before next extraction.", delay);
                    await Task.Delay(delay, stoppingToken);
                }

                try
                {
                    DateTime startTime = _timeProvider.Now;
                    await RunExtractAsync(stoppingToken);
                    DateTime endTime = _timeProvider.Now;
                    _logger.LogInformation("Extraction completed in {duration} seconds.", (endTime - startTime).TotalSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during extraction.");
                }

                // Schedule the next run.
                nextRun = nextRun.AddMinutes(_intervalMinutes);
                if (nextRun < _timeProvider.Now)
                {
                    nextRun = _timeProvider.Now;
                }
            }
        }

        public virtual async Task RunExtractAsync(CancellationToken stoppingToken)
        {
            // Calculate London local time.
            DateTime utcNow = _timeProvider.UtcNow;
            DateTime londonNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, _londonTimeZone);

            // Set trading day (day-ahead so add 1)
            DateTime tradingDay = londonNow.Date.AddDays(1);
            _logger.LogInformation("Running extract at {extractTime} for trading day {tradingDay}.", londonNow, tradingDay.ToShortDateString());

            IEnumerable<PowerTrade> trades = null;
            int attempt = 0;
            bool success = false;
            Exception lastException = null;
            while (attempt < _retryCount && !success && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    trades = await _powerService.GetTradesAsync(tradingDay);
                    success = true;
                }
                catch (PowerServiceException pse)
                {
                    attempt++;
                    lastException = pse;
                    _logger.LogWarning(pse, "Attempt {attempt} failed to retrieve trades.", attempt);
                    if (attempt < _retryCount)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_retryDelaySeconds), stoppingToken);
                    }
                }
            }

            if (!success)
            {
                _logger.LogError(lastException, "Failed to retrieve trades after {retryCount} attempts.", _retryCount);
                return;
            }

            // Validate and raise warning if periods less than 24
            int expectedPeriods = 24;
            int numberOfPeriods = trades.FirstOrDefault()?.Periods?.Length ?? 0;
            if (numberOfPeriods != expectedPeriods)
            {
                _logger.LogWarning("Expected {expected} periods, but received {actual}.", expectedPeriods, numberOfPeriods);
            }

            // Aggregate volumes per period.
            var aggregatedVolumes = new Dictionary<int, double>();
            for (int period = 1; period <= numberOfPeriods; period++)
            {
                aggregatedVolumes[period] = trades
                    .SelectMany(trade => trade.Periods)
                    .Where(p => p.Period == period)
                    .Sum(p => p.Volume);
            }

            // Build CSV.
            var csvLines = new List<string> { "Local Time,Volume" };
            
            // Map Period to time i.e. 1 = 23:00, 2 = 00:00 and so on.
            for (int period = 1; period <= numberOfPeriods; period++)
            {
                string localTimeStr = (period == 1) ? "23:00" : ((period - 2).ToString("D2") + ":00");
                double volume = aggregatedVolumes.ContainsKey(period) ? aggregatedVolumes[period] : 0;
                csvLines.Add($"{localTimeStr},{volume}");
            }

            // Create & write CSV file
            string fileName = $"PowerPosition_{londonNow:yyyyMMdd_HHmm}.csv";
            string fullPath = Path.Combine(_outputDirectory, fileName);

            _fileWriter.CreateDirectory(_outputDirectory);

            await _fileWriter.WriteAllLinesAsync(fullPath, csvLines, stoppingToken);

            _logger.LogInformation("CSV file written: {file}", fullPath);
        }
    }
}
