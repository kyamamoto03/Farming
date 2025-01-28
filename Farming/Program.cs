using Farming.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Farming
{
    internal class Program
    {
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<FarmingLoop>();
                services.AddLogging(b => b.AddConsole());
                services.AddSingleton<FarmingSetting>(_ =>
                {
                    var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(path: "appsettings.json")
                    .AddEnvironmentVariables()
                    .Build();

                    FarmingSetting farmingSetting = new()
                    {
                        InputType = builder["InputType"],
                        URI = builder["URI"],
                        ContainerRemove = builder["ContainerRemove"],
                        WaitTime = int.Parse(builder["WaitTime"]),
                        RestartTime = builder.GetSection("RestartTime").Get<string[]>(),
                    };
                    if (builder["Ignore"] is not null)
                    {
                        var s = builder["Ignore"].ToLower();
                        if (s.Length > 1)
                        {
                            farmingSetting.Ignore = s.Split(',');
                        }
                    }
                    return farmingSetting;
                });
                services.AddSingleton<IConfiguration>(_ =>
                {
                    var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile(path: "appsettings.json").Build();
                    return builder;
                });
            });

        private static void Main(string[] args)
        {
            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            catch (OperationCanceledException)
            { }
        }
    }
}
