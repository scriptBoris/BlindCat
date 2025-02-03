using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BlindCatCore.Services;

namespace BlindCatAvalonia.Tools;

public class FileConfig : IConfig
{
    private bool read;
    private Dictionary<string, object> dic = new();
    private string filePath;
    public FileConfig()
    {
        filePath = Path.Combine(Environment.CurrentDirectory, "conf.ini");
    }

    public Task Save()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        string json = JsonSerializer.Serialize(dic, options);
        File.WriteAllText(filePath, json);
        return Task.CompletedTask;
    }

    public void Write(string key, string? value)
    {
        if (value == null)
        {
            dic.Remove(key);
        }
        else if (!dic.TryAdd(key, value))
        {
            dic[key] = value;
        }
    }


    public void WriteJSON<T>(string key, T? model)
    {
        if (model != null)
        {
            if (!dic.TryAdd(key, model))
                dic[key] = model;
        }
        else
        {
            dic.Remove(key);
        }
    }

    public string? Read(string key)
    {
        if (File.Exists(filePath))
        {
            if (!read)
            {
                string json = File.ReadAllText(filePath);
                dic = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
                read = true;
            }

            if (dic.TryGetValue(key, out var result))
            {
                return result.ToString();
            }
        }
        return null;
    }

    public T ReadJSON<T>(string key, T defValue)
    {
        if (File.Exists(filePath))
        {
            if (!read)
            {
                string json = File.ReadAllText(filePath);
                dic = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
                read = true;
            }

            if (dic.TryGetValue(key, out var result))
            {
                switch (result)
                {
                    case T t:
                        return t;
                    case JsonElement j:
                        return j.Deserialize<T>()!;
                    default:
                        throw new InvalidCastException();
                }
            }
        }
        return defValue;
    }
}