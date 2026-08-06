using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using MITANZ360Pro.Web.Services;
using Radzen;

namespace MITANZ360Pro.Web.Modules.Entities;

public partial class StudentVisa : ComponentBase
{
    [Inject] private IEntityService EntityService { get; set; } = default!;
    [Inject] private IGraphMailService GraphMailService { get; set; } = default!;
    [Inject] private IStudentVisaRateLimiter RateLimiter { get; set; } = default!;
    [Inject] private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private NotificationService Notifications { get; set; } = default!;
    [Inject] private ILogger<StudentVisa> Logger { get; set; } = default!;
    [Inject] private IConfiguration Configuration { get; set; } = default!;

    private static readonly string[] GenderOptions = ["Male", "Female", "Other", "Prefer not to say"];
    private static readonly string[] ContactMethods = ["Email", "Mobile", "WhatsApp"];
    private static readonly string[] EnglishTests = ["IELTS", "PTE", "TOEFL", "Duolingo", "None", "Other"];

    private StudentVisaFormModel _model = new();
    private bool _busy;
    private bool _showConfirm;
    private bool _showSuccess;
    private bool _emailLocked;
    private bool _referralFromQuery;
    private string? _bannerError;
    private string? _bannerInfo;
    private string _savedStudentNumber = "";
    private string _savedStatus = EntityStatuses.Draft;
    private CancellationTokenSource? _emailLookupCts;

    protected override async Task OnInitializedAsync()
    {
        ApplyReferralFromQuery();
        await ResolveReferralPartnerAsync();
    }

    private void ApplyReferralFromQuery()
    {
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        if (!QueryHelpers.ParseQuery(uri.Query).TryGetValue("r", out var values))
        {
            return;
        }

        var raw = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var normalized = ReferralCodeHelper.Normalize(raw);
        if (normalized == null)
        {
            _bannerError = "The referral code in the link is invalid. You can still register without a referral.";
            return;
        }

        _model.ReferralCode = normalized;
        _referralFromQuery = true;
    }

    private async Task ResolveReferralPartnerAsync()
    {
        if (string.IsNullOrWhiteSpace(_model.ReferralCode))
        {
            return;
        }

        try
        {
            var partner = await EntityService.ResolveReferralPartnerAsync(_model.ReferralCode);
            if (partner != null)
            {
                var name = StudentVisaMetadataHelper.GetString(partner.Metadata, "CompanyName");
                _model.ReferralPartner = string.IsNullOrWhiteSpace(name) ? partner.Title : name;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Unable to resolve referral partner for {Code}", _model.ReferralCode);
        }
    }

    private async Task OnEmailChangedAsync()
    {
        _bannerInfo = null;
        _emailLookupCts?.Cancel();
        _emailLookupCts = new CancellationTokenSource();
        var token = _emailLookupCts.Token;

        var email = _model.EmailAddress?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return;
        }

        try
        {
            await Task.Delay(400, token);
            var existing = await EntityService.FindStudentByEmailAsync(email, token);
            if (existing == null)
            {
                _emailLocked = false;
                return;
            }

            var preservedReferral = _model.ReferralCode;
            var preservedPartner = _model.ReferralPartner;
            _model.LoadFromEntity(existing);

            // Key fields: keep existing referral if present; else keep query referral
            if (string.IsNullOrWhiteSpace(_model.ReferralCode) && _referralFromQuery)
            {
                _model.ReferralCode = preservedReferral;
                _model.ReferralPartner = preservedPartner;
            }

            _emailLocked = true;
            _bannerInfo = $"We found an existing student profile ({existing.EntityId}). You can update non-key fields and submit.";
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Email lookup failed");
        }
    }

    private void OnInvalidSubmit()
    {
        _bannerError = "Please correct the highlighted fields and try again.";
        Notifications.Notify(NotificationSeverity.Error, "Validation", "Please correct the highlighted fields.");
    }

    private Task OnValidSubmitAsync()
    {
        _bannerError = null;

        if (!string.IsNullOrWhiteSpace(_model.Website))
        {
            _bannerError = "Unable to submit this form.";
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(_model.ReferralCode) &&
            ReferralCodeHelper.Normalize(_model.ReferralCode) == null)
        {
            _bannerError = "Referral Code format is invalid.";
            return Task.CompletedTask;
        }

        _showConfirm = true;
        return Task.CompletedTask;
    }

    private void CancelConfirm()
    {
        if (_busy)
        {
            return;
        }

        _showConfirm = false;
    }

    private async Task ConfirmAndSaveAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _bannerError = null;
        StateHasChanged();

