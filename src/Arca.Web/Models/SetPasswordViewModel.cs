using System.ComponentModel.DataAnnotations;

namespace Arca.Web.Models;

public sealed class SetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(10)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
