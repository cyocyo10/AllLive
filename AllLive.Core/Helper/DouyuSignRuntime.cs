using System;
using System.Threading.Tasks;

namespace AllLive.Core.Helper
{
    public interface IDouyuSignRunner
    {
        Task<string> GenerateSignAsync(string html, string rid);
    }

    public static class DouyuSignRuntime
    {
        private static IDouyuSignRunner _current;

        static DouyuSignRuntime()
        {
            _current = new NullDouyuSignRunner();
        }

        public static IDouyuSignRunner Current
        {
            get => _current;
            set => _current = value ?? throw new ArgumentNullException(nameof(value));
        }

        private sealed class NullDouyuSignRunner : IDouyuSignRunner
        {
            public Task<string> GenerateSignAsync(string html, string rid) => Task.FromResult(string.Empty);
        }
    }
}
