using BlindCatCore.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Extensions;

public static class DateTimesExt
{
    [return: NotNullIfNotNull(nameof(input))]
    public static DateTime? DecryptDate(this string? input, string? password, ICrypto crypto)
    {
        if (input == null)
            return null;

        string read;
        if (password != null)
        {
            read = crypto.DecryptString(input, password);
        }
        else
        {
            read = input;
        }

        try
        {
            if (read.Length != 19)
                return null;

            var res = DateTime.ParseExact(read, "yyyy.MM.dd HH:mm:ss", null);
            return res;
        }
        catch (Exception ex)
        {
            //Debug.WriteLine($"ERROR DATE TIME FROM {read}\n{ex}!");
            return null;
        }
    }

    [return: NotNullIfNotNull(nameof(date))]
	public static string? EncryptDate(this DateTime? date, string? password, ICrypto crypto)
    {
        if (date == null)
            return null;

        string str = date.Value.ToString("yyyy.MM.dd HH:mm:ss");

        if (password != null)
        {
            str = crypto.EncryptString(str, password);
        }

        return str;
    }
}