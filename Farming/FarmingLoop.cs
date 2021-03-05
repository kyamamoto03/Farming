using Docker.DotNet;
using Docker.DotNet.Models;
using Farming.Model;
using Farming.Services;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farming
{
    public class FarmingLoop : IHostedService, IDisposable
    {
        #region IHostedService
        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();
        private Task _executingTask;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _executingTask = Task.Run(() =>
            {
                try
                {
                    MainLoop().Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex}");
                }
            });

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _stoppingCts.Cancel();
            }
            finally
            {
                await Task.WhenAny(_executingTask, Task.Delay(5000, cancellationToken));
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {

        }
        #endregion
        public string FarmingContainerFileName = "FarmingContainer.json";

        private async Task MainLoop()
        {
            var farmingContainerService = new FarmingContainerService();

            var targetContainers = await farmingContainerService.FromFile(FarmingContainerFileName);
            var containerService = new Services.ContainerService();

            while (!_stoppingCts.IsCancellationRequested)
            {
                foreach (var targetContainer in targetContainers.ContainerServices)
                {
                    string target_image = targetContainer.Image;
                    string target_image_tag = targetContainer.Tag;


                    await containerService.StartContainer(targetContainer);

                }
                await Task.Delay(5000);
            }
        }
    }
}
