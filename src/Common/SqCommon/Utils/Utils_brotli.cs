using System;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SqCommon
{
    public static partial class Utils
    {
        public static byte[] Str2BrotliBin(string p_str)
        {
            // For JSON string: One third (1/3rd) size reduction from UTF8 string to brotli bytes
            // E.g. allAssets in JSON: UTF8 text file: 1585 bytes, in-memory string allAssetsJson.Length: 1557 System.Text.Encoding.UTF8.GetBytes(): 1557,
            // Zip file: 696 bytes, 7z file: 695 bytes, Brotli in-memory bytes: 520 (1/3rd of the raw data size), -26% better than Zip file.
            using System.IO.MemoryStream msOutput = new();
            using var bs = new BrotliStream(msOutput, CompressionLevel.Optimal);
            byte[] inBytes = System.Text.Encoding.UTF8.GetBytes(p_str); // C# string is UTF16. Convert it to UTF8 bytes.
            bs.Write(inBytes);
            bs.Close();
            return msOutput.ToArray();
        }

        public static string BrotliBin2Str(byte[] p_bin)
        {
            using System.IO.MemoryStream msOutput = new(p_bin);
            using var bs = new BrotliStream(msOutput, CompressionMode.Decompress);
            int maxNbytesRead = p_bin.Length * 10;    // assume compression is max 10x, so we need 10x more byte array for uncompressed bytes
            byte[] decompressed = new byte[maxNbytesRead];
            var nReadBytes = bs.Read(decompressed);
            if (nReadBytes == maxNbytesRead)
                throw new Exception("maxNbytesRead is reached. Increase multiplier for temp buffer or develop a loop.");
            bs.Close();
            return System.Text.Encoding.UTF8.GetString(new ReadOnlySpan<byte>(decompressed, 0, nReadBytes));
        }

        public static byte[] Bin2BrotliBin(byte[] p_bytes)
        {
            using System.IO.MemoryStream msOutput = new();
            using var bs = new BrotliStream(msOutput, CompressionLevel.Optimal);
            bs.Write(p_bytes);
            bs.Close();
            return msOutput.ToArray();
        }

        public static byte[] BrotliBin2Bin(byte[] p_bin)
        {
            using System.IO.MemoryStream msOutput = new(p_bin);
            using var bs = new BrotliStream(msOutput, CompressionMode.Decompress);
            int maxNbytesRead = p_bin.Length * 10;    // assume compression is max 10x, so we need 10x more byte array for uncompressed bytes
            byte[] decompressed = new byte[maxNbytesRead];
            var nReadBytes = bs.Read(decompressed);
            if (nReadBytes == maxNbytesRead)
                throw new Exception("maxNbytesRead is reached. Increase multiplier for temp buffer or develop a loop.");
            bs.Close();
            return decompressed;
        }
    }
}