using System.Globalization;
using System.Text;
using System.Text.Json;
using AvancoChaoFabrica.Api.Contracts;
using AvancoChaoFabrica.Api.Domain;
using AvancoChaoFabrica.Api.Infrastructure;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AvancoChaoFabrica.Api.Services;

public sealed class WorkbookImportService(AppDbContext db)
{
    private const int MaximumPreviewRows = 200;
    private const int MaximumHeaderSearchRow = 40;

    private sealed record TableHeader(int RowNumber, string TableType);

    public async Task<ImportPreviewResponse> PreviewAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(content);
        var batch = new ImportBatch { FileName = Path.GetFileName(fileName) };
        var sheets = new List<ImportSheetPreview>();
        var skippedSheets = new List<SkippedSheetPreview>();

        foreach (var worksheet in workbook.Worksheets)
        {
            var usedRange = worksheet.RangeUsed();
            if (usedRange is null)
            {
                continue;
            }

            var lastRow = usedRange.RangeAddress.LastAddress.RowNumber;
            var lastColumn = usedRange.RangeAddress.LastAddress.ColumnNumber;
            var header = FindHeader(worksheet, Math.Min(lastRow, MaximumHeaderSearchRow), lastColumn);
            if (header is null)
            {
                skippedSheets.Add(new SkippedSheetPreview(
                    worksheet.Name,
                    "No recognized manufacturing, order, supplier-summary, planning, or shipping table header was found."));
                continue;
            }

            var headers = ReadHeaders(worksheet, header.RowNumber, lastColumn);
            var dataStartRow = FindDataStartRow(worksheet, header.RowNumber, lastRow, headers);
            var stageWeights = ReadStageWeights(worksheet, header.RowNumber, dataStartRow, headers);
            var metadata = ReadMetadata(worksheet, header.RowNumber, lastColumn);
            var previews = new List<ImportRowPreview>();
            var identitiesInSheet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var totalDataRows = 0;

            for (var rowNumber = dataStartRow; rowNumber <= lastRow; rowNumber++)
            {
                if (!HasData(worksheet, rowNumber, headers.Count))
                {
                    continue;
                }

                totalDataRows++;
                if (previews.Count >= MaximumPreviewRows)
                {
                    continue;
                }

                var values = ReadRow(worksheet, rowNumber, headers);
                var message = ValidateRow(values);
                var identity = BuildCandidateIdentity(values, header.TableType);
                var status = message is null ? "Review" : "Invalid";
                if (identity is not null && !identitiesInSheet.Add(identity))
                {
                    const string duplicateWarning =
                        "Possible duplicate within this worksheet; verify the source row. Repeated items in other views or preview batches are not treated as duplicates.";
                    if (status == "Invalid")
                    {
                        message = $"{message} {duplicateWarning}";
                    }
                    else
                    {
                        status = "PossibleDuplicate";
                        message = duplicateWarning;
                    }
                }

                var rawJson = JsonSerializer.Serialize(values);
                batch.Rows.Add(new ImportedRow
                {
                    SheetName = worksheet.Name,
                    RowNumber = rowNumber,
                    SourceDataDate = metadata.DataDate,
                    SourceRevision = metadata.Revision,
                    RawDataJson = rawJson,
                    Status = status,
                    ValidationMessage = message
                });
                previews.Add(new ImportRowPreview(rowNumber, status, message, values));
            }

            sheets.Add(new ImportSheetPreview(
                worksheet.Name,
                header.RowNumber,
                dataStartRow,
                header.TableType,
                metadata.DataDate,
                metadata.Revision,
                headers,
                stageWeights,
                totalDataRows,
                totalDataRows > previews.Count,
                previews));
        }

