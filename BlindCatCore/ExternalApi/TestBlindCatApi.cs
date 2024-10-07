using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.ExternalApi;

/// <summary>
/// Mock class of BlindCatApi for Unit testings
/// </summary>
public class TestBlindCatApi : IBlindCatApi
{
    LoadingStrDesc? IBlindCatApi.BusyContext { get; set; }
    private readonly HttpLauncher _httpLauncher;

    public TestBlindCatApi()
    {
        _httpLauncher = new HttpLauncher();
    }

    public void BusyAppend(string text)
    {
    }

    public void BusyAppendNewLine(string text)
    {
    }

    public void BusySetBody(string text)
    {
    }

    public Task<AppResponse<string>> GetHtml(string pageUrl, RequestOptions options, CancellationToken cancel)
    {
        return _httpLauncher.GetHtml(pageUrl, options, cancel);
    }

    public Task GoToAlbum(AlbumArgs args)
    {
        return Task.CompletedTask;
    }

    public Task ShowError(string textError)
    {
        return Task.CompletedTask;
    }

    public Task ShowError(AppResponse response)
    {
        return Task.CompletedTask;
    }

    public Task<string?> ShowPromtRequest(string desciption, string placeholder = "", string initValue = "")
    {
        throw new NotSupportedException();
    }

    void IBlindCatApi.Dispose()
    {
        _httpLauncher.Dispose();
    }
}