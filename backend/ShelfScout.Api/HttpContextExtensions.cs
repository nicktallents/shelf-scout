namespace ShelfScout.Api;

public static class HttpContextExtensions
{
    private const string CurrentUserItemKey = "CurrentUser";

    public static void SetCurrentUser(this HttpContext context, CurrentUser user) =>
        context.Items[CurrentUserItemKey] = user;

    public static CurrentUser GetCurrentUser(this HttpContext context) =>
        context.Items[CurrentUserItemKey] as CurrentUser
        ?? throw new InvalidOperationException("Identity middleware did not run for this request.");
}
