// Assets/_Project/Scripts/Assessment/StudentDirectory/StudentAccessCodes.cs
using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Client half of the student VR access-code contract. MUST stay identical to the
/// server's src/lib/vrAccessCodes.ts (vicsi-dashboard): same alphabet, same
/// normalization, same PBKDF2 parameters as delivered in each directory response.
/// Never log a code, normalized or not.
/// </summary>
public static class StudentAccessCodes
{
    /// <summary>Lookalikes removed: no 0/O, 1/I, L.</summary>
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int Length = 8;
    public const string LookupAlgorithm = "PBKDF2-HMAC-SHA256";

    /// <summary>Uppercase, with whitespace, '-' and '_' removed; null unless the result is exactly 8 alphabet symbols. Lookalikes are rejected, never guessed.</summary>
    public static string Normalize(string typed)
    {
        if (string.IsNullOrEmpty(typed))
        {
            return null;
        }

        var builder = new StringBuilder(Length);
        foreach (char raw in typed)
        {
            if (char.IsWhiteSpace(raw) || raw == '-' || raw == '_')
            {
                continue;
            }
            char c = char.ToUpperInvariant(raw);
            if (Alphabet.IndexOf(c) < 0 || builder.Length >= Length)
            {
                return null;
            }
            builder.Append(c);
        }
        return builder.Length == Length ? builder.ToString() : null;
    }

    /// <summary>Display form, grouped 4-4.</summary>
    public static string Format(string canonical) =>
        canonical != null && canonical.Length == Length ? canonical.Substring(0, 4) + "-" + canonical.Substring(4) : canonical;

    /// <summary>Lowercase hex PBKDF2-HMAC-SHA256(canonical code as UTF-8, salt). ~20k iterations: run off the main thread for interactive use.</summary>
    public static string DeriveLookupHash(string canonicalCode, string saltBase64, int iterations, int keyBytes)
    {
        byte[] salt = Convert.FromBase64String(saltBase64);
        using var pbkdf2 = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(canonicalCode), salt, iterations, HashAlgorithmName.SHA256);
        byte[] key = pbkdf2.GetBytes(keyBytes);
        var hex = new StringBuilder(key.Length * 2);
        foreach (byte b in key)
        {
            hex.Append(b.ToString("x2"));
        }
        return hex.ToString();
    }

    /// <summary>Equal-length hex comparison that doesn't stop at the first differing character.</summary>
    public static bool HashEquals(string a, string b)
    {
        if (a == null || b == null || a.Length != b.Length)
        {
            return false;
        }
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= char.ToLowerInvariant(a[i]) ^ char.ToLowerInvariant(b[i]);
        }
        return diff == 0;
    }
}
