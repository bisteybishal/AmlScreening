using System.Globalization;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface.Services;
using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Data;
using ClosedXML.Excel;
using CsvHelper;

namespace AmlScreening.Infrastructure.Services;

public sealed class AmlRecordFileImportService : IAmlRecordFileImportService
{
    private const int MaxRows = 1000;

    private readonly IAmlRecordRepository _repository;
    private readonly AmlScreeningDbContext _db;


    public AmlRecordFileImportService(
        IAmlRecordRepository repository,
        AmlScreeningDbContext db)
    {
        _repository = repository;
        _db = db;
    }


    // =====================================================
    // MAIN IMPORT
    // =====================================================

    public async Task<AmlRecordFileImportResult> ImportAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var extension =
            Path.GetExtension(fileName)
                .ToLowerInvariant();


        List<ImportRow> rows;

        if (extension == ".csv")
        {
            rows = ReadCsv(stream);
        }
        else if (extension == ".xlsx")
        {
            rows = ReadExcel(stream);
        }
        else
        {
            throw new InvalidOperationException(
                "Only CSV and XLSX files are supported.");
        }


        var result =
            new AmlRecordFileImportResult
            {
                TotalRows = rows.Count
            };


        if (rows.Count == 0)
        {
            result.Errors.Add(
                "The file contains no AML records.");

            return result;
        }


        if (rows.Count > MaxRows)
        {
            result.Errors.Add(
                $"Maximum {MaxRows} records are allowed.");

            return result;
        }


        var records =
            new List<AmlRecord>();


        // Validate all rows first
        foreach (var row in rows)
        {
            try
            {
                var record =
                    BuildRecord(row);

                records.Add(record);
            }
            catch (Exception ex)
            {
                result.Errors.Add(
                    $"Row {row.RowNumber}: {ex.Message}");
            }
        }


