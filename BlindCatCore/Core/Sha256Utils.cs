using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Core;

public static class Sha256Utils
{
    public static string CalculateSHA256(string input)
    {
        // Преобразуем строку в байтовый массив
        byte[] bytes = Encoding.UTF8.GetBytes(input);

        // Вычисляем хэш-значение SHA256
        byte[] hashBytes = SHA256.HashData(bytes);

        // Преобразуем байтовый массив в строку в шестнадцатеричном формате
        var builder = new StringBuilder();
        foreach (byte b in hashBytes)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }
}
