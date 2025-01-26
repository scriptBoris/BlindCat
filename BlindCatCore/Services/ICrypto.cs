using System.Collections.Concurrent;
using BlindCatCore.Core;
using BlindCatCore.Enums;
using BlindCatCore.Extensions;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace BlindCatCore.Services;

public interface ICrypto
{
    Task EncryptFile(string inputFile, string outputFile, string password);

    Task<AppResponse> EncryptFile(string inputFile, string outputFile, string password, EncryptionMethods from,
        EncryptionMethods to);

    Task EncryptFile(Stream inputStreamFile, string outputFile, string password);

    Task<AppResponse<Stream>> DecryptFile(Guid storageId, string inputFile, string? password, long? fileSize,
        CancellationToken cancel);

    Stream? DecryptFileFast(Guid storageId, string inputFile, string? password, long? fileSize);

    string DecryptString(string encryptedText, string password);
    string EncryptString(string plainText, string password);

    long DecryptInt64(string data, string password);
    string EncryptInt64(long value, string password);

    string ToCENCPassword(string passwordText);
    string GetKid();
    long? CalculateOgirinFileSize(Guid storageId, string filePath, string password);
}

public class Crypto : ICrypto
{
    private readonly byte[] _salt = [0x12, 0xdc, 0xab, 0x28, 0xcc, 0x6d, 0xce, 0xb9];
    private ConcurrentDictionary<Guid, CryptoCacheItem> _cache = new();

    public async Task<AppResponse> EncryptFile(string inputFile, string outputFile, string password,
        EncryptionMethods from, EncryptionMethods to)
    {
        string target = outputFile;
        if (inputFile == outputFile)
        {
            string dir = Path.GetDirectoryName(outputFile);
            string fileWithExt = Path.GetFileName(inputFile);
            string tmpDir = Path.Combine(dir, "tmp");
            if (!Directory.Exists(tmpDir))
                Directory.CreateDirectory(tmpDir);

            target = Path.Combine(tmpDir, fileWithExt);
        }

        if (to == EncryptionMethods.CENC)
        {
            if (from == EncryptionMethods.dotnet)
            {
                throw new NotImplementedException();
                // using var dec = await DecryptFile(outputFile, password, CancellationToken.None);
                // if (dec.IsFault)
                //     return dec.AsError;
                //
                // var decodedstream = dec.Result;
                // //var conv = await _fFMpegService.EncodeVideoTo_Mp4_CENC(decodedstream, target, password);
                // var conv = await EncodeVideoTo_Mp4_CENC(decodedstream, target, password);
                // if (conv.IsFault)
                //     return conv.AsError;
            }
            else if (from == EncryptionMethods.None)
            {
                //var conv = await _fFMpegService.EncodeVideoTo_Mp4_CENC(inputFile, target, password);
                var conv = await EncodeVideoTo_Mp4_CENC(inputFile, target, password);
                if (conv.IsFault)
                    return conv.AsError;
            }
            else
            {
                return AppResponse.Error("No support");
            }

            if (inputFile == outputFile)
            {
                try
                {
                    File.Delete(inputFile);
                }
                catch (Exception exDel)
                {
                    return AppResponse.Error("Fail delete origin file", 5, exDel);
                }

                try
                {
                    File.Move(target, inputFile);
                }
                catch (Exception ex)
                {
                    return AppResponse.Error("Fail move temp file", 6, ex);
                }
            }
        }

        return AppResponse.OK;
    }

    public async Task EncryptFile(Stream inputStr, string outputFile, string password)
    {
        // Генерация ключа и IV на основе пароля
        using var secretKey = new PasswordDeriveBytes(password, _salt);

        // Создание AES-шифратора
        using var aesAlg = Aes.Create();
        aesAlg.Key = secretKey.GetBytes(aesAlg.KeySize / 8);
        aesAlg.GenerateIV();

        // Создание потока для записи зашифрованных данных
        using var streamOut = new FileStream(outputFile, FileMode.Create);
        // Запись IV в начало файла
        streamOut.Write(aesAlg.IV, 0, aesAlg.IV.Length);

        // Создание криптографического потока для шифрования данных
        using var cs = new CryptoStream(streamOut, aesAlg.CreateEncryptor(), CryptoStreamMode.Write);

        await inputStr.CopyToAsync(cs);
    }

