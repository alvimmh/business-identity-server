using System.ComponentModel.DataAnnotations;

namespace BusinessIdentityServer.Areas.ClientServices.Models.Correspondence;

public class ResendEmailInputModel
{
    [Required]
    public string ResendEmailRecordId { get; set; } = null!;

    [Required]
    public string Email { get; set; } = null!;

    public string? ResendEmailErrorMessage { get; private set; }

    public void SetErrorMessage(string message)
    {
        ResendEmailErrorMessage = message;
    }
}
