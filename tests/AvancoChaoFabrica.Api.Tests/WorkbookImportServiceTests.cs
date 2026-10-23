using AvancoChaoFabrica.Api.Infrastructure;
using AvancoChaoFabrica.Api.Services;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AvancoChaoFabrica.Api.Tests;

public sealed class WorkbookImportServiceTests
{
    [Fact]
    public async Task PreviewAsync_PreservesMetadataAndFlagsDuplicateAndFormulaError()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("BANCO DE DADOS");
        sheet.Cell("A1").Value = "DATA_BASE";
        sheet.Cell("B1").Value = "2026-10-22";
        sheet.Cell("A2").Value = "REV:";
        sheet.Cell("B2").Value = "R1";
        sheet.Cell("A3").Value = "SUFORNECEDOR";
        sheet.Cell("B3").Value = "OF";
        sheet.Cell("C3").Value = "DESENHO";
        sheet.Cell("D3").Value = "TAG";
        sheet.Cell("E3").Value = "TIPAGEM";
        sheet.Cell("F3").Value = "TAG";
        sheet.Cell("G3").Value = "DESCRIÇÃO";
        sheet.Cell("H3").Value = "QTD. TOTAL";
        sheet.Cell("I3").Value = "PESO TOTAL (KG)";
        sheet.Cell("J3").Value = "PREP.";
        sheet.Cell("I4").Value = "PESO (KG)";
        sheet.Cell("J4").Value = "0.15";
        AddItem(sheet, 5, "OF-01", "DWG-01", "TAG-01", "1000,00");
        AddItem(sheet, 6, "OF-01", "DWG-01", "TAG-01", "1000,00");
        AddItem(sheet, 7, "OF-01", "DWG-02", "TAG-02", "#REF!");
        var mirrorSheet = workbook.AddWorksheet("BASE ESPELHO");
        SetHeaders(mirrorSheet, 1, "SUFORNECEDOR", "OF", "DESENHO", "TAG", "TIPAGEM",
            "TAG", "DESCRIÇÃO", "QTD. TOTAL", "PESO TOTAL (KG)", "PREP.");
        AddItem(mirrorSheet, 2, "OF-01", "DWG-01", "TAG-01", "1000,00");
        workbook.AddWorksheet("NOTAS").Cell("A1").Value = "Informação sem tabela de dados";

        using var content = new MemoryStream();
        workbook.SaveAs(content);
        content.Position = 0;

        var result = await new WorkbookImportService(db)
            .PreviewAsync("weekly.xlsx", content, CancellationToken.None);

        Assert.Equal(2, result.Sheets.Count);
        var importedSheet = result.Sheets[0];
        Assert.Equal(3, importedSheet.HeaderRow);
        Assert.Equal(5, importedSheet.DataStartRow);
        Assert.Equal("ManufacturingDetail", importedSheet.TableType);
        Assert.Equal("0.15", importedSheet.StageWeights["PREP."]);
        Assert.Equal(3, importedSheet.TotalDataRows);
        Assert.False(importedSheet.IsTruncated);
        Assert.Equal("2026-10-22", importedSheet.SourceDataDate);
        Assert.Equal("R1", importedSheet.SourceRevision);
        Assert.Equal(new[] { "Review", "PossibleDuplicate", "Invalid" },
            importedSheet.Rows.Select(row => row.Status));
        Assert.Contains("#REF!", importedSheet.Rows[2].Message);
        Assert.Equal(4, await db.ImportedRows.CountAsync());
        Assert.Equal("Review", Assert.Single(result.Sheets[1].Rows).Status);
        Assert.Single(result.SkippedSheets);

