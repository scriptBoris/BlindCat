using Android.App;
using Android.Content;

namespace BlindCatMauiMobile.Tools;

public class FilePickerService
{
    public const int CodeFile = 42;
    public const int CodeFolder = 43;
    
    private readonly Android.Content.Context _context;
    private readonly Activity _activity;

    private TaskCompletionSource<string?>? _tcsFile;

    public FilePickerService(Activity activity)
    {
        _context = activity;
        _activity = activity;
    }

    public void PickFolder()
    {
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);
        _activity.StartActivityForResult(intent, CodeFolder); // 42 - произвольный request code
    }

    public async Task<string?> PickFile()
    {
        if (_tcsFile != null)
            _tcsFile.TrySetResult(null);

        _tcsFile = new();
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*"); // Можно указать конкретный тип, например "audio/*"
        intent.PutExtra(Intent.ExtraLocalOnly, true);
        intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);
        _activity.StartActivityForResult(intent, CodeFile); // 43 - другой request code

        string? val = await _tcsFile.Task;
        return val;
    }

    public void PassResultFile(string? path)
    {
        _tcsFile?.TrySetResult(path);
    }

    public void PassResultDir(string? path)
    {
        throw new NotImplementedException();
    }
}