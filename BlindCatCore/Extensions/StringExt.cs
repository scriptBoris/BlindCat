using BlindCatCore.Enums;
using BlindCatCore.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Extensions;

public static class StringExt
{
    [return:NotNullIfNotNull(nameof(input))]
    public static string? ResolveEncrypt(this string? input, string? passwordRead, string? passwordWrite, ICrypto crypto)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        string? read;
        string? write;

        if (passwordRead == null)
            read = input;
        else
            read = crypto.DecryptString(input, passwordRead);

        if (passwordWrite == null)
            write = read;
        else
            write = crypto.EncryptString(read, passwordWrite);

        return write;
    }

    public static string Encrypt(this string? input, string? password, ICrypto crypto)
    {
        if (string.IsNullOrEmpty(password))
            return input;

        if (input == null)
            return null;

        if (string.IsNullOrWhiteSpace(input))
            return null;

        return crypto.EncryptString(input, password);
    }

    [return:NotNullIfNotNull(nameof(chiferText))]
    public static string? Decrypt(this string? chiferText, string? password, ICrypto crypto)
    {
        if (string.IsNullOrEmpty(chiferText))
            return null;

        if (string.IsNullOrEmpty(password))
            return chiferText;

        return crypto.DecryptString(chiferText, password);
    }

    public static T TryParseEnum<T>(this string? self, T defaultValue) where T : struct
    {
        if (Enum.TryParse<T>(self, true, out T res))
        {
            return res;
        }
        else
        {
            return defaultValue;
        }
    }
}