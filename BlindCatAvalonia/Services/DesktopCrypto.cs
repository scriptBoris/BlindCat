using BlindCatCore.Core;
using BlindCatCore.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Services;

public class DesktopCrypto : Crypto
{
    public string PathToFFmpegExe { get; set; } = "ffmpeg";
    public string PathToFFprobeExe { get; set; } = "ffprobe";

    protected sealed override async Task<AppResponse> EncodeVideoTo_Mp4_CENC(string inputFile, string target, string password)
    {
        // todo Реализовать перекодирование mp4 -> mp4:CENC
        throw new NotImplementedException();
        // string key = ToCENCPassword(password);
        // string kid = GetKid();
        //
        // _ = FFmpegWrapper.Open(PathToFFmpegExe,
        // [
        //     $"-i \"{inputFile}\"",
        //     "-vcodec libx264",
        //     "-acodec aac",
        //     "-encryption_scheme cenc-aes-ctr",
        //     $"-encryption_key {key}",
        //     $"-encryption_kid {kid}",
        //     "-f mp4",
        //     $"\"{target}\""
        // ]
        // , out var proc, true);
        //
        // await proc.WaitForExitAsync();
        // proc.Dispose();
        // return AppResponse.OK;
    }

    protected sealed override async Task<AppResponse> EncodeVideoTo_Mp4_CENC(Stream inputStream, string target, string password)
    {
        // todo Реализовать перекодирование mp4 -> mp4:CENC
        throw new NotImplementedException();
        // try
        // {
        //     if (File.Exists(target))
        //         File.Delete(target);
        // }
        // catch (Exception ex)
        // {
        //     return AppResponse.Error("Fail delete exist temp file", 16, ex);
        // }
        //
        // string key = ToCENCPassword(password);
        // string kid = GetKid();
        //
        // var (input, _) = FFmpegWrapper.Open(PathToFFmpegExe,
        // [
        //     //"-v debug",
        //     $"-i pipe:0",
        //     "-vcodec libx264",
        //     "-acodec aac",
        //     "-encryption_scheme cenc-aes-ctr",
        //     $"-encryption_key {key}",
        //     $"-encryption_kid {kid}",
        //     "-f mp4",
        //     $"\"{target}\"",
        //     "-report",
        // ]
        // , out var proc);
        //
        // try
        // {
        //     byte[] buffer = new byte[4096];
        //     int bytesRead;
        //     while (true)
        //     {
        //         bytesRead = inputStream.Read(buffer, 0, buffer.Length);
        //         if (bytesRead == 0)
        //             break;
        //
        //         if (!input.CanWrite)
        //             break;
        //
        //         await input.WriteAsync(buffer, 0, bytesRead);
        //     }
        //     input.Close();
        // }
        // catch (Exception ex)
        // {
        //     return AppResponse.Error("Fail ffmpeg operation", 214, ex);
        // }
        // finally
        // {
        //     proc.Dispose();
        // }
        //
        // //await proc.WaitForExitAsync();
        // return AppResponse.OK;
    }

    public override string ToCENCPassword(string password)
    {
        byte[] salt = Encoding.UTF8.GetBytes("fffuuuuu");
        int iterations = 10000;
        int keySize = 16;
        var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        byte[] key = pbkdf2.GetBytes(keySize);

        string hexKey = BitConverter.ToString(key).Replace("-", "").ToLower();
        return hexKey;
    }

    public override string GetKid()
    {
        return "112233445566778899aabbccddeeff00";
    }
}
