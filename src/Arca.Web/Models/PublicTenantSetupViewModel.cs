using System.ComponentModel.DataAnnotations;

namespace Arca.Web.Models;

public sealed class PublicTenantSetupViewModel
{
    [Required]
    public string CompanyName { get; set; } = string.Empty;

    public string LegalName { get; set; } = string.Empty;
    public string Document { get; set; } = string.Empty;

    [Required]
    public string Slug { get; set; } = string.Empty;

    [EmailAddress]
    public string CompanyEmail { get; set; } = string.Empty;

    public string CompanyPhone { get; set; } = string.Empty;
    public string MainSegment { get; set; } = string.Empty;
    public string Currency { get; set; } = "BRL";
    public string TimeZone { get; set; } = "America/Sao_Paulo";
    public string DefaultLanguage { get; set; } = "pt-BR";
    public bool AllowMultipleStores { get; set; } = true;
    public bool AllowBatchControl { get; set; }
    public bool AllowExpirationControl { get; set; }
    public bool AllowStoreSpecificPricing { get; set; } = true;

    [Required]
    public string StoreName { get; set; } = string.Empty;

    [Required]
    public string StoreCode { get; set; } = "MAIN";

    public string StoreDocument { get; set; } = string.Empty;

    [EmailAddress]
    public string StoreEmail { get; set; } = string.Empty;

    public string StorePhone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string StoreType { get; set; } = "Physical";

    [Required]
    public string AdminFullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string AdminEmail { get; set; } = string.Empty;

    public string AdminPhone { get; set; } = string.Empty;
    public string CatalogTemplate { get; set; } = "Custom";
}
