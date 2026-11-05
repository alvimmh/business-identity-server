using System.ComponentModel.DataAnnotations;

namespace BusinessIdentityServer.Internals.Models.InputModels;

public class EmailChallengeInputModel
{
    [Required]
    [StringLength(6)]
    public string VerificationCode { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string ResendEmailRecordId { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

