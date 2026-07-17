using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using MITANZ360Pro.Web.Models;

namespace MITANZ360Pro.Web.Services;

public class UserSessionInitializer
{
    private readonly AuthenticationStateProvider _authProvider;

    private readonly UserSessionService _userSession;

    public UserSessionInitializer(AuthenticationStateProvider authProvider,UserSessionService userSession)
    {
        _authProvider = authProvider;
        _userSession = userSession;
    }

    public async Task InitializeAsync()
    {
        var authState = await _authProvider.GetAuthenticationStateAsync();

        var user = authState.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        _userSession.SetUser(new UserSessionModel
        {
            FullName = user.Identity?.Name ?? "",

            Email = user.FindFirst("preferred_username")?.Value
                    ?? user.FindFirst("email")?.Value
                    ?? user.FindFirst(ClaimTypes.Email)?.Value
                    ?? user.Identity?.Name
                    ?? "",

            Role = user.FindFirst("roles")?.Value ??
                    user.FindFirst(ClaimTypes.Role)?.Value ??
                    "Student",

            IsAuthenticated = true
        });
    }
}