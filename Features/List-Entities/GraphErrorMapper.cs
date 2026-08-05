using Microsoft.Graph.Models.ODataErrors;

namespace MITANZ360Pro.Web.Modules.Entities;

public static class GraphErrorMapper
{
    public static string Map(Exception ex)
    {
        var message = ex.Message;

        if (ex is ODataError oDataError)
        {
            message = oDataError.Error?.Message ?? message;
        }

        if (Contains(message, "not indexed", "column does not exist", "field is not indexed"))
        {
            return "SharePoint configuration issue: a required column is not indexed.";
        }

        if (Contains(message, "duplicate", "already exists", "unique", "conflict"))
        {
            return "Entity already exists.";
        }

        if (Contains(message, "access denied", "forbidden", "unauthorized", "403"))
        {
            return "Access denied.";
        }

        if (Contains(message, "timeout", "timed out", "503", "429", "throttl"))
        {
            return "System temporarily unavailable. Please try again.";
        }

        return "An unexpected error occurred. Please try again.";
    }

    private static bool Contains(string message, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (message.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
