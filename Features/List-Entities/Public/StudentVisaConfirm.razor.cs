using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace MITANZ360Pro.Web.Modules.Entities;

public partial class StudentVisaConfirm : ComponentBase
{
    [Inject] private IEntityService EntityService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;
    [Inject] private ILogger<StudentVisaConfirm> Logger { get; set; } = default!;

    private bool _loading = true;
    private bool _success;
    private string? _errorMessage;
    private string _studentNumber = "";

    protected override async Task OnInitializedAsync()
    {
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        QueryHelpers.ParseQuery(uri.Query).TryGetValue("token", out var tokenValues);
        var token = tokenValues.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token))
        {
            _loading = false;
            _errorMessage = "Confirmation token is missing.";
            return;
        }

        try
        {
            var http = HttpContextAccessor.HttpContext;
            var context = new StudentVisaSaveContext
            {
                IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = http?.Request.Headers.UserAgent.ToString()
            };

            var result = await EntityService.VerifyStudentEmailAsync(token, context);
            if (!result.IsSuccess || result.Data == null)
            {
                _errorMessage = result.ErrorMessage ?? "This confirmation link is invalid or has already been used.";
            }
            else
            {
                _success = true;
                _studentNumber = result.Data.EntityId;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Email confirmation failed");
            _errorMessage = "An unexpected error occurred while verifying your email.";
        }
        finally
        {
            _loading = false;
        }
    }
}
