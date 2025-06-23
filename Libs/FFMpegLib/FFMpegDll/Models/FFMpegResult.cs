namespace FFMpegDll.Models;

public readonly struct FFMpegResult
{
    public const int CANCELLED = -1;
    
    public bool IsSuccess => Code == 0;
    public int Code { get; init; }
    public string Message { get; init; }

    public static FFMpegResult Success => new FFMpegResult
    {
        Code = 0,
        Message = "Success",
    };

    public static FFMpegResult Cancelled => new FFMpegResult
    {
        Code = CANCELLED,
        Message = "Cancelled",
    };

    public static FFMpegResult Error(int code, string message)
    {
        return new FFMpegResult
        {
            Code = code,
            Message = message,
        };
    }
}