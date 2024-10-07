using BlindCatCore.Services;

namespace BlindCatCore.Extensions;

public static class NumberExt
{
    public static string? Encrypt(this long input, string? password, ICrypto crypto)
    {
        if (string.IsNullOrEmpty(password))
            return input.ToString();

        return crypto.EncryptInt64(input, password);
    }

    public static long? DecryptInt64(this string? chiferText, string? password, ICrypto crypto)
    {
        if (string.IsNullOrEmpty(password))
        {
            if (long.TryParse(chiferText, out long value))
                return value;
            else
                return null;
        }

        if (chiferText == null)
            return null;

        return crypto.DecryptInt64(chiferText, password);
    }
}