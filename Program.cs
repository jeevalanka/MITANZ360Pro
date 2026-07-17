using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using MITANZ360Pro.Web.Components;
using MITANZ360Pro.Web.Components.Account;
using MITANZ360Pro.Web.Data;
using MITANZ360Pro.Web.Endpoints;
using MITANZ360Pro.Web.Models;
using MITANZ360Pro.Web.Services;
using MITANZ360Pro.Web.Services.AI;
using MITANZ360Pro.Web.Services.Automation;
using MITANZ360Pro.Web.Services.DocumentProcessing;
using MITANZ360Pro.Web.Services.Templates;
using MITANZ360Pro.Web.Services.Workflow.Engine;
using MITANZ360Pro.Web.Services.Workflow.Plugins;
using MITANZ360Pro.Web.Services.Workflow.Repository;
using MITANZ360Pro.Web.Services.Workflow.Validation;
using OfficeOpenXml;
using Radzen;
using Radzen.Blazor;
using System.Security.Claims;



var builder = WebApplication.CreateBuilder(args);

#region 🔧 CONFIGURATION

builder.Configuration
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile("system-settings.json", false, true)
    .AddEnvironmentVariables();

builder.Services.Configure<ApplicationSettings>(
    builder.Configuration.GetSection("Application"));

#endregion

#region 🪵 LOGGING

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

#endregion

#region 🧱 CORE FRAMEWORK

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

#endregion

#region 📦 APPLICATION SERVICES

builder.Services.AddScoped<UserSessionService>();
builder.Services.AddScoped<UserSessionInitializer>();
builder.Services.AddScoped<IGraphMailService, GraphMailService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<JsonLookupDataService>();

#endregion

#region 📦 User Context SERVICES

builder.Services.AddScoped<IUserContextService, UserContextService>();

#endregion

#region 🎨 UI SERVICES

builder.Services.AddRadzenComponents();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

#endregion

#region 🗄️ DATABASE

    var connectionString =
        builder.Configuration
            .GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' not found.");

    builder.Services.AddDbContextFactory<ApplicationDbContext>(
        options =>
        {
            options.UseSqlServer(
                connectionString,
                sql =>
                {
                    // ✅ Enterprise resiliency (REQUIRED for production)
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });

            // ✅ Development-only diagnostics (SAFE)
            if (builder.Environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
            }
        });

    // ✅ Scoped DbContext for services (REQUIRED pattern)
    builder.Services.AddScoped(sp =>
        sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
          .CreateDbContext());

#endregion

#region 🔐 IDENTITY & AUTHORIZATION
    builder.Services
        .AddIdentityCore<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequiredLength = 3;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

    builder.Services.Configure<IdentityOptions>(options =>
    {
        options.ClaimsIdentity.RoleClaimType = ClaimTypes.Role;
    });

    builder.Services
        .AddAuthentication(IdentityConstants.ApplicationScheme)
        .AddIdentityCookies();

    builder.Services.AddAuthorization();
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddScoped<
        AuthenticationStateProvider,
        IdentityRevalidatingAuthenticationStateProvider>();
    builder.Services.AddScoped<IdentityRedirectManager>();
#endregion

#region 🔥 MICROSOFT GRAPH

builder.Services.AddSingleton<GraphServiceClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var credential = new ClientSecretCredential(
        config["AzureAd:TenantId"],
        config["AzureAd:ClientId"],
        config["AzureAd:ClientSecret"]);

    return new GraphServiceClient(
        credential,
        new[] { "https://graph.microsoft.com/.default" });
});

#endregion

#region 🧠 SHAREPOINT SERVICES

builder.Services.AddScoped<SharePointService>();

#endregion

#region Document Processing

builder.Services.AddMemoryCache();
    builder.Services.AddScoped<
        MITANZ360Pro.Web.Services.DocumentProcessing.IFileStreamingService,
        MITANZ360Pro.Web.Services.DocumentProcessing.FileStreamingService>();

