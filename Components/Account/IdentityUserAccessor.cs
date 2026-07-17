using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace MITANZ360Pro.Web.Components.Account;

public sealed class IdentityUserAccessor
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public IdentityUserAccessor(AuthenticationStateProvider authenticationStateProvider)
        => _authenticationStateProvider = authenticationStateProvider;

    public async Task<ClaimsPrincipal> GetRequiredUserAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User;
    }
}