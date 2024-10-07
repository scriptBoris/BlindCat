using BlindCatCore.Core;
using BlindCatCore.Services;
using CommunityToolkit.Maui.Storage;
using BlindCatMaui.Core;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace BlindCatMaui.Services;

public class ViewPlatforms : IViewPlatforms
{
    public object BuildView(Type? viewType, BaseVm baseVm)
    {
        if (viewType == null)
            return new ContentView
            {
                BackgroundColor = Colors.DarkRed,
                Content = new Label
                {
                    Text = $"For VM {baseVm.GetType().Name} ViewType is NULL",
                    TextColor = Colors.White,
                },
            };

        try
        {
            foreach (var ctor in viewType.GetConstructors())
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length != 1)
                    break;

                var parg = parameters[0];
                if (parg.ParameterType != baseVm.GetType())
                    break;

                var lwview = (BindableObject)RuntimeHelpers.GetUninitializedObject(viewType);
                ctor.Invoke(lwview, [baseVm]);
                lwview.BindingContext = baseVm;
                return lwview;
            }

            var view = (BindableObject)Activator.CreateInstance(viewType)!;
            view.BindingContext = baseVm;
            return view;
        }
        catch (Exception ex)
        {
            return new ContentView
            {
                BackgroundColor = Colors.DarkRed,
                Content = new Label
                {
                    Text = $"Fail create View for VM {baseVm.GetType().Name}\n" +
                        $"{ex.Message}",
                },
            };
        }
    }

    public void InvokeInMainThread(Action act)
    {
        MainThread.BeginInvokeOnMainThread(act);
    }

    public Task ShowDialog(string title, string body, string OK)
    {
        return App.Current!.MainPage!.DisplayAlert(title, body, OK);
    }

    public Task<string?> ShowDialogPromt(string title, string message, string OK, string cancel, string placeholder, string initValue)
    {
        return App.Current!.MainPage!.DisplayPromptAsync(
            title:title, 
            message:message, 
            accept:OK, 
            cancel:cancel, 
            placeholder:placeholder, 
            initialValue: initValue);
    }

    public async Task<string?> ShowDialogPromtPassword(string title, string message, string OK, string cancel, string placeholder)
    {
#if WINDOWS
        var h = App.Current?.MainPage?.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
        var root = h.XamlRoot;
        var dialog = new Platforms.Windows.Native.PromtPasswordDialog(title, message, OK, cancel, placeholder);
        dialog.XamlRoot = root;
        var res = await dialog.ShowAsync();
        if (res == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary || dialog.IsTapeOK)
        {
            return dialog.Password ?? "";
        }
        return null;
#else
    throw new NotImplementedException();
#endif
    }

    public async Task<IFileResult?> SelectMediaFile()
    {
        string[] formats = 
        [
            ".jpeg", 
            ".jpg",
            ".png",
            ".webp",
            ".gif", 
            ".mp4", 
            ".mov", 
            ".webm"
        ];
        var dic = new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, formats },
            { DevicePlatform.Android, formats },
        };
        var res = await FilePicker.PickAsync(new PickOptions
        {
            FileTypes = new FilePickerFileType(dic)
        });

        if (res == null)
            return null;

        var stream = await res.OpenReadAsync();

        var result = new FileResult
        {
            Path = res.FullPath,
            Stream = stream,
        };
        return result;
    }

    public async Task<string?> SelectDirectory()
    {
#if WINDOWS
        string path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var res = await FolderPicker.PickAsync(path, CancellationToken.None);
        if (!res.IsSuccessful)
            return null;

        return res.Folder.Path;
#else
        var res = await FolderPicker.PickAsync(CancellationToken.None);
        if (!res.IsSuccessful)
            return null;

        return res.Folder.Path;
#endif
    }

    public Task<bool> ShowDialog(string title, string body, string OK, string cancel)
    {
        return App.Current!.MainPage!.DisplayAlert(title, body, OK, cancel);
    }

    public Task<string?> ShowDialogSheet(string title, string cancel, params string[] items)
    {
        return App.Current!.MainPage!.DisplayActionSheet(title, cancel, null, items);
    }

    public async Task<int?> ShowDialogSheetId(string title, string cancel, params string[] items)
    {
        for (int i = 0; i < items.Length; i++)
        {
            string item = items[i];
            items[i] = $"{i + 1}. {item}";
        }

        string? selected = await App.Current!.MainPage!.DisplayActionSheet(title, cancel, null, items);
        if (selected == null || selected == cancel)
            return null;

        string numString = selected.Split('.')[0];
        int selectedId = int.Parse(numString) - 1;
        return selectedId;
    }

    public ITimerCore MakeTimer()
    {
        var t = App.Current!.Dispatcher.CreateTimer();
        var tt = new TimerCore(t);
        return tt;
    }

    public void Destroy(object view)
    {
    }

    public void ShowFileOnExplorer(string filePath)
    {
        // Открытие папки в Проводнике Windows
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{filePath}\"",
            UseShellExecute = true
        });
    }

    public class FileResult : IFileResult
    {
        public required string Path { get; set; }
        public required Stream Stream { get; set; }
    }
}