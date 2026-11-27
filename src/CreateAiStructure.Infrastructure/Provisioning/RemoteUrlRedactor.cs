using System.Text;
using System.Text.RegularExpressions;

namespace CreateAiStructure.Infrastructure.Provisioning;

/// <summary>
/// Makes a raw Git remote value safe to echo in diagnostics: masks URL userinfo (which may carry tokens such as
/// <c>x-access-token:…</c>) and removes control characters (ANSI escapes, newlines, bells).
/// </summary>
internal static class RemoteUrlRedactor
{
    private const string RedactedUserInfo = "//***@";

    private static readonly Regex UserInfo = new(
        @"//[^/@\s]*@",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>Redacts userinfo after every <c>//</c> and strips control characters.</summary>
    /// <param name="value">The raw remote value.</param>
    /// <returns>The redacted value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static string Redact(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var masked = UserInfo.Replace(value, RedactedUserInfo);
        var builder = new StringBuilder(masked.Length);
        foreach (var character in masked)
        {
            if (!char.IsControl(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
