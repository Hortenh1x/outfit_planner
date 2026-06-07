namespace OutfitPlanner.Application.Common;

public static class InputGuard
{
    public static string NormalizeUserId(string userId)
    {
        var normalized = RequireText(userId, "User id");
        if (normalized.Length > 100)
        {
            throw new InvalidOperationException("User id is too long.");
        }

        return normalized;
    }

    public static string RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value.Trim();
    }
}
