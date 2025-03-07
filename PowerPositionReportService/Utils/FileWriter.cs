namespace PowerPositionReportService.Utils
{

    // Default implementation of IFileWriter using System.IO.
    public class FileWriter : IFileWriter
    {
        public Task WriteAllLinesAsync(string path, IEnumerable<string> lines, CancellationToken cancellationToken)
        {
            return File.WriteAllLinesAsync(path, lines, cancellationToken);
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }
    }
}
