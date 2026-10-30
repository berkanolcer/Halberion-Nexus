using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Halberion.HotspotControl.Services;

/// <summary>Dependency-free QR Code Model 2 encoder for Wi-Fi payloads (Version 10-L, byte mode).</summary>
public static class QrCodeService
{
    private const int Version = 10;
    private const int Size = 21 + 4 * (Version - 1);
    private const int DataCodewords = 274;
    private const int TotalCodewords = 346;
    private const int BlockCount = 4;
    private const int EccCodewordsPerBlock = 18;

    public static byte[] CreateWifiPng(string ssid, string passphrase)
    {
        if (string.IsNullOrWhiteSpace(ssid)) throw new ArgumentException("SSID boş olamaz.", nameof(ssid));
        if (passphrase.Length is < 8 or > 63) throw new ArgumentException("WPA2 parolası 8–63 karakter olmalıdır.", nameof(passphrase));

        var payload = $"WIFI:T:WPA;S:{Escape(ssid.Trim())};P:{Escape(passphrase)};;";
        var bytes = Encoding.UTF8.GetBytes(payload);
        if (bytes.Length > 271)
            throw new ArgumentException("SSID/parola QR kod kapasitesini aşıyor. Daha kısa veya daha az çok baytlı karakter kullanın.");

        var codewords = AddErrorCorrection(CreateDataCodewords(bytes));
        var modules = BuildMatrix(codewords);
        return RenderPng(modules);
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is '\\' or ';' or ',' or '"' or ':') builder.Append('\\');
            builder.Append(c);
        }
        return builder.ToString();
    }

    private static byte[] CreateDataCodewords(byte[] payload)
    {
        var bits = new List<bool>(DataCodewords * 8);
        AppendBits(bits, 0b0100, 4); // Byte mode
        AppendBits(bits, payload.Length, 16); // Versions 10–40 use a 16-bit byte count
        foreach (var value in payload) AppendBits(bits, value, 8);
        var capacity = DataCodewords * 8;
        AppendBits(bits, 0, Math.Min(4, capacity - bits.Count));
        while (bits.Count % 8 != 0) bits.Add(false);

        var result = new List<byte>(DataCodewords);
        for (var i = 0; i < bits.Count; i += 8)
        {
            var value = 0;
            for (var j = 0; j < 8; j++) value = (value << 1) | (bits[i + j] ? 1 : 0);
            result.Add((byte)value);
        }
        for (var pad = 0; result.Count < DataCodewords; pad++) result.Add((byte)(pad % 2 == 0 ? 0xEC : 0x11));
        return result.ToArray();
    }

    private static byte[] AddErrorCorrection(byte[] data)
    {
        var divisor = ReedSolomonDivisor(EccCodewordsPerBlock);
        var shortBlockTotal = TotalCodewords / BlockCount;
        var shortBlockCount = BlockCount - TotalCodewords % BlockCount;
        var shortDataLength = shortBlockTotal - EccCodewordsPerBlock;
        var blocks = new List<(byte[] Data, byte[] Ecc)>();
        var offset = 0;

        for (var block = 0; block < BlockCount; block++)
        {
            var length = shortDataLength + (block < shortBlockCount ? 0 : 1);
            var blockData = data[offset..(offset + length)];
            offset += length;
            blocks.Add((blockData, ReedSolomonRemainder(blockData, divisor)));
        }

        var result = new List<byte>(TotalCodewords);
        var maxDataLength = blocks.Max(b => b.Data.Length);
        for (var i = 0; i < maxDataLength; i++)
            foreach (var block in blocks)
                if (i < block.Data.Length) result.Add(block.Data[i]);
        for (var i = 0; i < EccCodewordsPerBlock; i++)
            foreach (var block in blocks) result.Add(block.Ecc[i]);
        return result.ToArray();
    }

    private static bool[,] BuildMatrix(byte[] codewords)
    {
        var modules = new bool[Size, Size];
        var function = new bool[Size, Size];
        DrawFinder(modules, function, 3, 3);
        DrawFinder(modules, function, Size - 4, 3);
        DrawFinder(modules, function, 3, Size - 4);

        for (var i = 0; i < Size; i++)
        {
            if (!function[6, i]) SetFunction(modules, function, i, 6, i % 2 == 0);
            if (!function[i, 6]) SetFunction(modules, function, 6, i, i % 2 == 0);
        }

        var align = new[] { 6, 28, 50 };
        for (var row = 0; row < align.Length; row++)
        for (var column = 0; column < align.Length; column++)
        {
            var overlapsFinder = (row == 0 && column == 0)
                || (row == 0 && column == align.Length - 1)
                || (row == align.Length - 1 && column == 0);
            if (!overlapsFinder) DrawAlignment(modules, function, align[column], align[row]);
        }

        DrawVersion(modules, function);
        DrawFormat(modules, function, mask: 0);
        DrawCodewords(modules, function, codewords);
        DrawFormat(modules, function, mask: 0);
        return modules;
    }

    private static void DrawFinder(bool[,] modules, bool[,] function, int centerX, int centerY)
    {
        for (var dy = -4; dy <= 4; dy++)
        for (var dx = -4; dx <= 4; dx++)
        {
            var x = centerX + dx;
            var y = centerY + dy;
            if (x < 0 || y < 0 || x >= Size || y >= Size) continue;
            var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
            SetFunction(modules, function, x, y, distance is not (2 or 4));
        }
    }

    private static void DrawAlignment(bool[,] modules, bool[,] function, int centerX, int centerY)
    {
        for (var dy = -2; dy <= 2; dy++)
        for (var dx = -2; dx <= 2; dx++)
            SetFunction(modules, function, centerX + dx, centerY + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
    }

    private static void DrawVersion(bool[,] modules, bool[,] function)
    {
        var remainder = Version;
        for (var i = 0; i < 12; i++) remainder = (remainder << 1) ^ ((remainder >> 11) * 0x1F25);
        var bits = (Version << 12) | remainder;
        for (var i = 0; i < 18; i++)
        {
            var bit = ((bits >> i) & 1) != 0;
            var a = Size - 11 + i % 3;
            var b = i / 3;
            SetFunction(modules, function, a, b, bit);
            SetFunction(modules, function, b, a, bit);
        }
    }

    private static void DrawFormat(bool[,] modules, bool[,] function, int mask)
    {
        var data = (1 << 3) | mask; // Error correction level L has format bits 01
        var remainder = data;
        for (var i = 0; i < 10; i++) remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);
        var bits = ((data << 10) | remainder) ^ 0x5412;

        for (var i = 0; i <= 5; i++) SetFunction(modules, function, 8, i, GetBit(bits, i));
        SetFunction(modules, function, 8, 7, GetBit(bits, 6));
        SetFunction(modules, function, 8, 8, GetBit(bits, 7));
        SetFunction(modules, function, 7, 8, GetBit(bits, 8));
        for (var i = 9; i < 15; i++) SetFunction(modules, function, 14 - i, 8, GetBit(bits, i));

        for (var i = 0; i < 8; i++) SetFunction(modules, function, Size - 1 - i, 8, GetBit(bits, i));
        for (var i = 8; i < 15; i++) SetFunction(modules, function, 8, Size - 15 + i, GetBit(bits, i));
        SetFunction(modules, function, 8, Size - 8, true);
    }

    private static void DrawCodewords(bool[,] modules, bool[,] function, byte[] codewords)
    {
        var bitIndex = 0;
        var upward = true;
        for (var right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (var vertical = 0; vertical < Size; vertical++)
            {
                var y = upward ? Size - 1 - vertical : vertical;
                for (var column = 0; column < 2; column++)
                {
                    var x = right - column;
                    if (function[y, x]) continue;
                    var bit = bitIndex < codewords.Length * 8 && GetBit(codewords[bitIndex >> 3], 7 - (bitIndex & 7));
                    bitIndex++;
                    if ((x + y) % 2 == 0) bit = !bit; // Mask pattern 0
                    modules[y, x] = bit;
                }
            }
            upward = !upward;
        }
    }

    private static byte[] RenderPng(bool[,] modules)
    {
        const int scale = 9;
        const int border = 4;
        var side = (Size + border * 2) * scale;
        var pixels = new byte[side * side * 4];
        Array.Fill(pixels, (byte)255);
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            if (!modules[y, x]) continue;
            for (var py = 0; py < scale; py++)
            for (var px = 0; px < scale; px++)
            {
                var index = (((y + border) * scale + py) * side + (x + border) * scale + px) * 4;
                pixels[index] = 31;      // B
                pixels[index + 1] = 17;  // G
                pixels[index + 2] = 7;   // R
                pixels[index + 3] = 255;
            }
        }
        var bitmap = BitmapSource.Create(side, side, 96, 96, PixelFormats.Bgra32, null, pixels, side * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static byte[] ReedSolomonDivisor(int degree)
    {
        var result = new byte[degree];
        result[^1] = 1;
        byte root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < result.Length; j++)
            {
                result[j] = GaloisMultiply(result[j], root);
                if (j + 1 < result.Length) result[j] ^= result[j + 1];
            }
            root = GaloisMultiply(root, 2);
        }
        return result;
    }

    private static byte[] ReedSolomonRemainder(byte[] data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (var value in data)
        {
            var factor = (byte)(value ^ result[0]);
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++) result[i] ^= GaloisMultiply(divisor[i], factor);
        }
        return result;
    }

    private static byte GaloisMultiply(byte x, byte y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            if (((y >> i) & 1) != 0) z ^= x;
        }
        return (byte)z;
    }

    private static void AppendBits(List<bool> bits, int value, int length)
    {
        for (var i = length - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
    }

    private static bool GetBit(int value, int index) => ((value >> index) & 1) != 0;

    private static void SetFunction(bool[,] modules, bool[,] function, int x, int y, bool value)
    {
        modules[y, x] = value;
        function[y, x] = true;
    }
}
