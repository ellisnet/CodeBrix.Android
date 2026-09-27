using CodeBrix.Platform.Simple;
using Microsoft.Extensions.Hosting;

namespace PainDiagram.Helpers;

public static class HostHelper
{
    private class HostBuilderProvider : IHostBuilderProvider
    {
        public IHostBuilder CreateDefaultBuilder() => Host.CreateDefaultBuilder();
        public IHostBuilder CreateDefaultBuilder(string[] args) => Host.CreateDefaultBuilder(args);
    }

    private static readonly HostBuilderProvider _hostBuilderProvider = new();

    public static IHostBuilderProvider GetHost() => _hostBuilderProvider;
}