    public async Task EncryptFile(string inputFile, string outputFile, string password)
    {
        // Генерация ключа и IV на основе пароля
        using var secretKey = new PasswordDeriveBytes(password, _salt);

        // Создание AES-шифратора
        using var aesAlg = Aes.Create();
        aesAlg.Key = secretKey.GetBytes(aesAlg.KeySize / 8);
        aesAlg.GenerateIV();

        // Создание потока для записи зашифрованных данных
        using var streamOut = new FileStream(outputFile, FileMode.Create);
        // Запись IV в начало файла
        streamOut.Write(aesAlg.IV, 0, aesAlg.IV.Length);

        // Создание криптографического потока для шифрования данных
        using var cs = new CryptoStream(streamOut, aesAlg.CreateEncryptor(), CryptoStreamMode.Write);
        // Открытие исходного файла для чтения
        using var fsIn = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);

        await fsIn.CopyToAsync(cs);
    }

    public async Task<AppResponse<Stream>> DecryptFile(Guid storageId, string inputFile, string? password,
        long? fileSize, CancellationToken cancel)
    {
        if (password == null)
            return AppResponse.Error("Password required");

        var cache = _cache.GetOrAdd(storageId, (guid) =>
        {
            return new CryptoCacheItem
            {
                StorageId = guid,
                SecretKey = new PasswordDeriveBytes(password, _salt),
            }.Commit();
        });

        // Создание AES-шифратора
        using var aesAlg = Aes.Create();
        aesAlg.Key = cache.KeyBytes;

        long payloadLength = 0;
        FileStream encriptedStream = null!;
        int tryCount = 3;
        Exception? ex = null;
        while (tryCount > 0)
        {
            // Чтение IV из начала зашифрованного файла
            try
            {
                byte[] iv = new byte[aesAlg.BlockSize / 8];
                encriptedStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                encriptedStream.Read(iv, 0, iv.Length);
                aesAlg.IV = iv;

                if (fileSize != null)
                    payloadLength = fileSize.Value;
                else
                    payloadLength = encriptedStream.Length - encriptedStream.Position;
                break;
            }
            catch (Exception ex1)
            {
                ex = ex1;
                tryCount--;
                await TaskExt.Delay(300, cancel);

                if (cancel.IsCancellationRequested)
                    return AppResponse.Canceled;
            }
        }

        if (tryCount == 0)
            return AppResponse.Error("Fail open file stream", 99907, ex);

        if (cancel.IsCancellationRequested)
        {
            encriptedStream?.Dispose();
            encriptedStream = null!;
            return AppResponse.Canceled;
        }

        // новый подход
        var remake = () => RemakeDecryptFileSync(storageId, inputFile, password);
        var cryptoStream = new CryptoStream(encriptedStream, aesAlg.CreateDecryptor(), CryptoStreamMode.Read);
        var tube = new TubeStream(cryptoStream, payloadLength, remake);
        return AppResponse.Result<Stream>(tube);
    }

    public Stream? DecryptFileFast(Guid storageId, string inputFile, string? password, long? fileSize)
    {
        if (password == null)
            return null;
        
        var cache = _cache.GetOrAdd(storageId, (guid) =>
        {
            return new CryptoCacheItem
            {
                StorageId = guid,
                SecretKey = new PasswordDeriveBytes(password, _salt),
            }.Commit();
        });

        using var aesAlg = Aes.Create();
        aesAlg.Key = cache.KeyBytes;

        // Чтение IV из начала зашифрованного файла
        try
        {
            byte[] iv = new byte[aesAlg.BlockSize / 8];
            var encriptedStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            encriptedStream.Read(iv, 0, iv.Length);
            aesAlg.IV = iv;

            var cryptoTransform = aesAlg.CreateDecryptor();
            var cryptoStream = new CryptoStream(encriptedStream, cryptoTransform, CryptoStreamMode.Read);
            return cryptoStream;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private CryptoStream? RemakeDecryptFileSync(Guid storageId, string inputFile, 
        string password)
    {
        var cache = _cache.GetOrAdd(storageId, (guid) =>
        {
            return new CryptoCacheItem
            {
                StorageId = guid,
                SecretKey = new PasswordDeriveBytes(password, _salt),
            }.Commit();
        });
        
        using var aesAlg = Aes.Create();
        aesAlg.Key = cache.KeyBytes;

        FileStream encriptedStream;
        try
        {
            byte[] iv = new byte[aesAlg.BlockSize / 8];
            encriptedStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            encriptedStream.Read(iv, 0, iv.Length);
            aesAlg.IV = iv;
        }
        catch (Exception)
        {
            return null;
        }

        var cryptoStream = new CryptoStream(encriptedStream, aesAlg.CreateDecryptor(), CryptoStreamMode.Read);
        return cryptoStream;
    }

    public string EncryptString(string plainText, string password)
    {
        // Генерация ключа и IV на основе пароля
        using var secretKey = new PasswordDeriveBytes(password, _salt);
        using var aesAlg = Aes.Create();

        aesAlg.Key = secretKey.GetBytes(aesAlg.KeySize / 8);
        //aesAlg.IV = secretKey.GetBytes(aesAlg.BlockSize / 8);
        aesAlg.GenerateIV();
        aesAlg.Padding = PaddingMode.PKCS7;
        var deb1 = string.Join('.', aesAlg.Key);
        var deb2 = string.Join('.', aesAlg.IV);

        // Создание потока для записи зашифрованных данных
        using var encryptedBins = new MemoryStream();

        // Запись IV в начало файла
        encryptedBins.Write(aesAlg.IV, 0, aesAlg.IV.Length);

        // Создание криптографического потока для шифрования данных
        using (var cs = new CryptoStream(encryptedBins, aesAlg.CreateEncryptor(), CryptoStreamMode.Write))
        {
            // Открытие исходного файла для чтения
            byte[] initBytes = Encoding.UTF8.GetBytes(plainText);
            using var originBytesMem = new MemoryStream(initBytes);

            // Чтение и шифрование данных в поток
            int data;
            long pureCountWrited = 0;
            while ((data = originBytesMem.ReadByte()) != -1)
            {
                cs.WriteByte((byte)data);
                pureCountWrited++;
            }
        }

        byte[] outBin = encryptedBins.ToArray();
        string result = Convert.ToBase64String(outBin);
        return result;
    }

    public string DecryptString(string value, string password)
    {
        try
        {
            // Преобразование зашифрованного текста в массив байтов
            byte[] cipherBytes = Convert.FromBase64String(value);

            // Генерация ключа и IV на основе пароля
            using var secretKey = new PasswordDeriveBytes(password, _salt);
            using var aesAlg = Aes.Create();
            aesAlg.Key = secretKey.GetBytes(aesAlg.KeySize / 8);
            aesAlg.Padding = PaddingMode.PKCS7;

            // Извлечение IV из начала зашифрованных данных
            byte[] iv = new byte[aesAlg.BlockSize / 8];
            Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
            aesAlg.IV = iv;
            var deb1 = string.Join('.', aesAlg.Key);
            var deb2 = string.Join('.', iv);

            string res = "?";
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, aesAlg.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(cipherBytes, iv.Length, cipherBytes.Length - iv.Length);
                }

                res = Encoding.UTF8.GetString(ms.ToArray());
            }

            return res;

            // Создание потока для чтения расшифрованных данных
            //using (var msDecrypt = new MemoryStream(cipherBytes, iv.Length, cipherBytes.Length - iv.Length))
            //{
            //    using (var csDecrypt = new CryptoStream(msDecrypt, aesAlg.CreateDecryptor(), CryptoStreamMode.Read))
            //    {
            //        using var srDecrypt = new StreamReader(csDecrypt);
            //    }
            //}

            // Чтение расшифрованного текста
            //string result = srDecrypt.ReadToEnd();
            //return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Fail decrypt text: " + ex);
            return "?";
        }
    }

    public string EncryptInt64(long int64, string password)
    {
        // Генерация ключа и IV на основе пароля
        using var secretKey = new PasswordDeriveBytes(password, _salt);
        using var aesAlg = Aes.Create();

        aesAlg.Key = secretKey.GetBytes(aesAlg.KeySize / 8);
        //aesAlg.IV = secretKey.GetBytes(aesAlg.BlockSize / 8);
        aesAlg.GenerateIV();
        aesAlg.Padding = PaddingMode.PKCS7;
        var deb1 = string.Join('.', aesAlg.Key);
        var deb2 = string.Join('.', aesAlg.IV);

        // Создание потока для записи зашифрованных данных
        using var encryptedBins = new MemoryStream();

        // Запись IV в начало файла
        encryptedBins.Write(aesAlg.IV, 0, aesAlg.IV.Length);

        // Создание криптографического потока для шифрования данных
        using (var cs = new CryptoStream(encryptedBins, aesAlg.CreateEncryptor(), CryptoStreamMode.Write))
        {
            // Открытие исходного файла для чтения
            byte[] initBytes = BitConverter.GetBytes(int64);
            using var originBytesMem = new MemoryStream(initBytes);

            // Чтение и шифрование данных в поток
            int data;
            long pureCountWrited = 0;
            while ((data = originBytesMem.ReadByte()) != -1)
            {
                cs.WriteByte((byte)data);
                pureCountWrited++;
            }
        }

        byte[] outBin = encryptedBins.ToArray();
        string result = Convert.ToBase64String(outBin);
        return result;
    }

    public long DecryptInt64(string value, string password)
    {
        try
        {
            // Преобразование зашифрованного текста в массив байтов
            byte[] cipherBytes = Convert.FromBase64String(value);

            // Генерация ключа и IV на основе пароля
            using var secretKey = new PasswordDeriveBytes(password, _salt);
            using var aesAlg = Aes.Create();
            aesAlg.Key = secretKey.GetBytes(aesAlg.KeySize / 8);
            aesAlg.Padding = PaddingMode.PKCS7;

            // Извлечение IV из начала зашифрованных данных
            byte[] iv = new byte[aesAlg.BlockSize / 8];
            Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
            aesAlg.IV = iv;
            var deb1 = string.Join('.', aesAlg.Key);
            var deb2 = string.Join('.', iv);

            long res = 0;
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, aesAlg.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(cipherBytes, iv.Length, cipherBytes.Length - iv.Length);
                }

                res = BitConverter.ToInt64(ms.ToArray());
            }

            return res;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Fail decrypt text: " + ex);
            return 0;
        }
    }

    /// <summary>
    /// Конвертирование в mp4 и шифрование с использование CENC
    /// </summary>
    /// <param name="inputFile"></param>
    /// <param name="target"></param>
    /// <param name="password"></param>
    protected virtual Task<AppResponse> EncodeVideoTo_Mp4_CENC(string inputFile, string target, string password)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Конвертирование в mp4 и шифрование с использование CENC
    /// </summary>
    /// <param name="inputStream"></param>
    /// <param name="target"></param>
    /// <param name="password"></param>
    protected virtual Task<AppResponse> EncodeVideoTo_Mp4_CENC(Stream inputStream, string target, string password)
    {
        throw new NotImplementedException();
    }

    public virtual string ToCENCPassword(string password)
    {
        throw new NotImplementedException();
    }

    public virtual string GetKid()
    {
        throw new NotImplementedException();
    }

    private long SeekThroughRead(Stream stream, long currentPosition, long targetPosition)
    {
        const int bufferSize = 4096; // Размер буфера для пропуска данных
        var buffer = new byte[bufferSize];

        while (currentPosition < targetPosition)
        {
            // Сколько данных нужно пропустить
            long remaining = targetPosition - currentPosition;
            int bytesToRead = (int)Math.Min(bufferSize, remaining);

            // Читаем данные и обновляем текущую позицию
            int bytesRead = stream.Read(buffer, 0, bytesToRead);
            if (bytesRead == 0)
            {
                // Достигнут конец потока
                throw new InvalidOperationException("Cannot seek beyond the end of the stream.");
            }

            currentPosition += bytesRead;
        }

        return currentPosition; // Возвращаем новую позицию (должна совпадать с targetPosition)
    }

    public long? CalculateOgirinFileSize(Guid storageId, string encryptedFilePath, string password)
    {
        const int ivSize = 16;

        // Открываем зашифрованный файл
        using var stream = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read);

        // Проверяем минимальную длину файла
        if (stream.Length <= ivSize)
            throw new InvalidOperationException("Файл слишком мал для расшифровки.");

        // Считываем IV
        byte[] iv = new byte[ivSize];
        stream.Read(iv, 0, ivSize);

        // Настраиваем шифрование
        using var secretKey = new PasswordDeriveBytes(password, _salt);
        using var aesAlg = Aes.Create();
        aesAlg.Key = secretKey.GetBytes(aesAlg.KeySize / 8);
        aesAlg.IV = iv;

        // aesAlg.Padding = PaddingMode.PKCS7;
        // aesAlg.Mode = CipherMode.CBC;

        // Открываем поток для расшифровки
        using var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
        using var cryptoStream = new CryptoStream(stream, decryptor, CryptoStreamMode.Read);

        // Читаем расшифрованные данные и считаем их длину
        long totalBytesRead = 0;
        byte[] buffer = new byte[4096];
        int bytesRead;

        try
        {
            while ((bytesRead = cryptoStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalBytesRead += bytesRead;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Debugger.Break();
            return null;
        }

        return totalBytesRead;
    }

    private class CryptoCacheItem
    {
        public Guid StorageId { get; set; }
        public PasswordDeriveBytes SecretKey { get; set; }
        public byte[] KeyBytes { get; set; }

        public CryptoCacheItem Commit()
        {
            KeyBytes = SecretKey.GetBytes(32);
            return this;
        }
    }
}