using System.IO.Compression;

namespace YLang;

public static class Tgz
{
    public static void Unzip(GZipStream str, string output)
    {
        var stream = new MemoryStream();
        str.CopyTo(stream);
        stream.Position = 0;
        Span<byte> buff = stackalloc byte[512];
        Span<byte> header = stackalloc byte[512];
        while (true)
        {
            header.Clear();
            var nl = stream.Read(header);
            var name = System.Text.Encoding.ASCII.GetString(header.Slice(0, 100)).Trim('\0');
            if (string.IsNullOrWhiteSpace(name))
                return;
            var sizestr = System.Text.Encoding.ASCII.GetString(header.Slice(124, 12)).Trim('\0');
            var size = name.EndsWith('/') ? 0 : Convert.ToInt32(sizestr, 8);
            if (size > 0 && !name.EndsWith('/'))
            {
                var blocksize = size + (512 - (size % 512));
                Directory.CreateDirectory(Path.Combine(output, Path.GetDirectoryName(name)!));
                using var fs = File.Create(Path.Combine(output, name));
                while(size > 0) 
                {
                    int readed = stream.Read(buff);
                    fs.Write(buff.Slice(0, readed));
                    size -= 512; 
                }
            }
        }
    }
}

