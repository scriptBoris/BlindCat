namespace BlindCatCore.Services;

public interface IConfig
{
    void Write(string key, string? value);
    string? Read(string key);
    Task Save();
    void WriteJSON<T>(string key, T? model);
    T ReadJSON<T>(string key, T defValue);
}