        content.Position = 0;
        var repeatedPreview = await new WorkbookImportService(db)
            .PreviewAsync("weekly.xlsx", content, CancellationToken.None);
        Assert.Equal(importedSheet.Rows.Select(row => row.Status),
            repeatedPreview.Sheets[0].Rows.Select(row => row.Status));
    }

    [Fact]
    public async Task PreviewAsync_RecognizesSummaryAndMaterialListViews()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        using var workbook = new XLWorkbook();

        var supplierSheet = workbook.AddWorksheet("ANÁLISE GERAL _FORNECEDOR");
        SetHeaders(supplierSheet, 8,
            "Item", "Subfornecedor", "DESCRIÇÃO", "PESO (KG)", "PREP.", "MONT.", "SOLDA",
            "EXPEDIDO", "Saldo em fabricação", "% Avanço");
        supplierSheet.Cell(9, 5).Value = "0.15";
        supplierSheet.Cell(9, 4).Value = "PESO (KG)";
        supplierSheet.Cell(10, 1).Value = "1";
        supplierSheet.Cell(10, 2).Value = "JM";
        supplierSheet.Cell(10, 3).Value = "Estruturas";
        supplierSheet.Cell(10, 4).Value = "1000";

        var orderSheet = workbook.AddWorksheet("ANÁLISE GERAL _OS");
        SetHeaders(orderSheet, 8,
            "ITEM", "FORNECEDOR", "Nº PEDIDO", "OF / OS", "DESCRIÇÃO", "PESO (KG)",
            "PREP.", "MONT.", "SOLDA", "EXPEDIDO");
        orderSheet.Cell(9, 7).Value = "0.15";
        orderSheet.Cell(9, 6).Value = "PESO (KG)";
        orderSheet.Cell(10, 1).Value = "1";
        orderSheet.Cell(10, 2).Value = "JM";
        orderSheet.Cell(10, 3).Value = "PED-1";
        orderSheet.Cell(10, 4).Value = "OF-01";
        orderSheet.Cell(10, 5).Value = "Transportador";
        orderSheet.Cell(10, 6).Value = "1000";

        var shipmentSheet = workbook.AddWorksheet("LX");
        SetHeaders(shipmentSheet, 3,
            "ITEM", "PACOTE", "RT", "CWA", "DISCIPLINA", "CWP",
            "TAG DA REFERÊNCIA", "IDENTICAÇÃO DO MATERIAL", "DESCRIÇÃO COMPLETA",
            "QTDE", "UND", "PESO UNIT(KG)", "PESO TOTAL (KG)", "FORNECEDOR");
        shipmentSheet.Cell(4, 1).Value = "SKU-01";
        shipmentSheet.Cell(4, 2).Value = "PKG-01";
        shipmentSheet.Cell(4, 3).Value = "RT-01";
        shipmentSheet.Cell(4, 7).Value = "TAG-01";
        shipmentSheet.Cell(4, 8).Value = "MAT-01";
        shipmentSheet.Cell(4, 9).Value = "Parafuso";
        shipmentSheet.Cell(4, 10).Value = "2";
        shipmentSheet.Cell(4, 12).Value = "1,25";
        shipmentSheet.Cell(4, 13).Value = "2,50";

        var planningSheet = workbook.AddWorksheet("ANÁLISE GERAL");
        SetHeaders(planningSheet, 1, "Item", "Contrato", "Pacote", "TAG",
            "DESCRIÇÃO", "PESO (KG)", "PREP.", "MONT.", "SOLDA");
        planningSheet.Cell(2, 1).Value = "1";
        planningSheet.Cell(2, 3).Value = "PKG-01";
        planningSheet.Cell(2, 4).Value = "TAG-01";
        planningSheet.Cell(2, 5).Value = "Estrutura";
        planningSheet.Cell(2, 6).Value = "1000";

        using var content = new MemoryStream();
        workbook.SaveAs(content);
        content.Position = 0;

        var result = await new WorkbookImportService(db)
            .PreviewAsync("views.xlsx", content, CancellationToken.None);

        Assert.Equal(4, result.Sheets.Count);
        Assert.Equal("SupplierSummary", result.Sheets[0].TableType);
        Assert.Equal(10, result.Sheets[0].DataStartRow);
        Assert.Equal("OrderSummary", result.Sheets[1].TableType);
        Assert.Equal(10, result.Sheets[1].DataStartRow);
        Assert.Equal("ShipmentList", result.Sheets[2].TableType);
        Assert.Equal("PlanningView", result.Sheets[3].TableType);
        Assert.Single(result.Sheets[0].Rows);
        Assert.Single(result.Sheets[1].Rows);
        Assert.Single(result.Sheets[2].Rows);
        Assert.Empty(result.SkippedSheets);
    }

    [Fact]
    public async Task PreviewAsync_ReportsTruncatedSampleAndTotalRows()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("BANCO DE DADOS");
        SetHeaders(sheet, 1, "FORNECEDOR", "OF", "DESENHO", "TAG", "DESCRIÇÃO",
            "QTD. TOTAL", "PESO TOTAL (KG)", "PREP.");
        for (var row = 2; row <= 206; row++)
        {
            sheet.Cell(row, 1).Value = "JM";
            sheet.Cell(row, 2).Value = "OF-01";
            sheet.Cell(row, 3).Value = $"DWG-{row}";
            sheet.Cell(row, 4).Value = $"TAG-{row}";
            sheet.Cell(row, 5).Value = "Estrutura";
            sheet.Cell(row, 6).Value = "1";
            sheet.Cell(row, 7).Value = "10";
        }

        using var content = new MemoryStream();
        workbook.SaveAs(content);
        content.Position = 0;

        var result = await new WorkbookImportService(db)
            .PreviewAsync("large.xlsx", content, CancellationToken.None);

        var sheetPreview = Assert.Single(result.Sheets);
        Assert.Equal(205, sheetPreview.TotalDataRows);
        Assert.True(sheetPreview.IsTruncated);
        Assert.Equal(200, sheetPreview.Rows.Count);
        Assert.Equal(200, await db.ImportedRows.CountAsync());
    }

    private static void SetHeaders(IXLWorksheet sheet, int row, params string[] headers)
    {
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(row, column + 1).Value = headers[column];
        }
    }

    private static void AddItem(
        IXLWorksheet sheet,
        int row,
        string order,
        string drawing,
        string tag,
        string weight)
    {
        sheet.Cell(row, 1).Value = "JM";
        sheet.Cell(row, 2).Value = order;
        sheet.Cell(row, 3).Value = drawing;
        sheet.Cell(row, 4).Value = tag;
        sheet.Cell(row, 6).Value = tag;
        sheet.Cell(row, 7).Value = "Estrutura metálica";
        sheet.Cell(row, 8).Value = "2";
        sheet.Cell(row, 9).Value = weight;
    }
}
