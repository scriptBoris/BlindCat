using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Extensions;

public static class StreamExt
{
    public static byte[] ToArray(this Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        if (stream is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}