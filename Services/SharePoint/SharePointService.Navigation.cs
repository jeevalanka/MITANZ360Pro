using Microsoft.Graph.Models;
using MITANZ360Pro.Web.Models;

namespace MITANZ360Pro.Web.Services;

public partial class SharePointService
{
    // =========================================================
    // GET NAVIGATION CARDS
    // =========================================================

    public async Task<List<NavigationCardModel>>
        GetNavigationCardsAsync()
        {
            var cards = new List<NavigationCardModel>();

            try
            {
                var listId =_configuration["SharePoint:Lists:AppNavCards"];

                var response = await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items
                .GetAsync(config =>
                {
                    config.QueryParameters.Expand =
                    [
                        "fields"
                    ];
                });

                if (response?.Value == null)
                {
                    return cards;
                }

                foreach (var item in response.Value)
                {
                    var fields =
                        item.Fields?.AdditionalData;

                    if (fields == null)
                    {
                        continue;
                    }

                    cards.Add(
                        new NavigationCardModel
                        {
                            Id = item.Id ?? "",
                            Title = GetString(fields, "Title"),
                            Subtitle = GetString(fields, "field_1"),
                            Icon = GetString(fields, "field_2"),
                            ImageUrl = GetString(fields, "field_3"),
                            Url = GetString(fields, "field_4"),
                            OpenType = GetString(fields, "field_5"),
                            Role = GetString(fields, "field_6"),
                            Campus = GetString(fields, "field_7"),
                            IsEnabled = GetBool(fields, "field_8"),
                            SortOrder = GetInt(fields, "field_9"),
                            CardColor = GetString(fields, "field_10")
                        });
                }

                return cards
                    .OrderBy(x => x.SortOrder)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading AppNavCards");

                return [];
            }
        }

    // =========================================================
    // CREATE
    // =========================================================

    public async Task
        CreateNavigationCardAsync(
            NavigationCardModel model)
    {
        try
        {
            var listId =
                _configuration[
                    "SharePoint:Lists:AppNavCards"
                ];

            var item =
                new ListItem
                {
                    Fields =
                        new FieldValueSet
                        {
                            AdditionalData =
                            new Dictionary<string, object>
                            {
                                {
                                    "Title",
                                    model.Title ?? ""
                                },

                                {
                                    "field_1",
                                    model.Subtitle ?? ""
                                },

                                {
                                    "field_2",
                                    model.Icon ?? ""
                                },

                                {
                                    "field_3",
                                    model.ImageUrl ?? ""
                                },

                                {
                                    "field_4",
                                    model.Url ?? ""
                                },

                                // =====================================================
                                // OPENTYPE (CHOICE FIELD)
                                // MUST BE STRING
                                // =====================================================

                                {
                                    "field_5",
                                    model.OpenType?.ToString() ?? "Internal"
                                },

                                // =====================================================
                                // ROLE
                                // =====================================================

                                {
                                    "field_6",
                                    model.Role ?? ""
                                },

                                // =====================================================
                                // CAMPUS
                                // =====================================================

                                {
                                    "field_7",
                                    model.Campus ?? ""
                                },

                                // =====================================================
                                // BOOL
                                // =====================================================

                                {
                                    "field_8",
                                    model.IsEnabled
                                },

                                // =====================================================
                                // NUMBER
                                // =====================================================

                                {
                                    "field_9",
                                    Convert.ToInt32(
                                        model.SortOrder)
                                },

                                // =====================================================
                                // COLOR
                                // =====================================================

                                {
                                    "field_10",
                                    model.CardColor ?? ""
                                }
                            }
                        }
                };

            await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items
                .PostAsync(item);

            _logger.LogInformation(
                "Navigation card created: {title}",
                model.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating AppNavCard");

            throw;
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    public async Task
        UpdateNavigationCardAsync(
            NavigationCardModel model)
    {
        try
        {
            var listId =
                _configuration[
                    "SharePoint:Lists:AppNavCards"
                ];

            var fields =
                new FieldValueSet
                {
                    AdditionalData =
                        new Dictionary<string, object>
                        {
                            { "Title", model.Title },
                            { "field_1", model.Subtitle },
                            { "field_2", model.Icon },
                            { "field_3", model.ImageUrl },
                            { "field_4", model.Url },
                            { "field_5", model.OpenType },
                            { "field_6", model.Role },
                            { "field_7", model.Campus },
                            { "field_8", model.IsEnabled },
                            { "field_9", model.SortOrder },
                            { "field_10", model.CardColor }
                        }
                };

            await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items[model.Id]
                .Fields
                .PatchAsync(fields);

            _logger.LogInformation(
                "Navigation card updated: {title}",
                model.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating AppNavCard");

            throw;
        }
    }

    // =========================================================
    // DELETE
    // =========================================================

    public async Task
        DeleteNavigationCardAsync(
            string itemId)
    {
        try
        {
            var listId =
                _configuration[
                    "SharePoint:Lists:AppNavCards"
                ];

            await _graphClient
                .Sites[SiteId]
                .Lists[listId]
                .Items[itemId]
                .DeleteAsync();

            _logger.LogInformation(
                "Navigation card deleted: {id}",
                itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting AppNavCard");

            throw;
        }
    }


    // =========================================================
    // GET CAMPUSES
    // =========================================================

    public async Task<List<string>>
        GetCampusesAsync()
    {
        return
        [
            "Auckland",
            "Wellington",
            "Christchurch",
            "Online"
        ];
    }

    // =========================================================
    // SAFE STRING
    // =========================================================

    private static string GetString(
        IDictionary<string, object> fields,
        string key)
    {
        return fields.TryGetValue(
            key,
            out var value)
            ? value?.ToString() ?? ""
            : "";
    }

    // =========================================================
    // SAFE BOOL
    // =========================================================

    private static bool GetBool(
        IDictionary<string, object> fields,
        string key)
    {
        if (!fields.TryGetValue(
            key,
            out var value))
        {
            return false;
        }

        var text =
            value?.ToString();

        return
            text == "1"
            || text?.Equals(
                "true",
                StringComparison.OrdinalIgnoreCase) == true;
    }

    // =========================================================
    // SAFE INT
    // =========================================================

    private static int GetInt(
        IDictionary<string, object> fields,
        string key)
    {
        if (!fields.TryGetValue(
            key,
            out var value))
        {
            return 0;
        }

        return int.TryParse(
            value?.ToString(),
            out var result)
                ? result
                : 0;
    }
}