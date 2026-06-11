using System.Text;

namespace J9_NeoAdmin.Utils;

public static class StringExtensions
{
    public static bool IsNull(this string? s) => string.IsNullOrWhiteSpace(s);

    public static string ToHex(this byte[] bytes, bool lowerCase = true)
    {
        if (bytes == null)
        {
            return null!;
        }

        var text = lowerCase ? "x2" : "X2";
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            builder.Append(b.ToString(text));
        }

        return builder.ToString();
    }

    public static byte[] HexToBytes(this string s)
    {
        if (s.IsNull())
        {
            return null!;
        }

        var array = new byte[s.Length / 2];
        for (var i = 0; i < s.Length / 2; i++)
        {
            array[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
        }

        return array;
    }
}
