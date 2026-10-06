using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AllLive.UWP.Helper
{
    public enum AccountVerificationState { SignedOut, Unverified, Verifying, Verified, Expired, Failed }

    /// <summary>A new attempt invalidates every callback belonging to the previous one.</summary>
    internal sealed class LoginAttempt : IDisposable
    {
        private CancellationTokenSource cancellation;
        public CancellationToken Begin()
        {
            Cancel();
            cancellation = new CancellationTokenSource();
            return cancellation.Token;
        }
        public void Cancel()
        {
            var old = cancellation;
            cancellation = null;
            if (old == null) return;
            old.Cancel();
            old.Dispose();
        }
        public void Dispose() { Cancel(); }
    }

    internal static class LoginCookiePolicy
    {
        public static bool IsPlatformDomain(string cookieDomain, string platformDomain)
        {
            if (string.IsNullOrWhiteSpace(cookieDomain) || string.IsNullOrWhiteSpace(platformDomain)) return false;
            var domain = cookieDomain.TrimStart('.');
            return domain.Equals(platformDomain, StringComparison.OrdinalIgnoreCase)
                || domain.EndsWith("." + platformDomain, StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasSessionCookie(string cookie, string platformDomain)
        {
            if (string.IsNullOrWhiteSpace(cookie)) return false;
            var names = new HashSet<string>(cookie.Split(';').Select(x => x.Trim())
                .Where(x => x.IndexOf('=') > 0 && x.Substring(x.IndexOf('=') + 1).Trim().Length > 0)
                .Select(x => x.Substring(0, x.IndexOf('='))), StringComparer.Ordinal);
            // These only identify a candidate session, never prove server-side validity.
            if (platformDomain == "douyu.com") return names.Contains("acf_auth");
            if (platformDomain == "douyin.com") return names.Contains("sessionid") || names.Contains("sessionid_ss") || names.Contains("sid_tt");
            if (platformDomain == "bilibili.com") return names.Contains("SESSDATA");
            return false;
        }
    }
}
