using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using System.Text;

namespace AtomicCounter
{
    public static class Utilities
    {
        public static string ToJson<T>(this T input)
        {
            return input.ToJson();
        }

        public static T FromJson<T>(this string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return default(T);

            return JsonConvert.DeserializeObject<T>(input);
        }

        public static string ToBase64String<T>(this T input)
        {
            return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(input.ToJson()));
        }

        public static T FromBase64String<T>(this string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return default(T);

            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input)).FromJson<T>();
        }
    }
}
