using AvancoChaoFabrica.Api.Infrastructure;
using AvancoChaoFabrica.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AvancoChaoFabrica.Api.Endpoints;

public static class ImportEndpoints
{
    private const long MaximumFileSize = 25 * 1024 * 1024;

    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/imports/preview", async (
            HttpRequest request,
            WorkbookImportService importer,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "Upload a multipart/form-data request with an Excel file." });
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "Select a non-empty .xlsx file." });
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return Results.UnprocessableEntity(new { error = "Only .xlsx workbooks are supported." });
            }

            if (file.Length > MaximumFileSize)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            try
            {
                await using var stream = file.OpenReadStream();
                return Results.Ok(await importer.PreviewAsync(file.FileName, stream, ct));
            }
            catch (Exception exception) when (
                exception is InvalidDataException or ArgumentException or IOException or System.Xml.XmlException)
            {
                return Results.UnprocessableEntity(new { error = exception.Message });
            }
        }).WithTags("Imports");

        app.MapGet("/api/imports", async (
            AppDbContext db,
            CancellationToken ct) =>
            Results.Ok(await db.ImportBatches
                    .AsNoTracking()
                    .OrderByDescending(x => x.ImportedAt)
                    .Select(x => new
                    {
                        x.Id,
                        x.FileName,
                        x.ImportedAt,
                        x.Status,
                        Rows = x.Rows.Count,
                        InvalidRows = x.Rows.Count(row => row.Status == "Invalid"),
                        PossibleDuplicates = x.Rows.Count(row => row.Status == "PossibleDuplicate")
                    })
                .ToListAsync(ct)))
            .WithTags("Imports");
        app.MapGet("/api/imports/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var batch = await db.ImportBatches.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.FileName,
                    x.ImportedAt,
                    x.ImportedBy,
                    x.Status,
                    Rows = x.Rows.OrderBy(row => row.SheetName).ThenBy(row => row.RowNumber)
                        .Select(row => new
                        {
                            row.SheetName,
                            row.RowNumber,
                            row.SourceDataDate,
                            row.SourceRevision,
                            row.Status,
                            row.ValidationMessage,
                            row.RawDataJson
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(ct);
            if (batch is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(batch);
        }).WithTags("Imports");
        return app;
    }
}
