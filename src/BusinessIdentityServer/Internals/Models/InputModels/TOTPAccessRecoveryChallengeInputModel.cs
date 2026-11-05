using System.ComponentModel.DataAnnotations;

namespace BusinessIdentityServer.Internals.Models.InputModels;

public class TOTPAccessRecoveryChallengeInputModel
{
    [Required]
    [StringLength(11)]
    public string VerificationCode { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

