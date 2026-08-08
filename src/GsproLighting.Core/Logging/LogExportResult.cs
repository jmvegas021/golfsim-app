namespace GsproLighting.Core.Logging;

public sealed record LogExportResult(
    string DestinationPath,
    IReadOnlyList<string> ExportedFileNames);
