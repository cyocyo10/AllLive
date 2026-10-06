using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AllLive.UWP.Helper
{
    internal static class LoginHttp
    {
        // Login cookies remain a candidate until explicitly committed. Never put a
        // Set-Cookie response from a cancelled QR poll into a shared CookieContainer.
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        }) { Timeout = TimeSpan.FromSeconds(30) };

        public static async Task<HttpResponseMessage> GetAsync(string url, string cookie, CancellationToken token)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (!string.IsNullOrWhiteSpace(cookie)) request.Headers.TryAddWithoutValidation("Cookie", cookie);
                var response = await Client.SendAsync(request, token);
                try { response.EnsureSuccessStatusCode(); return response; }
                catch { response.Dispose(); throw; }
            }
        }
        public static async Task<string> GetStringAsync(string url, string cookie, CancellationToken token)
        {
            using (var response = await GetAsync(url, cookie, token))
            {
                var result = await response.Content.ReadAsStringAsync();
                token.ThrowIfCancellationRequested();
                return result;
            }
        }
    }
}
