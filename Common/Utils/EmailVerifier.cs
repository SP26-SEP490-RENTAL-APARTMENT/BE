using System.Net.Mail;
using DnsClient;

namespace Common.Utils
{
    public static class EmailVerifier
    {
        private static readonly LookupClient _lookup = new();

        public static bool IsValidFormat(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> DomainHasMailServerAsync(string email, int timeoutMs = 2000)
        {
            try
            {
                var domain = email.Split('@').Last();
                var mxTask = _lookup.QueryAsync(domain, QueryType.MX);
                var mxCompleted = await Task.WhenAny(mxTask, Task.Delay(timeoutMs));
                if (mxCompleted == mxTask && mxTask.Result.Answers.MxRecords().Any())
                    return true;

                // fallback to A/AAAA records
                var aTask = _lookup.QueryAsync(domain, QueryType.A);
                var aCompleted = await Task.WhenAny(aTask, Task.Delay(timeoutMs));
                if (aCompleted == aTask && aTask.Result.Answers.ARecords().Any())
                    return true;

                var aaaaTask = _lookup.QueryAsync(domain, QueryType.AAAA);
                var aaaaCompleted = await Task.WhenAny(aaaaTask, Task.Delay(timeoutMs));
                if (aaaaCompleted == aaaaTask && aaaaTask.Result.Answers.AaaaRecords().Any())
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        // Very small built-in disposable / blocked domains list. Extend as needed.
        // Includes common disposable providers and reserved example domains which should be rejected.
        private static readonly string[] DisposableDomains =
        [
            // disposable providers
            "mailinator.com",
            "10minutemail.com",
            "tempmail.com",
            "dispostable.com",
            "guerrillamail.com",
            "trashmail.com",
            // reserved / example domains that are not real user mailboxes
            "example.com",
            "example.org",
            "example.net",
            "localhost",
            "invalid",
            "test",
        ];

        public static bool IsDisposableDomain(string email)
        {
            try
            {
                var domain = email.Split('@').Last().ToLowerInvariant();
                // match exact domain or subdomains (e.g. sub.mailinator.com)
                foreach (var blocked in DisposableDomains)
                {
                    if (domain == blocked)
                        return true;
                    if (domain.EndsWith("." + blocked))
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
