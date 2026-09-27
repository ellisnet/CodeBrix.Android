using CodeBrix.Platform.Simple;
using Microsoft.Extensions.Hosting;

namespace WebcamPainter.Helpers;

public static class HostHelper
{
    private class HostBuilderProvider : IHostBuilderProvider
    {
        public IHostBuilder CreateDefaultBuilder() => Host.CreateDefaultBuilder();
        public IHostBuilder CreateDefaultBuilder(string[] args) => Host.CreateDefaultBuilder(args);
    }

    private static readonly HostBuilderProvider Instance = new();

    public static IHostBuilderProvider GetHost() => Instance;
}
