using Microsoft.Graph.Models;
using MITANZ360Pro.Web.Models;

namespace MITANZ360Pro.Web.Services;

public partial class SharePointService
{
    // =========================================================
    // GET ROLES
    // =========================================================

    public async Task<List<AppRoleModel>>
        GetRolesAsync()
    {
        try
        {
            var response =
                await _graphClient
                    .Sites[SiteId]
                    .Lists[
                        _configuration["SharePoint:Lists:Roles"]!]
                    .Items
                    .GetAsync(request =>
                    {
                        request.QueryParameters
                            .Expand =
                            ["fields"];
                    });

            if (response?.Value == null)
            {
                return [];
            }

            var roles =
                new List<AppRoleModel>();

            foreach (var item in response.Value)
            {
                try
                {
                    var fields =
                        item.Fields?
                            .AdditionalData;

                    if (fields == null)
                    {
                        continue;
                    }

                    var model =
                        new AppRoleModel
                        {
                            // =================================================
                            // ID
                            // =================================================

                            Id =
                                item.Id ?? "",

                            // =================================================
                            // TITLE
                            // =================================================

                            Title =
                                fields.TryGetValue(
                                    "Title",
                                    out var title)
                                        ? title?.ToString()
                                            ?? ""
                                        : "",

                            // =================================================
                            // DESCRIPTION
                            // =================================================

                            Description =
                                fields.TryGetValue(
                                    "field_1",
                                    out var description)
                                        ? description?.ToString()
                                            ?? ""
                                        : "",

                            // =================================================
                            // ACTIVE
                            // =================================================

                            IsActive =
                                fields.TryGetValue(
                                    "field_2",
                                    out var active)
                                &&
                                bool.TryParse(
                                    active?.ToString(),
                                    out var isActive)
                                        ? isActive
                                        : false
                        };

                    roles.Add(model);
                }
                catch (Exception itemEx)
                {
                    _logger.LogWarning(
                        itemEx,
                        "Invalid role item skipped");
                }
            }

            return roles
                .OrderBy(x => x.Title)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error loading roles");

            return [];
        }
    }

    // =========================================================
    // CREATE ROLE
    // =========================================================

    public async Task
        CreateRoleAsync(
            AppRoleModel model)
    {
        try
        {
            var item =
                new ListItem
                {
                    Fields =
                        new FieldValueSet
                        {
                            AdditionalData =
                                new Dictionary<string, object>
                                {
                                    { "Title", model.Title },
                                    { "field_1", model.Description },
                                    { "field_2", model.IsActive }
                                }
                        }
                };

            await _graphClient
                .Sites[SiteId]
                .Lists[
                    _configuration["SharePoint:Lists:Roles"]!]
                .Items
                .PostAsync(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating role");

            throw;
        }
    }

    // =========================================================
    // UPDATE ROLE
    // =========================================================

    public async Task
        UpdateRoleAsync(
            AppRoleModel model)
    {
        try
        {
            var update =
                new FieldValueSet
                {
                    AdditionalData =
                        new Dictionary<string, object>
                        {
                            { "Title", model.Title },
                            { "field_1", model.Description },
                            { "field_2", model.IsActive }
                        }
                };

            await _graphClient
                .Sites[SiteId]
                .Lists[
                    _configuration["SharePoint:Lists:Roles"]!]
                .Items[model.Id]
                .Fields
                .PatchAsync(update);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating role");

            throw;
        }
    }

    // =========================================================
    // DELETE ROLE
    // =========================================================

    public async Task
        DeleteRoleAsync(
            string id)
    {
        try
        {
            await _graphClient
                .Sites[SiteId]
                .Lists[
                    _configuration["SharePoint:Lists:Roles"]!]
                .Items[id]
                .DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting role");

            throw;
        }
    }
}