using System.Text.Json;
using BlindCatCore.Services;

namespace BlindCatMauiMobile.Services;

public class Config : IConfig
{
    public void Write(string key, string? value)
    {
        if (value != null)
        {
            Preferences.Set(key, value);
        }
        else
        {
            Preferences.Remove(key);
        }
    }

    public string? Read(string key)
    {
        return Preferences.Get(key, null);
    }

    public Task Save()
    {
        return Task.CompletedTask;
    }

    public void WriteJSON<T>(string key, T? model)
    {
        if (model != null)
        {
            string json = JsonSerializer.Serialize(model);
            Preferences.Set(key, json);
        }
        else
        {
            Preferences.Remove(key);
        }
    }

    public T ReadJSON<T>(string key, T defValue)
    {
        string? json = Preferences.Get(key, null);
        if (json == null)
            return defValue;

        try
        {
            var res = JsonSerializer.Deserialize<T>(json);
            if (res == null)
                return defValue;
            
            return res;
        }
        catch (Exception)
        {
            return defValue;
        }
    }
}