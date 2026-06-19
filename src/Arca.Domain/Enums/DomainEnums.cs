namespace Arca.Domain.Enums;

public enum RoleScope
{
    System,
    Tenant,
    Store
}

public enum ProductStatus
{
    Draft,
    Active,
    Inactive
}

public enum AttributeType
{
    Select,
    MultiSelect,
    Text,
    Number,
    Boolean,
    Date,
    Decimal
}

public enum StorageProvider
{
    Local,
    S3
}

public enum StockMovementType
{
    Purchase,
    Sale,
    Return,
    Adjustment,
    TransferIn,
    TransferOut,
    Loss,
    Production,
    Consumption
}
