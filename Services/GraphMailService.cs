using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using System.Net;

namespace MITANZ360Pro.Web.Services;

/* ================================
 * INTERFACE
 * ================================ */
public interface IGraphMailService
{
    Task SendMailAsync(GraphMailRequest request);
    Task SendBulkMailAsync(List<GraphMailRequest> requests);
}

/* ================================
 * SERVICE
 * ================================ */
public sealed class GraphMailService : IGraphMailService
{
    private static readonly string[] GraphScopes =
    [
        "https://graph.microsoft.com/.default"
    ];

    private readonly ILogger<GraphMailService> _logger;
    private readonly GraphServiceClient _graphClient;

    private readonly string _senderMailbox;

    public GraphMailService(
        IConfiguration configuration,
        ILogger<GraphMailService> logger)
    {
        _logger = logger;

        var tenantId =
            configuration["Graph:TenantId"]
            ?? throw new Exception("Graph:TenantId missing");

        var clientId =
            configuration["Graph:ClientId"]
            ?? throw new Exception("Graph:ClientId missing");

        var clientSecret =
            configuration["Graph:ClientSecret"]
            ?? throw new Exception("Graph:ClientSecret missing");

        _senderMailbox =
            configuration["EmailSettings:SenderEmail"]
            ?? throw new Exception("EmailSettings:SenderEmail missing");

        var credential =
            new ClientSecretCredential(
                tenantId,
                clientId,
                clientSecret);

        _graphClient =
            new GraphServiceClient(
                credential,
                GraphScopes);

        _logger.LogInformation("GraphMailService initialized");
        _logger.LogInformation("Sender Mailbox: {Mailbox}", _senderMailbox);
    }

    public async Task SendMailAsync(GraphMailRequest request)
    {
        ValidateRequest(request);

        var clientRequestId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("GRAPH MAIL SEND STARTED");
            _logger.LogInformation("CLIENT REQUEST ID: {Id}", clientRequestId);

            var message = BuildMessage(request);

            var body =
                new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                {
                    Message = message,
                    SaveToSentItems = true
                };

            await _graphClient
                .Users[_senderMailbox]
                .SendMail
                .PostAsync(
                    body,
                    cfg =>
                    {
                        cfg.Headers.Add("client-request-id", clientRequestId);
                        cfg.Headers.Add("return-client-request-id", "true");
                    });

            _logger.LogInformation("GRAPH EMAIL SENT SUCCESSFULLY");
        }
        catch (ApiException apiEx)
        {
            _logger.LogError(
                apiEx,
                "GRAPH API ERROR StatusCode={StatusCode} Message={Message}",
                apiEx.ResponseStatusCode,
                apiEx.Message);

            // Common causes:
            // - Missing Mail.Send (Application) permission
            // - Missing admin consent
            // - RAOP scope mismatch
            throw new Exception(
                $"GRAPH MAIL FAILED [{apiEx.ResponseStatusCode}]: {apiEx.Message}",
                apiEx);
        }
    }

    public async Task SendBulkMailAsync(List<GraphMailRequest> requests)
    {
        if (requests == null || requests.Count == 0)
            return;

        foreach (var req in requests)
            await SendMailAsync(req);
    }

    /* ================================
     * HELPERS
     * ================================ */

    private static void ValidateRequest(GraphMailRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ArgumentException("Subject is required.");

        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("Body is required.");

        if (request.To == null || request.To.Count == 0)
            throw new ArgumentException("At least one recipient is required.");

        if (request.To.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Invalid TO address.");

        if (request.Cc.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Invalid CC address.");

        if (request.Bcc.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Invalid BCC address.");
    }

    private static Message BuildMessage(GraphMailRequest request)
    {
        var message = new Message
        {
            Subject = request.Subject,
            Body = new ItemBody
            {
                ContentType =
                    request.IsHtml
                        ? BodyType.Html
                        : BodyType.Text,
                Content = request.Body
            },
            ToRecipients =
                request.To.Select(e =>
                    new Recipient
                    {
                        EmailAddress =
                            new EmailAddress { Address = e }
                    }).ToList()
        };

        if (request.Cc.Count > 0)
        {
            message.CcRecipients =
                request.Cc.Select(e =>
                    new Recipient
                    {
                        EmailAddress =
                            new EmailAddress { Address = e }
                    }).ToList();
        }

        if (request.Bcc.Count > 0)
        {
            message.BccRecipients =
                request.Bcc.Select(e =>
                    new Recipient
                    {
                        EmailAddress =
                            new EmailAddress { Address = e }
                    }).ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.ReplyTo))
        {
            message.ReplyTo =
            [
                new Recipient
                {
                    EmailAddress =
                        new EmailAddress
                        {
                            Address = request.ReplyTo
                        }
                }
            ];
        }

        if (request.Attachments.Count > 0)
        {
            message.Attachments = [];

            foreach (var a in request.Attachments)
            {
                message.Attachments.Add(
                    new FileAttachment
                    {
                        OdataType = "#microsoft.graph.fileAttachment",
                        Name = a.FileName,
                        ContentType = a.ContentType,
                        ContentBytes = a.ContentBytes
                    });
            }
        }

        return message;
    }
}

/* ================================
 * DTOs
 * ================================ */
public sealed class GraphMailRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsHtml { get; set; }

    public List<string> To { get; set; } = [];
    public List<string> Cc { get; set; } = [];
    public List<string> Bcc { get; set; } = [];

    public string? ReplyTo { get; set; }

    public List<GraphMailAttachment> Attachments { get; set; } = [];
}

public sealed class GraphMailAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] ContentBytes { get; set; } = [];
}