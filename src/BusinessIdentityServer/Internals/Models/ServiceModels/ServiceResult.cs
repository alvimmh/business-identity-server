namespace BusinessIdentityServer.Internals.Models.ServiceModels;

// encapsulates the result of a service operation returning either a view model or a redirect route
public readonly record struct ServiceResult<T>(T? ViewModel, string? RedirectRoute)
{
    public bool RequiresRedirect => !string.IsNullOrEmpty(RedirectRoute);

    public static ServiceResult<T> View(T viewModel) => new(viewModel, null);

    public static ServiceResult<T> Redirect(string redirectRoute) => new(default, redirectRoute);
}
