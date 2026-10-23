namespace poker_web_api.Configuration;

/// <summary>
/// Small helpers for reconciling environment-variable configuration with the
/// strongly-typed options, where plain env vars cannot naturally express arrays.
/// </summary>
public static class ConfigurationHelpers
{
    /// <summary>
    /// Supports a convenient comma-separated form for bootstrap admin ids via the
    /// <c>TelegramAuth__BootstrapAdminIds</c> environment variable / configuration key,
    /// e.g. "111111111,222222222". When present and parseable into ids, it overrides
    /// whatever the normal binder produced. The indexed form
    /// (<c>TelegramAuth__BootstrapAdminIds__0</c>) and appsettings JSON arrays continue
    /// to work through the standard binder and are left untouched when no scalar value exists.
    /// </summary>
    public static void ApplyBootstrapAdminIdsOverride(
        IConfiguration configuration,
        TelegramAuthOptions options)
    {
        var raw = configuration[$"{TelegramAuthOptions.SectionName}:BootstrapAdminIds"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var ids = ParseIdList(raw);
        if (ids.Count > 0)
        {
            options.BootstrapAdminIds = ids;
        }
    }

    /// <summary>
    /// Parses a comma (or whitespace) separated list of Telegram numeric ids,
    /// ignoring blanks and non-numeric tokens.
    /// </summary>
    public static List<long> ParseIdList(string raw)
    {
        return raw
            .Split(new[] { ',', ';', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s.Trim(), out var id) ? id : (long?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }
}
