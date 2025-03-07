namespace PowerPositionReportService.Utils
{
    // Default implementation of ITimeProvider that uses system time.
    public class SystemTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime Now => DateTime.Now;
    }
}
