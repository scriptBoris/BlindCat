namespace BlindCatCore.Enums;

public enum MediaFormats
{
    Unknown = -1,
    Png,
    Jpeg,
    Webp,
    Gif,
    Mp4,
    Mov,
    Webm,
    Avi,
    Mkv,
    Flv,
}

public static class ExtMediaFormats
{
    public static bool IsVideo(this MediaFormats self)
    {
        switch (self)
        {
            case MediaFormats.Mp4:
            case MediaFormats.Mov:
            case MediaFormats.Webm:
            case MediaFormats.Avi:
            case MediaFormats.Mkv:
            case MediaFormats.Flv:
                return true;
            default:
                return false;
        }
    }

    public static bool IsPicture(this MediaFormats self)
    {
        switch (self)
        {
            case MediaFormats.Png:
            case MediaFormats.Jpeg:
            case MediaFormats.Webp:
            case MediaFormats.Gif:
                return true;
            default:
                return false;
        }
    }
}