        if (sheets.Count == 0)
        {
            throw new InvalidDataException(
                "No importable manufacturing, order, supplier-summary, planning, or shipping table was found. " +
                $"Sheets inspected: {string.Join(", ", skippedSheets.Select(x => x.SheetName).Take(10))}.");
        }

        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);
        return new ImportPreviewResponse(batch.Id, batch.FileName, batch.ImportedAt, sheets, skippedSheets);
    }

    private static TableHeader? FindHeader(IXLWorksheet worksheet, int maxRow, int maxColumn)
    {
        for (var rowNumber = 1; rowNumber <= maxRow; rowNumber++)
        {
            var headers = ReadHeaders(worksheet, rowNumber, maxColumn);
            var tableType = ClassifyTable(headers);
            if (tableType is not null)
            {
                return new TableHeader(rowNumber, tableType);
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadHeaders(IXLWorksheet worksheet, int headerRow, int lastColumn)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headers = new List<string>(lastColumn);
        for (var column = 1; column <= lastColumn; column++)
        {
            var cell = worksheet.Cell(headerRow, column);
            var label = cell.GetString().Trim();
            if (label.Length == 0 || decimal.TryParse(
                    label,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out _))
            {
                label = $"COLUMN_{column}";
            }

            var key = Normalize(label);
            counts.TryGetValue(key, out var count);
            count++;
            counts[key] = count;
            headers.Add(count == 1 ? label : $"{label}#{count}");
        }

        return headers;
    }

    private static string? ClassifyTable(IReadOnlyList<string> headers)
    {
        var normalized = headers.Select(Normalize).ToArray();
        var hasDescription = normalized.Any(x => x.Contains("DESCRICAO", StringComparison.Ordinal));
        var hasWeight = normalized.Any(x => x.StartsWith("PESO", StringComparison.Ordinal));
        var hasQuantity = normalized.Any(x =>
            x.StartsWith("QTD", StringComparison.Ordinal) ||
            x.StartsWith("QTDE", StringComparison.Ordinal) ||
            x.StartsWith("QUANTIDADE", StringComparison.Ordinal));
        var hasTag = normalized.Any(x => x.StartsWith("TAG", StringComparison.Ordinal));
        var hasSku = normalized.Any(x =>
            x == "SKU" ||
            x.Contains("CODIGO DO MATERIAL", StringComparison.Ordinal) ||
            x.Contains("IDENTICACAO DO MATERIAL", StringComparison.Ordinal) ||
            x.Contains("IDENTIFICACAO DO MATERIAL", StringComparison.Ordinal));
        var hasDrawing = normalized.Any(x => x.Contains("DESENHO", StringComparison.Ordinal));
        var hasSupplier = normalized.Any(x =>
            x.Contains("SUBFORNECEDOR", StringComparison.Ordinal) ||
            x.Contains("SUB FORNECEDOR", StringComparison.Ordinal) ||
            x.Contains("SUFORNECEDOR", StringComparison.Ordinal) ||
            x == "FORNECEDOR");
        var hasPackage = normalized.Any(x => x.StartsWith("PACOTE", StringComparison.Ordinal));
        var hasOrder = normalized.Any(x =>
            x is "OF" or "OS" or "OF OS" or "N PEDIDO" ||
            x.StartsWith("ORDEM", StringComparison.Ordinal));
        var stageCount = normalized.Count(IsStageHeader);
        var hasProgress = normalized.Any(x => x.Contains("AVANCO", StringComparison.Ordinal));

        if (hasSku && hasDescription && hasQuantity && hasWeight &&
            (hasTag || hasPackage || normalized.Any(x => x == "RT")))
        {
            return "ShipmentList";
        }

        if (hasDrawing && hasTag && hasDescription && hasSupplier && hasOrder &&
            (hasQuantity || hasWeight) && stageCount > 0)
        {
            return "ManufacturingDetail";
        }

        if (hasOrder && hasWeight && stageCount >= 2 &&
            (normalized.Any(x => x == "ITEM") || hasDescription))
        {
            return "OrderSummary";
        }

        if (hasSupplier && hasWeight && (hasProgress || stageCount >= 2))
        {
            return "SupplierSummary";
        }

        if (hasPackage && hasTag && hasDescription && hasWeight && stageCount >= 2)
        {
            return "PlanningView";
        }

        return null;
    }

    private static bool IsStageHeader(string header) =>
        header.StartsWith("PREP", StringComparison.Ordinal) ||
        header.StartsWith("MONT", StringComparison.Ordinal) ||
        header.StartsWith("SOLDA", StringComparison.Ordinal) ||
        header.StartsWith("ACAB", StringComparison.Ordinal) ||
        header.StartsWith("PINT", StringComparison.Ordinal) ||
        header.StartsWith("EXPED", StringComparison.Ordinal) ||
        header.StartsWith("EMBAL", StringComparison.Ordinal) ||
        header.StartsWith("RECEBIMENTO", StringComparison.Ordinal) ||
        header.StartsWith("PRE MONT", StringComparison.Ordinal);

    private static int FindDataStartRow(
        IXLWorksheet worksheet,
        int headerRow,
        int lastRow,
        IReadOnlyList<string> headers)
    {
        for (var rowNumber = headerRow + 1; rowNumber <= Math.Min(headerRow + 3, lastRow); rowNumber++)
        {
            var weightMarkerFound = Enumerable.Range(1, headers.Count)
                .Select(column => Normalize(worksheet.Cell(rowNumber, column).GetString()))
                .Any(value => value is "PESO KG" or "PESO");
            if (weightMarkerFound)
            {
                return rowNumber + 1;
            }
        }

        return headerRow + 1;
    }

    private static IReadOnlyDictionary<string, string?> ReadStageWeights(
        IXLWorksheet worksheet,
        int headerRow,
        int dataStartRow,
        IReadOnlyList<string> headers)
    {
        if (dataStartRow <= headerRow + 1)
        {
            return new Dictionary<string, string?>();
        }

        var weightsRow = dataStartRow - 1;
        var weights = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= headers.Count; column++)
        {
            if (!IsStageHeader(Normalize(headers[column - 1])))
            {
                continue;
            }

            var value = worksheet.Cell(weightsRow, column).GetFormattedString().Trim();
            if (value.Length > 0)
            {
                weights[headers[column - 1]] = value;
            }
        }

        return weights;
    }

    private static bool HasData(IXLWorksheet worksheet, int rowNumber, int columnCount)
    {
        for (var column = 1; column <= columnCount; column++)
        {
            if (!string.IsNullOrWhiteSpace(worksheet.Cell(rowNumber, column).GetFormattedString()))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, string?> ReadRow(
        IXLWorksheet worksheet,
        int rowNumber,
        IReadOnlyList<string> headers)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= headers.Count; column++)
        {
            var cell = worksheet.Cell(rowNumber, column);
            var text = cell.GetFormattedString().Trim();
            values[headers[column - 1]] = text.Length == 0 ? null : text;
        }

        return values;
    }

    private static string? ValidateRow(IReadOnlyDictionary<string, string?> values)
    {
        var formulaErrors = new[]
            { "#REF!", "#VALUE!", "#DIV/0!", "#NAME?", "#N/A", "#NUM!", "#NULL!", "#SPILL!", "#CALC!" };
        var formulaError = values.Values
            .Where(value => value is not null)
            .SelectMany(value => formulaErrors.Where(error =>
                value!.Contains(error, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault();
        if (formulaError is not null)
        {
            return $"Spreadsheet contains formula error {formulaError}; do not treat it as a valid value or zero.";
        }

        foreach (var (header, value) in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalizedHeader = Normalize(header);
            var isQuantity = normalizedHeader.StartsWith("QTD", StringComparison.Ordinal) ||
                             normalizedHeader.StartsWith("QTDE", StringComparison.Ordinal) ||
                             normalizedHeader.StartsWith("QUANTIDADE", StringComparison.Ordinal);
            var isWeight = normalizedHeader.StartsWith("PESO", StringComparison.Ordinal);
            var isPercentage = normalizedHeader.Contains("AVANCO", StringComparison.Ordinal) ||
                               normalizedHeader.Contains("PERCENTUAL", StringComparison.Ordinal) ||
                               normalizedHeader.Contains("PERCENTAGEM", StringComparison.Ordinal) ||
                               normalizedHeader.Contains("PERCENT", StringComparison.Ordinal) ||
                               normalizedHeader.EndsWith(" %", StringComparison.Ordinal);
            var isDate = normalizedHeader.StartsWith("DATA", StringComparison.Ordinal) ||
                         normalizedHeader.Contains("PRAZO", StringComparison.Ordinal) ||
                         normalizedHeader.Contains("PREVISAO", StringComparison.Ordinal) ||
                         normalizedHeader.Contains("EMBARQUE", StringComparison.Ordinal);

            if (isPercentage)
            {
                var percentageText = value.Trim().TrimEnd('%').Trim();
                if (!TryParseNumber(percentageText, out var percentage))
                {
                    return $"{header} is not a valid percentage.";
                }

                if (percentage is < 0m or > 100m)
                {
                    return $"{header} must be between 0 and 100%.";
                }

                continue;
            }

            if (isDate)
            {
                if (!DateTime.TryParse(value, CultureInfo.GetCultureInfo("pt-BR"),
                        DateTimeStyles.AllowWhiteSpaces, out _) &&
                    !DateTime.TryParse(value, CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces, out _))
                {
                    return $"{header} is not a valid date.";
                }

                continue;
            }

            if (!isQuantity && !isWeight)
            {
                continue;
            }

            if (!TryParseNumber(value, out var numericValue))
            {
                return $"{header} is not a valid number.";
            }

            if (numericValue < 0)
            {
                return $"{header} cannot be negative.";
            }
        }

        return null;
    }

    private static string? BuildCandidateIdentity(
        IReadOnlyDictionary<string, string?> values,
        string tableType)
    {
        if (tableType == "ManufacturingDetail")
        {
            var order = FindValue(
                values,
                "OF",
                "OS",
                "OF / OS",
                "ORDEM DE FABRICACAO",
                "ORDEM FABRICACAO");
            var drawing = FindValue(values, "DESENHO", "DESENHO DA VALE");
            var tags = FindValues(values, key => Normalize(key).StartsWith("TAG", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (string.IsNullOrWhiteSpace(order) || string.IsNullOrWhiteSpace(drawing) || tags.Length == 0)
            {
                return null;
            }

            return string.Join("|", new[]
            {
                order.Trim(),
                FindValue(
                    values,
                    "SUBFORNECEDOR",
                    "SUB FORNECEDOR",
                    "SUFORNECEDOR",
                    "FORNECEDOR")?.Trim() ?? "",
                FindValue(values, "PACOTE", "PACOTE DE CONTRATACAO")?.Trim() ?? "",
                drawing.Trim()
            }.Concat(tags));
        }

        if (tableType == "ShipmentList")
        {
            var material = FindValue(
                values,
                "SKU",
                "CODIGO DO MATERIAL",
                "IDENTICACAO DO MATERIAL",
                "IDENTIFICACAO DO MATERIAL");
            if (string.IsNullOrWhiteSpace(material))
            {
                return null;
            }

            return string.Join("|",
                FindValue(values, "PACOTE", "PACOTE DE CONTRATACAO")?.Trim() ?? "",
                FindValue(values, "RT")?.Trim() ?? "",
                FindValue(values, "CWP")?.Trim() ?? "",
                material.Trim());
        }

        return null;
    }

    private static (string? DataDate, string? Revision) ReadMetadata(
        IXLWorksheet worksheet,
        int headerRow,
        int lastColumn)
    {
        string? dataDate = null;
        string? revision = null;
        for (var row = 1; row < headerRow && row <= 12; row++)
        {
            for (var column = 1; column < lastColumn; column++)
            {
                var label = Normalize(worksheet.Cell(row, column).GetString());
                if (dataDate is null && (label.Contains("DATA BASE", StringComparison.Ordinal) ||
                                         label == "DATA"))
                {
                    dataDate = worksheet.Cell(row, column + 1).GetFormattedString().Trim();
                }
                else if (revision is null && label.StartsWith("REV", StringComparison.Ordinal))
                {
                    revision = worksheet.Cell(row, column + 1).GetFormattedString().Trim();
                }
            }
        }

        return (dataDate, revision);
    }

    private static string? FindValue(IReadOnlyDictionary<string, string?> values, params string[] aliases)
    {
        var normalizedAliases = aliases.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return values.FirstOrDefault(x => normalizedAliases.Contains(Normalize(x.Key))).Value;
    }

    private static IEnumerable<string> FindValues(
        IReadOnlyDictionary<string, string?> values,
        Func<string, bool> headerPredicate) =>
        values.Where(x => headerPredicate(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.Value!.Trim());

    private static bool TryParseNumber(string value, out decimal number)
    {
        var normalized = value.Replace(" ", "", StringComparison.Ordinal)
            .Replace("\u00a0", "", StringComparison.Ordinal);
        var lastComma = normalized.LastIndexOf(',');
        var lastDot = normalized.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            var decimalSeparator = lastComma > lastDot ? ',' : '.';
            var thousandsSeparator = decimalSeparator == ',' ? '.' : ',';
            normalized = normalized.Replace(thousandsSeparator.ToString(), "", StringComparison.Ordinal)
                .Replace(decimalSeparator, '.');
        }
        else if (lastComma >= 0)
        {
            normalized = normalized.Replace(",", ".", StringComparison.Ordinal);
        }
        else if (normalized.Count(character => character == '.') > 1)
        {
            var decimalPoint = normalized.LastIndexOf('.');
            normalized = normalized[..decimalPoint].Replace(".", "", StringComparison.Ordinal) +
                         normalized[decimalPoint..];
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        var normalized = new StringBuilder(builder.Length);
        foreach (var character in builder.ToString())
        {
            normalized.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(' ', normalized.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