        // Do not import anything if any row has error
        if (result.Errors.Count > 0)
        {
            return result;
        }


        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                cancellationToken);


        try
        {
            foreach (var record in records)
            {
                await _repository.CreateAsync(
                    record,
                    cancellationToken);
            }


            await transaction.CommitAsync(
                cancellationToken);


            result.ImportedRows =
                records.Count;


            return result;
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }


    // =====================================================
    // CSV
    // =====================================================

    private static List<ImportRow> ReadCsv(
        Stream stream)
    {
        using var reader =
            new StreamReader(stream);

        using var csv =
            new CsvReader(
                reader,
                CultureInfo.InvariantCulture);


        if (!csv.Read())
        {
            return [];
        }


        csv.ReadHeader();


        var headers =
            BuildHeaderMap(
                csv.HeaderRecord ?? []);


        ValidateRequiredHeaders(
            headers);


        var rows =
            new List<ImportRow>();


        var rowNumber = 1;


        while (csv.Read())
        {
            rowNumber++;


            var row =
                new ImportRow
                {
                    RowNumber =
                        rowNumber,

                    RecordType =
                        GetCsvValue(
                            csv,
                            headers,
                            "RecordType",
                            "Record Type",
                            "Type"),

                    FullName =
                        GetCsvValue(
                            csv,
                            headers,
                            "FullName",
                            "Full Name",
                            "Name"),

                    Aliases =
                        GetCsvValue(
                            csv,
                            headers,
                            "Aliases",
                            "Alias"),

                    DateOfBirth =
                        GetCsvValue(
                            csv,
                            headers,
                            "DateOfBirth",
                            "Date Of Birth",
                            "DOB"),

                    Country =
                        GetCsvValue(
                            csv,
                            headers,
                            "Country"),

                    Gender =
                            GetCsvValue(
                                csv,
                                headers,
                                "Gender",
                                "Sex"),

                    Nationality =
                        GetCsvValue(
                            csv,
                            headers,
                            "Nationality"),

                    Occupation =
                        GetCsvValue(
                            csv,
                            headers,
                            "Occupation"),

                    Position =
                        GetCsvValue(
                            csv,
                            headers,
                            "Position",
                            "Designation"),

                    PoliticalParties =
                        GetCsvValue(
                            csv,
                            headers,
                            "PoliticalParties",
                            "Political Parties",
                            "Political Party"),

                    SocialMediaLinks =
                        GetCsvValue(
                            csv,
                            headers,
                            "SocialMediaLinks",
                            "Social Media Links",
                            "Social Media"),

                    Description =
                        GetCsvValue(
                            csv,
                            headers,
                            "Description"),

                    SourceName =
                        GetCsvValue(
                            csv,
                            headers,
                            "SourceName",
                            "Source Name",
                            "Source"),

                    Remarks =
                        GetCsvValue(
                            csv,
                            headers,
                            "Remarks",
                            "Remark")
                };


            // Ignore completely blank rows
            if (IsEmpty(row))
            {
                continue;
            }


            rows.Add(row);
        }


        return rows;
    }


    // =====================================================
    // EXCEL
    // =====================================================

    private static List<ImportRow> ReadExcel(
        Stream stream)
    {
        using var workbook =
            new XLWorkbook(stream);


        var sheet =
            workbook.Worksheet(1);


        var headers =
            BuildExcelHeaderMap(
                sheet.Row(1));


        ValidateRequiredHeaders(
            headers);


        var rows =
            new List<ImportRow>();


        foreach (var excelRow in
                 sheet.RowsUsed().Skip(1))
        {
            var row =
                new ImportRow
                {
                    RowNumber =
                        excelRow.RowNumber(),

                    RecordType =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "RecordType",
                            "Record Type",
                            "Type"),

                    FullName =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "FullName",
                            "Full Name",
                            "Name"),

                    Aliases =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "Aliases",
                            "Alias"),

                    DateOfBirth =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "DateOfBirth",
                            "Date Of Birth",
                            "DOB"),

                    Country =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "Country"),

                    Gender =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "Gender",
                            "Sex"),

                    Nationality =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "Nationality"),

                    Occupation =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "Occupation"),

                    Position =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "Position",
                            "Designation"),

                    PoliticalParties =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "PoliticalParties",
                            "Political Parties",
                            "Political Party"),

                    SocialMediaLinks =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "SocialMediaLinks",
                            "Social Media Links",
                            "Social Media"),

                    Description =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "Description"),

                    SourceName =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "SourceName",
                            "Source Name",
                            "Source"),

                    Remarks =
                        GetExcelValue(
                            excelRow,
                            headers,
                            "Remarks",
                            "Remark")
                };


            // Ignore completely blank rows
            if (IsEmpty(row))
            {
                continue;
            }


            rows.Add(row);
        }


        return rows;
    }


    // =====================================================
    // CREATE AML RECORD
    // =====================================================

    private static AmlRecord BuildRecord(
        ImportRow row)
    {
        var sourceName =
            Required(
                row.SourceName,
                "SourceName");


        var dateOfBirth =
            ParseDate(
                row.DateOfBirth);


        if (dateOfBirth.HasValue &&
            dateOfBirth.Value.Date > DateTime.UtcNow.Date)
        {
            throw new InvalidOperationException(
                "DateOfBirth cannot be in the future.");
        }


        return new AmlRecord
        {
            RecordType =
                NormalizeRecordType(
                    row.RecordType),

            FullName =
                Required(
                    row.FullName,
                    "FullName"),

            Aliases =
                Clean(
                    row.Aliases),

            DateOfBirth =
                dateOfBirth,
            Gender =
    Clean(
        row.Gender),

            Country =
                Required(
                    row.Country,
                    "Country"),

            Nationality =
                Clean(
                    row.Nationality),

            Occupation =
                Clean(
                    row.Occupation),

            Position =
                Clean(
                    row.Position),

            PoliticalParties =
                Clean(
                    row.PoliticalParties),

            SocialMediaLinks =
                Clean(
                    row.SocialMediaLinks),

            Description =
                Clean(
                    row.Description),

            SourceName =
                sourceName,

            Remarks =
                Clean(
                    row.Remarks),

            // AUTO
            ReferenceNo =
                GenerateReferenceNo(
                    sourceName),

            // AUTO
            IsActive =
                true
        };
    }


    // =====================================================
    // RECORD TYPE
    // =====================================================

    private static string NormalizeRecordType(
        string? value)
    {
        var type =
            value?.Trim();


        if (string.Equals(
                type,
                "Individual",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Individual";
        }


        if (string.Equals(
                type,
                "Entity",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Entity";
        }


        if (string.Equals(
                type,
                "Vessel",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Vessel";
        }


        throw new InvalidOperationException(
            "RecordType must be Individual, Entity or Vessel.");
    }


    // =====================================================
    // DATE
    // =====================================================

    private static DateTime? ParseDate(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }


        string[] formats =
        [
            "dd/MM/yyyy",
            "yyyy-MM-dd",
            "dd-MM-yyyy"
        ];


        if (DateTime.TryParseExact(
                value.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date.Date;
        }


        throw new InvalidOperationException(
            $"DateOfBirth '{value}' is invalid.");
    }


    // =====================================================
    // REQUIRED VALUE
    // =====================================================

    private static string Required(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{fieldName} is required.");
        }


        return value.Trim();
    }


    // =====================================================
    // CLEAN VALUE
    // =====================================================

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }


    // =====================================================
    // REFERENCE NUMBER
    // =====================================================

    private static string GenerateReferenceNo(
        string sourceName)
    {
        var source =
            sourceName
                .Trim()
                .Replace(" ", "_")
                .ToUpperInvariant();


        var timestamp =
            DateTime.UtcNow
                .ToString(
                    "yyyyMMddHHmmssfff");


        var unique =
            Guid.NewGuid()
                .ToString("N")[..6]
                .ToUpperInvariant();


        return
            $"{source}-{timestamp}-{unique}";
    }


    // =====================================================
    // BUILD CSV HEADER MAP
    // =====================================================

    private static Dictionary<string, string>
        BuildHeaderMap(
            IEnumerable<string> headers)
    {
        var result =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);


        foreach (var header in headers)
        {
            var normalized =
                NormalizeHeader(
                    header);


            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }


            if (result.ContainsKey(normalized))
            {
                throw new InvalidOperationException(
                    $"Duplicate column '{header}' found.");
            }


            result[normalized] =
                header;
        }


        return result;
    }


    // =====================================================
    // BUILD EXCEL HEADER MAP
    // =====================================================

    private static Dictionary<string, int>
        BuildExcelHeaderMap(
            IXLRow headerRow)
    {
        var result =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);


        foreach (var cell in
                 headerRow.CellsUsed())
        {
            var header =
                cell.GetString();


            var normalized =
                NormalizeHeader(
                    header);


            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }


            if (result.ContainsKey(normalized))
            {
                throw new InvalidOperationException(
                    $"Duplicate column '{header}' found.");
            }


            result[normalized] =
                cell.Address.ColumnNumber;
        }


        return result;
    }


    // =====================================================
    // GET CSV VALUE
    // =====================================================

    private static string? GetCsvValue(
        CsvReader csv,
        Dictionary<string, string> headers,
        params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var normalized =
                NormalizeHeader(
                    name);


            if (!headers.TryGetValue(
                    normalized,
                    out var actualHeader))
            {
                continue;
            }


            if (csv.TryGetField<string>(
                    actualHeader,
                    out var value))
            {
                return Clean(value);
            }
        }


        return null;
    }


    // =====================================================
    // GET EXCEL VALUE
    // =====================================================

    private static string? GetExcelValue(
        IXLRow row,
        Dictionary<string, int> headers,
        params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var normalized =
                NormalizeHeader(
                    name);


            if (!headers.TryGetValue(
                    normalized,
                    out var columnNumber))
            {
                continue;
            }


            return Clean(
                row.Cell(columnNumber)
                    .GetFormattedString());
        }


        return null;
    }


    // =====================================================
    // NORMALIZE HEADER
    // =====================================================

    private static string NormalizeHeader(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }


        return value
            .Trim()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .ToUpperInvariant();
    }


    // =====================================================
    // REQUIRED HEADERS - CSV
    // =====================================================

    private static void ValidateRequiredHeaders(
        Dictionary<string, string> headers)
    {
        ValidateRequiredHeader(
            headers.Keys,
            "RecordType",
            "Record Type",
            "Type");


        ValidateRequiredHeader(
            headers.Keys,
            "FullName",
            "Full Name",
            "Name");


        ValidateRequiredHeader(
            headers.Keys,
            "Country");


        ValidateRequiredHeader(
            headers.Keys,
            "SourceName",
            "Source Name",
            "Source");
    }


    // =====================================================
    // REQUIRED HEADERS - EXCEL
    // =====================================================

    private static void ValidateRequiredHeaders(
        Dictionary<string, int> headers)
    {
        ValidateRequiredHeader(
            headers.Keys,
            "RecordType",
            "Record Type",
            "Type");


        ValidateRequiredHeader(
            headers.Keys,
            "FullName",
            "Full Name",
            "Name");


        ValidateRequiredHeader(
            headers.Keys,
            "Country");


        ValidateRequiredHeader(
            headers.Keys,
            "SourceName",
            "Source Name",
            "Source");
    }


    // =====================================================
    // VALIDATE ONE HEADER
    // =====================================================

    private static void ValidateRequiredHeader(
        IEnumerable<string> headers,
        params string[] possibleNames)
    {
        var available =
            headers.ToHashSet(
                StringComparer.OrdinalIgnoreCase);


        foreach (var name in possibleNames)
        {
            if (available.Contains(
                    NormalizeHeader(name)))
            {
                return;
            }
        }


        throw new InvalidOperationException(
            $"Required column '{possibleNames[0]}' is missing.");
    }


    // =====================================================
    // EMPTY ROW
    // =====================================================

    private static bool IsEmpty(
        ImportRow row)
    {
        return
            string.IsNullOrWhiteSpace(row.RecordType) &&
            string.IsNullOrWhiteSpace(row.FullName) &&
            string.IsNullOrWhiteSpace(row.Aliases) &&
            string.IsNullOrWhiteSpace(row.DateOfBirth) &&
            string.IsNullOrWhiteSpace(row.Country) &&
            string.IsNullOrWhiteSpace(row.Gender) &&
            string.IsNullOrWhiteSpace(row.Nationality) &&
            string.IsNullOrWhiteSpace(row.Occupation) &&
            string.IsNullOrWhiteSpace(row.Position) &&
            string.IsNullOrWhiteSpace(row.PoliticalParties) &&
            string.IsNullOrWhiteSpace(row.SocialMediaLinks) &&
            string.IsNullOrWhiteSpace(row.Description) &&
            string.IsNullOrWhiteSpace(row.SourceName) &&
            string.IsNullOrWhiteSpace(row.Remarks);
    }


    // =====================================================
    // IMPORT ROW
    // =====================================================

    private sealed class ImportRow
    {
        public int RowNumber { get; set; }

        public string? RecordType { get; set; }

        public string? FullName { get; set; }

        public string? Aliases { get; set; }

        public string? DateOfBirth { get; set; }
        public string? Gender { get; set; }

        public string? Country { get; set; }

        public string? Nationality { get; set; }

        public string? Occupation { get; set; }

        public string? Position { get; set; }

        public string? PoliticalParties { get; set; }

        public string? SocialMediaLinks { get; set; }

        public string? Description { get; set; }

        public string? SourceName { get; set; }

        public string? Remarks { get; set; }
    }
}