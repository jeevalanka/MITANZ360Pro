using MITANZ360Pro.Web.Models.Workflow;

namespace MITANZ360Pro.Web.Services.Workflow.Validation;

/// <summary>
/// Validates workflow definitions before saving or execution.
/// </summary>
public sealed class WorkflowValidator
{
    public IReadOnlyList<string> Validate(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var errors = new List<string>();

        // Workflow
        if (string.IsNullOrWhiteSpace(workflow.Name))
        {
            errors.Add("Workflow name is required.");
        }

        if (workflow.DataSources.Count == 0)
        {
            errors.Add("Workflow must contain at least one step.");
        }

        foreach (var step in workflow.DataSources)
        {
            ValidateCommonStep(step, errors);

            switch (step.Type.ToLowerInvariant())
            {
                case "scrape":
                    ValidateRequiredSetting(
                        step,
                        "url",
                        "URL",
                        errors);
                    break;

                case "http":
                    ValidateRequiredSetting(
                        step,
                        "url",
                        "URL",
                        errors);

                    ValidateRequiredSetting(
                        step,
                        "method",
                        "Method",
                        errors);
                    break;

                case "sql":
                    ValidateRequiredSetting(
                        step,
                        "connection",
                        "Connection",
                        errors);

                    ValidateRequiredSetting(
                        step,
                        "query",
                        "SQL Query",
                        errors);
                    break;

                case "file":
                    ValidateRequiredSetting(
                        step,
                        "path",
                        "File Path",
                        errors);
                    break;

                case "email":
                    ValidateRequiredSetting(
                        step,
                        "to",
                        "Recipient",
                        errors);

                    ValidateRequiredSetting(
                        step,
                        "subject",
                        "Subject",
                        errors);

                    ValidateRequiredSetting(
                        step,
                        "body",
                        "Body",
                        errors);
                    break;

                case "sharepoint":
                    ValidateRequiredSetting(step,"siteUrl","Site URL",errors);

                    ValidateRequiredSetting(
                        step,
                        "listName",
                        "List Name",
                        errors);

                    ValidateRequiredSetting(
                        step,
                        "operation",
                        "Operation",
                        errors);
                    break;

                case "ai":
                    ValidateRequiredSetting(
                        step,
                        "provider",
                        "Provider",
                        errors);

                    ValidateRequiredSetting(
                        step,
                        "model",
                        "Model",
                        errors);

                    ValidateRequiredSetting(
                        step,
                        "prompt",
                        "Prompt",
                        errors);
                    break;

                default:
                    errors.Add(
                        $"'{step.Name}' has unsupported plugin type '{step.Type}'.");
                    break;
            }
        }

        return errors;
    }

    private static void ValidateCommonStep(
        WorkflowStep step,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(step.Name))
        {
            errors.Add("Step name is required.");
        }

        if (string.IsNullOrWhiteSpace(step.Type))
        {
            errors.Add($"'{step.Name}' type is required.");
        }

        if (string.IsNullOrWhiteSpace(step.Output))
        {
            errors.Add($"'{step.Name}' output is required.");
        }

        if (step.Order <= 0)
        {
            errors.Add(
                $"'{step.Name}' must have a valid execution order.");
        }
    }

    private static void ValidateRequiredSetting(
        WorkflowStep step,
        string key,
        string displayName,
        List<string> errors)
    {
        if (!step.Settings.TryGetValue(key, out var value) ||
            string.IsNullOrWhiteSpace(value?.ToString()))
        {
            errors.Add(
                $"'{step.Name}' requires {displayName}.");
        }
    }
}