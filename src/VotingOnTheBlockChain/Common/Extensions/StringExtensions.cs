using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public static class StringExtensions
    {
        public static string HexToString(this string hexString)
        {
            try
            {
                if (hexString == null || (hexString.Length & 1) == 1)
                {
                    //return original 
                    return hexString;
                }
                var sb = new StringBuilder();
                for (var i = 0; i < hexString.Length; i += 2)
                {
                    var hexChar = hexString.Substring(i, 2);
                    sb.Append((char)Convert.ToByte(hexChar, 16));
                }
                return sb.ToString().Replace(Convert.ToChar(0x0).ToString(), "");
            }
            catch
            {
                return hexString;
            }

        }

        public static string StringToHex(this string inputString)
        {
            try
            {
                byte[] bytes = Encoding.Default.GetBytes(inputString);
                string hexString = BitConverter.ToString(bytes);
                return hexString.Replace("-", "");
            }
            catch
            {
                return inputString;
            }
        }

        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);

        }
        public static string Truncate(this string value, int maxLength, bool removeFromCenter)
        {

            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (removeFromCenter)
            {

                if (maxLength % 2 > 0)
                {
                    maxLength--;
                }

                var firstPart = value.Length <= (maxLength) ? value : value.Substring(0, (maxLength / 2));
                var secondPart = value.Length <= (maxLength) ? value : value.Substring(value.Length - (maxLength / 2));

                return string.Concat(firstPart, "...", secondPart);
            }
            else
                return value.Truncate(maxLength);

        }
    }
}
