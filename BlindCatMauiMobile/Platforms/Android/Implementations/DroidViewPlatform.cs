using Android.Content;
using Android.OS;
using Android.Provider;
using BlindCatCore.Services;
using BlindCatMauiMobile.Services;
using BlindCatMauiMobile.Tools;
using Environment = Android.OS.Environment;

namespace BlindCatMauiMobile.Implementations;

public class DroidViewPlatform : ViewPlatform
{
    private FilePickerService _picker => MainActivity.Instance.FilePickerService;
    
    public override async Task<IFileResult?> SelectMediaFile(object? hostView)
    {
        var context = Android.App.Application.Context;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            if (Environment.IsExternalStorageManager)
            {
                string file = await _picker.PickFile();
                return new FileResultPick
                {
                    FileName = file,
                    UsePath = true,
                    Path = file,
                    Stream = null,
                    UseStream = false,
                };
            }
            else
            {
                var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission);
                intent.SetData(Android.Net.Uri.Parse($"package:{context.PackageName}"));
                intent.AddFlags(ActivityFlags.NewTask);
                context.StartActivity(intent);
            }
        }
        else
        {
            return await base.SelectMediaFile(hostView);
        }
        
        return null;
    }
    
    
    private class FileResultPick : IFileResult
    {
        public required bool UseStream { get; set; }
        public required bool UsePath { get; set; }
        public required string? Path { get; set; }
        public required Stream? Stream { get; set; }
        public required string FileName { get; set; }
    }
}