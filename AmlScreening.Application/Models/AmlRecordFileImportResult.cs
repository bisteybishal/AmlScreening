namespace AmlScreening.Application.Models;

public sealed class AmlRecordFileImportResult
{
    public int TotalRows { get; set; }

    public int ImportedRows { get; set; }

    public List<string> Errors { get; set; } = [];

    public bool Success => Errors.Count == 0;
}