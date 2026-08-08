namespace BusinessIdentityServer.Areas.ClientServices.Models.Correspondence;

public class ErrorViewModel
{
    public string? Message { get; set; }

    public ErrorViewModel()
    {}

    public ErrorViewModel(string error)
    {
        Message = error;
    }
}
