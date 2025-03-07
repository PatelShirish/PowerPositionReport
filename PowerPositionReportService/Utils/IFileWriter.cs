namespace PowerPositionReportService.Utils
{
    // Abstraction for file-writing operations.
    public interface IFileWriter
    {
        Task WriteAllLinesAsync(string path, IEnumerable<string> lines, CancellationToken cancellationToken);
        void CreateDirectory(string path);
    }
}
