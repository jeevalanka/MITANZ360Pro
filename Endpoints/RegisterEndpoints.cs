using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MITANZ360Pro.Web.Data;

namespace MITANZ360Pro.Web.Endpoints;

public static class RegisterEndpoints
{
    public static IEndpointRouteBuilder MapRegisterEndpoints(this IEndpointRouteBuilder app)
    {
        // GET safeguard (prevents 415 in browser)
        app.MapGet("/api/register", () => Results.Redirect("/register"));

        // POST from HTML form
        app.MapPost("/api/register", async (
            [FromForm] string Email,
            [FromForm] string Password,
            [FromForm] string ConfirmPassword,
            [FromForm] bool AcceptTerms,
            [FromServices] UserManager<ApplicationUser> userManager
        ) =>
        {
            if (!AcceptTerms)
                return Results.BadRequest("Terms must be accepted.");

            if (Password != ConfirmPassword)
                return Results.BadRequest("Passwords do not match.");

            var existing = await userManager.FindByEmailAsync(Email);
            if (existing != null)
                return Results.BadRequest("Email already exists.");

            var user = new ApplicationUser
            {
                UserName = Email,
                Email = Email
            };

            var result = await userManager.CreateAsync(user, Password);

            if (!result.Succeeded)
                return Results.BadRequest(
                    result.Errors.Select(e => e.Description));

            return Results.Redirect("/xlogin");
        })
        .DisableAntiforgery();

        return app;
    }
}