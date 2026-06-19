namespace Arca.Application.Security;

public interface ICurrentStoreService
{
    Guid? StoreId { get; }
    string? StoreCode { get; }
}