        try
        {
            var ctx = BuildSaveContext();
            var rateKey = ctx.IpAddress ?? _model.EmailAddress;
            if (!RateLimiter.TryAcquire(rateKey, out var rateError))
            {
                _bannerError = rateError;
                _showConfirm = false;
                Notifications.Notify(NotificationSeverity.Error, "Rate limit", rateError ?? "Too many attempts.");
                return;
            }

            var metadata = _model.ToMetadata();
            var result = await EntityService.UpsertStudentVisaRegistrationAsync(metadata, ctx);

            if (!result.IsSuccess || result.Data == null)
            {
                _bannerError = result.ErrorMessage ?? "Unable to save your application.";
                _showConfirm = false;
                Notifications.Notify(NotificationSeverity.Error, "Save failed", _bannerError);
                return;
            }

            await SendWelcomeEmailAsync(result.Data);

            _savedStudentNumber = result.Data.Entity.EntityId;
            _savedStatus = string.IsNullOrWhiteSpace(result.Data.Entity.Status)
                ? EntityStatuses.Draft
                : result.Data.Entity.Status;
            _showConfirm = false;
            _showSuccess = true;

            Notifications.Notify(
                NotificationSeverity.Success,
                "Submitted",
                result.Data.WasCreated
                    ? "Your student profile has been created."
                    : "Your student profile has been updated.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Student visa save failed");
            _bannerError = "An unexpected error occurred. Please try again later.";
            _showConfirm = false;
            Notifications.Notify(NotificationSeverity.Error, "Error", _bannerError);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task SendWelcomeEmailAsync(StudentVisaSaveResult save)
    {
        var first = StudentVisaMetadataHelper.GetString(save.Entity.Metadata, StudentVisaMetadataKeys.FirstName);
        var last = StudentVisaMetadataHelper.GetString(save.Entity.Metadata, StudentVisaMetadataKeys.LastName);
        var email = StudentVisaMetadataHelper.GetString(save.Entity.Metadata, StudentVisaMetadataKeys.Email);
        var name = $"{first} {last}".Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var body = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;line-height:1.5;color:#1b1b1b">
              <h2 style="color:#107c10">Welcome to MITANZ Student Visa Services</h2>
              <p>Dear {System.Net.WebUtility.HtmlEncode(name)},</p>
              <p>Thank you for registering with MITANZ. Your student profile has been saved.</p>
              <p><strong>Student Number:</strong> {System.Net.WebUtility.HtmlEncode(save.Entity.EntityId)}<br/>
                 <strong>Status:</strong> {System.Net.WebUtility.HtmlEncode(save.Entity.Status)}</p>
              <p>Please confirm your email address by clicking the link below:</p>
              <p><a href="{save.ConfirmationUrl}" style="background:#0078d4;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none;display:inline-block">Confirm email address</a></p>
              <p style="font-size:12px;color:#605e5c">Or copy this link:<br/>{System.Net.WebUtility.HtmlEncode(save.ConfirmationUrl)}</p>
              <p>We will communicate only through official MITANZ channels.</p>
              <p>Kind regards,<br/>MITANZ Student Visa Services</p>
            </div>
            """;

        try
        {
            await GraphMailService.SendMailAsync(new GraphMailRequest
            {
                Subject = "Welcome to MITANZ Student Visa Services",
                Body = body,
                IsHtml = true,
                To = [email]
            });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Welcome email failed for {Email}", email);
            Notifications.Notify(
                NotificationSeverity.Warning,
                "Email",
                "Profile saved, but the confirmation email could not be sent. Please contact support if you do not receive it.");
        }
    }

    private StudentVisaSaveContext BuildSaveContext()
    {
        var http = HttpContextAccessor.HttpContext;
        var request = http?.Request;
        string? baseUrl = null;

        if (request != null)
        {
            baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var configured = Configuration["PublicBaseUrl"];
            baseUrl = string.IsNullOrWhiteSpace(configured)
                ? Navigation.BaseUri.TrimEnd('/')
                : configured.TrimEnd('/');
        }

        return new StudentVisaSaveContext
        {
            IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = request?.Headers.UserAgent.ToString(),
            BaseUrl = baseUrl
        };
    }

    private void ResetForNewRegistration()
    {
        var referral = _referralFromQuery ? _model.ReferralCode : "";
        var partner = _referralFromQuery ? _model.ReferralPartner : "";
        _model = new StudentVisaFormModel
        {
            ReferralCode = referral,
            ReferralPartner = partner
        };
        _showSuccess = false;
        _emailLocked = false;
        _bannerError = null;
        _bannerInfo = null;
        _savedStudentNumber = "";
        _savedStatus = EntityStatuses.Draft;
    }
}