#endregion

#region 🤖 AI & AUTOMATION

builder.Services.AddMitanzAiEngine();

builder.Services.AddScoped<DocumentProcessingService>();
builder.Services.AddScoped<TemplateEngine>();
builder.Services.AddScoped<AutomationService>();

builder.Services.AddHttpClient<AIService>();

#endregion

#region ✅ Workflow Studio
builder.Services.AddScoped<WorkflowRepository>();
builder.Services.AddScoped<WorkflowExecutionService>();
builder.Services.AddScoped<WorkflowPluginManager>();
builder.Services.AddScoped<WorkflowEngine>();
builder.Services.AddScoped<WorkflowExecutionService>();
builder.Services.AddSingleton<WorkflowValidator>();

builder.Services.AddScoped<IWorkflowPlugin, ScrapePlugin>();
builder.Services.AddScoped<IWorkflowPlugin, HttpPlugin>();
builder.Services.AddScoped<IWorkflowPlugin, FilePlugin>();
builder.Services.AddScoped<IWorkflowPlugin, SqlPlugin>();
builder.Services.AddScoped<IWorkflowPlugin, SharePointPlugin>();
builder.Services.AddScoped<IWorkflowPlugin, EmailPlugin>();
builder.Services.AddScoped<IWorkflowPlugin, AiPlugin>();

builder.Services.AddHttpClient<HttpPlugin>();
#endregion

#region 🌐 MVC / API

builder.Services.AddControllersWithViews();

#endregion

#region 📊 THIRD-PARTY

ExcelPackage.License.SetNonCommercialOrganization("MITANZ360");

#endregion

#region 🚀 BUILD APPLICATION

var app = builder.Build();

#endregion

#region ✅ DOCUMENT PROXY API (MINIMAL API)

// Renders a library item in-browser. Office documents are converted to
// PDF server-side and cached (see SharePointService.DocumentViewer.cs);
// PDFs/images/HTML/text are streamed as-is. Permission (Published/Archived
// state vs. caller's role) is validated inside GetRenderableDocumentAsync —
// never trust the query string or client-side state for this decision.

app.MapGet("/api/documents/render/{id}", async (
    string id,
    SharePointService sp,
    HttpContext context) =>
{
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest("Missing document id.");

    try
    {
        var doc = await sp.GetRenderableDocumentAsync(id, context.User, context.RequestAborted);

        context.Response.Headers.ContentDisposition = $"inline; filename=\"{doc.FileName}\"";

        return Results.File(
            doc.Stream,
            doc.ContentType,
            enableRangeProcessing: true);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound();
    }
    catch (NotSupportedException ex)
    {
        return Results.BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Document render failed: {ex.Message}");
    }
})
.RequireAuthorization();

#endregion

#region 🧱 DATABASE INITIALIZATION

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var factory =
        services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAsync(services);
    await EnsureSysAdminAsync(services);
}

#endregion

#region 🌐 HTTP PIPELINE

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

#endregion

#region 🔗 ENDPOINTS

app.MapControllers();

app.MapRegisterEndpoints();
app.MapAdditionalIdentityEndpoints();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

#endregion

app.Run();

#region 🔐 SYSADMIN SEED

static async Task EnsureSysAdminAsync(IServiceProvider services)
{
    var config = services.GetRequiredService<IConfiguration>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    var roleName = config["Security:SysAdminRole"] ?? "SysAdmin";
    var email = config["Security:SysAdminEmail"];

    if (string.IsNullOrWhiteSpace(email))
        return;

    if (!await roleManager.RoleExistsAsync(roleName))
        await roleManager.CreateAsync(new IdentityRole(roleName));

    var user = await userManager.FindByEmailAsync(email);

    if (user != null &&
        !await userManager.IsInRoleAsync(user, roleName))
    {
        await userManager.AddToRoleAsync(user, roleName);
    }
}

#endregion