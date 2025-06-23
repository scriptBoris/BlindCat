using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using BlindCatMauiMobile.Tools;

namespace BlindCatMauiMobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private Context _context;
    public static MainActivity Instance { get; private set; }
    public FilePickerService FilePickerService { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Instance = this;
        _context = this;
        FilePickerService = new(this);
        base.OnCreate(savedInstanceState);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (resultCode == Result.Ok && data?.Data != null)
        {
            var uri = data.Data;

            // Выбор папки
            if (requestCode == FilePickerService.CodeFolder)
            {
                string? path = GetRealPath(uri);
                FilePickerService.PassResultDir(path);
            }
            // Выбор файла
            else if (requestCode == FilePickerService.CodeFile)
            {
                string? path = GetRealPath(uri);
                FilePickerService.PassResultFile(path);
            }
        }

        base.OnActivityResult(requestCode, resultCode, data);
    }

    private string? GetRealPath(Android.Net.Uri uri)
    {
        return Crutches.GetActualPathFromFile(uri);
        // Для content:// URI
        // if (DocumentsContract.IsDocumentUri(_context, uri))
        // {
        //     string docId = DocumentsContract.GetDocumentId(uri);
        //     
        //     // Если это файл с внешнего хранилища
        //     if (uri.Authority == "com.android.externalstorage.documents" ||
        //         uri.Authority == "com.android.providers.downloads.documents")
        //     {
        //         string[] split = docId.Split(':');
        //         string type = split[0];
        //         
        //         if ("primary".Equals(type, StringComparison.OrdinalIgnoreCase))
        //         {
        //             return Path.Combine(Android.OS.Environment.ExternalStorageDirectory.Path, split[1]);
        //         }
        //     }
        // }
        // // Для file:// URI
        // else if ("file".Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase))
        // {
        //     return uri.Path;
        // }
        //
        // return null;
    }
}