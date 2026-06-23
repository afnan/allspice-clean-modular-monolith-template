using System.Security.Cryptography;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Invitations.Commands.InviteUser;

/// <summary>
/// Generates a cryptographically-random temporary password that is guaranteed to contain at least one
/// lowercase, uppercase, digit, and special character, so it satisfies a typical Keycloak realm complexity
/// policy. A uniform draw from the full alphabet (the previous approach) could omit a required class and
/// cause intermittent Keycloak rejections.
/// </summary>
public static class TempPasswordGenerator
{
    private const string Lower = "abcdefghjkmnpqrstuvwxyz";   // no l/o/i — avoid look-alikes
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";                 // no 0/1
    private const string Special = "!@#$%";
    private const string All = Lower + Upper + Digits + Special;

    public const int Length = 16;

    public static string Generate()
    {
        var chars = new char[Length];

        // Guarantee one of each required class.
        chars[0] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
        chars[1] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        chars[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        chars[3] = Special[RandomNumberGenerator.GetInt32(Special.Length)];

        for (var i = 4; i < chars.Length; i++)
        {
            chars[i] = All[RandomNumberGenerator.GetInt32(All.Length)];
        }

        // Fisher-Yates shuffle (crypto RNG) so the guaranteed characters aren't fixed in positions 0-3.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
