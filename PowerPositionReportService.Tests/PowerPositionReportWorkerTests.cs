using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services;
using PowerPositionReportService.Utils;

namespace PowerPositionReportService.Tests
{
    public class FakeTimeProvider : ITimeProvider
    {
        public DateTime UtcNowValue { get; set; }
        public DateTime NowValue { get; set; }
        public DateTime UtcNow => UtcNowValue;
        public DateTime Now => NowValue;
    }

    public class FakeFileWriter : IFileWriter
    {
        public string WrittenFilePath { get; private set; }
        public List<string> WrittenLines { get; private set; }

        public Task WriteAllLinesAsync(string path, IEnumerable<string> lines, CancellationToken cancellationToken)
        {
            WrittenFilePath = path;
            WrittenLines = lines.ToList();
            return Task.CompletedTask;
        }

        public void CreateDirectory(string path)
        {
            // No operation needed for testing.
        }
    }

    [TestClass]
    public class PowerPositionReportWorkerTests
    {
        private IConfiguration _configuration;
        private FakeTimeProvider _fakeTimeProvider;
        private FakeFileWriter _fakeFileWriter;
        private Mock<IPowerService> _powerServiceMock;
        private ILogger<PowerPositionReportWorker> _logger;

        [TestInitialize]
        public void Setup()
        {
            // In-memory configuration with our settings.
            var configDict = new Dictionary<string, string>
            {
                { "OutputDirectory", "C:\\FakeOutput" },
                { "IntervalMinutes", "60" },
                { "RetryCount", "3" },
                { "RetryDelaySeconds", "1" }
            };
            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configDict)
                .Build();

            // Set Now DateTime, GMT is same as UTC
            _fakeTimeProvider = new FakeTimeProvider
            {
                UtcNowValue = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                NowValue = new DateTime(2025, 1, 15, 12, 0, 0)
            };

            _fakeFileWriter = new FakeFileWriter();

            _powerServiceMock = new Mock<IPowerService>();

            // Trading day is today + 1
            DateTime tradingDay = _fakeTimeProvider.Now.Date.AddDays(1); // 2025-01-16

            // Trade 1: Volume 100
            var trade1 = PowerTrade.Create(tradingDay, 24);
            foreach (var period in trade1.Periods)
            {
                period.Volume = 100;
            }

            // Trade 2: (Periods 1-11 volume 50) (Periods 12-24 Volume -20)
            var trade2 = PowerTrade.Create(tradingDay, 24);
            foreach (var period in trade2.Periods)
            {
                period.Volume = period.Period <= 11 ? 50 : -20;
            }

            // Setup GetTradesAsync to return test data
            _powerServiceMock
                .Setup(ps => ps.GetTradesAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<PowerTrade> { trade1, trade2 });

            // Set up a simple logger (using Debug logging for tests).
            var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
            _logger = loggerFactory.CreateLogger<PowerPositionReportWorker>();
        }

        [TestMethod]
        public async Task RunExtractAsync_GeneratesExpectedCsvFile()
        {
            // Arrange
            var worker = new PowerPositionReportWorker(_configuration, _powerServiceMock.Object, _logger, _fakeTimeProvider, _fakeFileWriter);

            // Act
            await worker.RunExtractAsync(CancellationToken.None);

            // Asserts
            Assert.IsNotNull(_fakeFileWriter.WrittenFilePath, "File path was not written.");
            Assert.IsNotNull(_fakeFileWriter.WrittenLines, "CSV content was not written.");

            string expectedFileName = "PowerPosition_20250115_1200.csv";
            string actualFileName = Path.GetFileName(_fakeFileWriter.WrittenFilePath);
            Assert.AreEqual(expectedFileName, actualFileName, "File name does not match the expected value.");

            // Verify number of lines (1 header + 24 rows).
            var lines = _fakeFileWriter.WrittenLines;
            Assert.AreEqual(25, lines.Count, "CSV file should contain 25 lines (1 header + 24 data rows).");

            // Verify header.
            Assert.AreEqual("Local Time,Volume", lines[0], "CSV header is incorrect.");

            // For periods 1-11: Trade1 (100) + Trade2 (50) = 150.
            // For periods 12-24: Trade1 (100) + Trade2 (-20) = 80.
            // Period 1 maps to "23:00", Period 2 maps to "00:00", ..., Period 24 maps to "22:00".
            Assert.AreEqual("23:00,150", lines[1], "Period 1 row is incorrect.");
            Assert.AreEqual("00:00,150", lines[2], "Period 2 row is incorrect.");
            Assert.AreEqual("10:00,80", lines[12], "Period 12 row is incorrect.");
            Assert.AreEqual("22:00,80", lines[24], "Period 24 row is incorrect.");
        }

        [TestMethod]
        public async Task RunExtractAsync_RetriesAndFails_WhenServiceThrowsException()
        {
            // Arrange: Configure the IPowerService to always throw an exception.
            _powerServiceMock
                .Setup(ps => ps.GetTradesAsync(It.IsAny<DateTime>()))
                .ThrowsAsync(new PowerServiceException("Simulated error"));

            var worker = new PowerPositionReportWorker(_configuration, _powerServiceMock.Object, _logger, _fakeTimeProvider, _fakeFileWriter);

            // Act
            await worker.RunExtractAsync(CancellationToken.None);

            // Assert: Since extraction fails after retries, no file should be written.
            Assert.IsNull(_fakeFileWriter.WrittenFilePath, "File should not be written when trades cannot be retrieved.");
            Assert.IsNull(_fakeFileWriter.WrittenLines, "No CSV content should be written when trades retrieval fails.");
        }
    }
}
