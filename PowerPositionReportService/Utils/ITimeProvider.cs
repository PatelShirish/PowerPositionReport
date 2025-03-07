namespace PowerPositionReportService.Utils
{
    // Abstraction for time-related operations.
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
        DateTime Now { get; }
    }
}
