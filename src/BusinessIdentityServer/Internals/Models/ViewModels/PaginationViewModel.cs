namespace BusinessIdentityServer.Internals.Models.ViewModels;

public class PaginationViewModel
{
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public bool IsPaginationRequestMethodGet { get; set; }
    public Func<int, string>? MethodToGeneratePageLink { get; set; }
    public Func<int, string, string>? MethodToGeneratePaginationFormElements { get; set; }

    public string? CSSClassForActiveStatus(int pageNumber)
    {
        return pageNumber == CurrentPage ? "active" : null;
    }

    public string PageLink(int pageNumber)
    {
        return MethodToGeneratePageLink?.Invoke(pageNumber) ?? string.Empty;
    }

    public string PageForm(int pageNumber, string buttonText)
    {
        return MethodToGeneratePaginationFormElements?.Invoke(pageNumber, buttonText) ?? string.Empty;
    }

    public string? PaginationRequestArea { get; set; }
    public string? PaginationRequestController { get; set; }
    public string? PaginationRequestAction { get; set; }
}
