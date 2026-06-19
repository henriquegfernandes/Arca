using System.Text;
using Arca.Application.Abstractions.Catalog;
using Arca.Application.Abstractions.Inventory;
using Arca.Application.Common;

namespace Arca.Application.Export;

public sealed class CsvExportService(
    ICatalogManagementRepository catalogRepo,
    IInventoryRepository inventoryRepo)
{
    public async Task<Result<byte[]>> ExportProductsAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result<byte[]>.Failure("TenantId is required.");

        var products = await catalogRepo.ListProductsAsync(tenantId,
            new PageRequest(1, 10_000), cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Name,Slug,BaseSku,Barcode,Brand,Status,CreatedAt");
        foreach (var p in products.Items)
        {
            CsvEscape(sb, p.Name); sb.Append(',');
            CsvEscape(sb, p.Slug); sb.Append(',');
            CsvEscape(sb, p.BaseSku); sb.Append(',');
            CsvEscape(sb, p.Barcode); sb.Append(',');
            CsvEscape(sb, p.Brand); sb.Append(',');
            CsvEscape(sb, p.Status); sb.Append(',');
            CsvEscape(sb, p.CreatedAt.ToString("O"));
            sb.AppendLine();
        }

        return Result<byte[]>.Success(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<Result<byte[]>> ExportInventoryAsync(
        Guid tenantId, Guid storeId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || storeId == Guid.Empty)
            return Result<byte[]>.Failure("TenantId and StoreId are required.");

        var balances = await inventoryRepo.ListAllBalancesAsync(tenantId, storeId, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("StockLocation,VariantSku,Quantity,Reserved,Available,MinimumStock,UpdatedAt");

        foreach (var b in balances)
        {
            CsvEscape(sb, b.StockLocationName); sb.Append(',');
            CsvEscape(sb, b.VariantSku); sb.Append(',');
            sb.Append(b.Quantity); sb.Append(',');
            sb.Append(b.ReservedQuantity); sb.Append(',');
            sb.Append(b.AvailableQuantity); sb.Append(',');
            sb.Append(b.MinimumStock); sb.Append(',');
            CsvEscape(sb, b.UpdatedAt?.ToString("O"));
            sb.AppendLine();
        }

        return Result<byte[]>.Success(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    public async Task<Result<byte[]>> ExportMovementsAsync(
        Guid tenantId, Guid storeId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || storeId == Guid.Empty)
            return Result<byte[]>.Failure("TenantId and StoreId are required.");

        var movements = await inventoryRepo.ListMovementsAsync(tenantId, storeId, null, 10_000, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Type,Quantity,UnitCost,Reason,Notes,UserId,CreatedAt");
        foreach (var m in movements)
        {
            CsvEscape(sb, m.Type); sb.Append(',');
            sb.Append(m.Quantity); sb.Append(',');
            sb.Append(m.UnitCost); sb.Append(',');
            CsvEscape(sb, m.Reason); sb.Append(',');
            CsvEscape(sb, m.Notes); sb.Append(',');
            CsvEscape(sb, m.UserId?.ToString()); sb.Append(',');
            CsvEscape(sb, m.CreatedAt.ToString("O"));
            sb.AppendLine();
        }

        return Result<byte[]>.Success(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static void CsvEscape(StringBuilder sb, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            sb.Append('"');
            sb.Append(value.Replace("\"", "\"\""));
            sb.Append('"');
        }
        else
        {
            sb.Append(value);
        }
    }
}
