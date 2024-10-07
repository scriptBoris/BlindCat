using BlindCatCore.Core;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Channels;
using System.Threading;

namespace BlindCatCore.Services;

public interface IHttpLauncher : IDisposable
{
    Task<AppResponse<Stream>> GetStream(string url, CancellationToken cancelationToken);
    Task<AppResponse<byte[]>> GetBin(string url, Action<int>? progress, CancellationToken cancellationToken);
    Task<AppResponse<string>> GetHtml(string url, RequestOptions options, CancellationToken cancellationToken);
}

public class RequestOptions
{
    public IEnumerable<KeyValuePair<string, string>>? Headers { get; set; }
}

public class HttpLauncher : IHttpLauncher
{
    private readonly HttpClient _httpClient;
    private readonly List<CancellationTokenSource> _tokens = new();
    private readonly CancellationToken _disposeCancel = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    public HttpLauncher()
    {
        var handler = new HttpClientHandlerLogger();
        _httpClient = new HttpClient(handler);
    }

    public async Task<AppResponse<string>> GetHtml(string url, RequestOptions options, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_lock)
        {
            _tokens.Add(cts);
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        if (options.Headers != null)
        {
            try
            {
                foreach (var item in options.Headers)
                {
                    req.Headers.Add(item.Key, item.Value);
                }
            }
            catch (Exception ex)
            {
                return AppResponse.Error("Bad HTTP headers", 88817, ex);
            }
        }

        //req.Headers.CookieContainer = new CookieContainer();
        //handler.CookieContainer.Add(new Cookie
        //{
        //    Name = "cf_clearance",
        //    Value = "6LTntYupGqSZVrPzu94yas0czZZlEvy4FsnSKoccNRI-1725800879-1.2.1.1-YUVoUk5ApVtez07sLmMrVL4y_vpqsp_0EYFPwkh_HPkvHJNGLe5x5ngk4ZPHexhJr.SiaPHTH7SOilfqoUjH6_dfWts23EQVwnsRG464Ym8h1sX447re258cDRc2fcS7czxkti_5Cebw6JBmvi_Ccy.IGUziyNJE36qBs67zauhGjuDvt1JCff3gwsECIrFluzHBX1pQtHkizEbC8C0ITv9czIz18NTTM5cXwnmuG.1GIf07zWl1Sr4yO4WrgNTN6ZQyEvsqjViNhlwymPY7ghvQHlGboZGVEufoT3j5qH1xs3FAZlTyOs1ovL.yU2dcItzvaNGA75lGCffCbjxd3iQ5pWDmV78RZ3UgbW90NjqbLphWjprn80hQhvP1X18OBNVgEe5FTeFWvBauh26TSUfYgQqvhqODBTAxya.IT0CU9GUWRUvEHhyIHZLwEhtq",
        //    Domain = ".zzup.com",
        //    Path = "/",
        //    HttpOnly = true,
        //    Secure = true,
        //    SameSite = SameSite
        //    //new Cookie("cf_clearance", "FS_INtUV8ObsdEamIsdrhIeHHu367LN85oaMmKq_kbI-1725798980-1.2.1.1-I6oZEvox6pe.awR3eGcV1dTGmtT4m1aEnkcX.UoRiwj3egcDzMrIgBIKCVv45pYKrKL.G1qhQUGKr3SbbfzTH9bAd4Y6nK_6_4z5a2xmvrKcv4yqjMmrJF90UWiTpBJ.Ll2d1rT9IkztoE871cIzN1aPkwuIEgI6sLpOA9n3Pve3znTcIBC_YfEYs8L32GDL1UK1_QNdRx94T3mGcXZkLy0xdlEfrdRGeUfxUEINkO80P.qC91fWjt_2iQN97rlhj_D79oAFj0wmuCzW5N38Z1FumsHOWs9JUAkttDRAcb6wTEf63.56_BS0meKNPhnI0T66zSH3S9KzK_yZHAFxG4RvObAOszYk_g7QgjGP5.dXSrGamk7K.tbtXh.Rh15LjlPh506ZeZV45dlP6fMZMLyt3Il_fTAacSUOv6kOkfyki3Jxod8gvCRMTt0lmtcU; Path=/; Expires=Mon, 08-Sep-25 12:42:05 GMT; Domain=.zzup.com; HttpOnly; Secure; SameSite=None; Partitioned"));
        //});


        try
        {
            using var res = await _httpClient.SendAsync(req, cts.Token);
            if (res.IsSuccessStatusCode)
            {
                string str = await res.Content.ReadAsStringAsync(cts.Token);
                return AppResponse.Result(str);
            }
            return AppResponse.Error($"Bad HTTP{res.StatusCode}", 10);
        }
        catch when (cts.Token.IsCancellationRequested)
        {
            return AppResponse.Canceled;
        }
        catch (Exception ex)
        {
            return AppResponse.Error("NET", 22, ex);
        }
        finally
        {
            lock (_lock)
            {
                _tokens.Remove(cts);
            }
        }
    }

    public async Task<AppResponse<byte[]>> GetBin(string url, Action<int>? progress, CancellationToken cancelationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelationToken);
        lock (_lock)
        {
            _tokens.Add(cts);
        }

        try
        {
            using var res = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (res.IsSuccessStatusCode)
            {
                using var bodyStream = await res.Content.ReadAsStreamAsync(cts.Token);
                int totalRead = 0;
                byte[] buffer = new byte[1024];
                using var mem = new MemoryStream();

                while (true)
                {
                    int read = await bodyStream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                    if (read == 0)
                    {
                        break;
                    }
                    else
                    {
                        mem.Write(buffer, 0, read);
                        totalRead += read;
                        progress?.Invoke(totalRead);
                    }
                }

                mem.Position = 0;
                byte[] arr = mem.ToArray();
                return AppResponse.Result(arr);
            }
            return AppResponse.Error($"Bad HTTP{res.StatusCode}", 10);
        }
        catch when (cts.IsCancellationRequested)
        {
            return AppResponse.Canceled;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return AppResponse.Error("NET", 22, ex);
        }
        finally
        {
            lock (_lock)
            {
                _tokens.Remove(cts);
            }
        }
    }

    public async Task<AppResponse<Stream>> GetStream(string url, CancellationToken cancelationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelationToken);
        lock (_lock)
        {
            _tokens.Add(cts);
        }

        HttpResponseMessage? res = null;
        try
        {
            res = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (res.IsSuccessStatusCode)
            {
                var str = await res.Content.ReadAsStreamAsync(cts.Token);
                return AppResponse.Result(str);
            }
            return AppResponse.Error($"Bad HTTP{res.StatusCode}", 10);
        }
        catch when(cts.Token.IsCancellationRequested)
        {
            return AppResponse.Canceled;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return AppResponse.Error("NET", 22, ex);
        }
        finally
        {
            lock (_lock)
            {
                _tokens.Remove(cts);
            }

            // dispose only if canceled (for saving body stream for success http)
            if (res != null && cts.Token.IsCancellationRequested)
                res?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) 
            return;

        _isDisposed = true;
        lock (_lock)
        {
            foreach (var token in _tokens)
            {
                token.Cancel();
                token.Dispose();
            }
            _tokens.Clear();
        }
        _httpClient.Dispose();
    }

    private class HttpClientHandlerLogger : HttpClientHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return base.SendAsync(request, cancellationToken);
        }
    }
}
