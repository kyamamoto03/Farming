using Docker.DotNet;
using Docker.DotNet.Models;
using Farming.Model;
using Farming.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
                    _logger.LogError($"{ex}");
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

        private readonly ILogger<FarmingLoop> _logger;

        public FarmingLoop(ILogger<FarmingLoop> logger)
        {
            _logger = logger;
        }
        public string FarmingContainerFileName = "FarmingContainer.json";
        public string FarmingContainerURL= "https://farming.z11.web.core.windows.net/FarmingContainer.json";

        private async Task MainLoop()
        {
            _logger.LogInformation("MainLoop Start");

            var farmingContainerService = new FarmingContainerService<ContainerServiceBase>();

            //var targetContainers = await farmingContainerService.FromFile(FarmingContainerFileName);
            ContainerServiceBase targetContainers = await farmingContainerService.FromURL(FarmingContainerURL);

            var containerService = new Services.ContainerService();

            containerService.MessageCalled = (x => _logger.LogInformation(x));

            while (!_stoppingCts.IsCancellationRequested)
            {
                //設定ファイル読み込み
                targetContainers = await farmingContainerService.FromURL(FarmingContainerURL);

                ///jsonにないcontainerを削除
                var RunningContainers = await containerService.GetAllContainer();
                foreach(var rc in RunningContainers)
                {
                    if (!targetContainers.ContainerServices.Any(x => $"{x.Image}:{x.Tag}" == $"{rc.Image}") == true)
                    {
                        _logger.LogInformation($"Container Stop & Remove:{rc.Image}");
                        await containerService.StopAndDeleteContainer(rc.ID);
                    }
                }
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
