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
        private string FarmingSeetingFileName = "FarmingSetting.json";

        private string FARMING_SETTING_INPUT_TYPE_FILE = "file";
        private string FARMING_SETTING_TRUE = "true";

        private async Task MainLoop()
        {
            _logger.LogInformation("MainLoop Start");


            var containerService = new ContainerService();

            containerService.MessageCalled = (x => _logger.LogInformation(x));

            while (!_stoppingCts.IsCancellationRequested)
            {
                //設定ファイル読み込み
                var containerSettingList = await LoadContainerSettingsList();

                ///jsonにないcontainerを削除
                var RunningContainers = await containerService.GetAllContainer();
                foreach(var rc in RunningContainers)
                {
                    if (!containerSettingList.ContainerSettings.Any(x => $"{x.Image}:{x.Tag}" == $"{rc.Image}") == true)
                    {
                        if ((await LoadFarmingSetting()).ContainerRemove == FARMING_SETTING_TRUE)
                        {
                            _logger.LogInformation($"Container Stop :{rc.Image}");
                            await containerService.StopContainer(rc.ID);
                        }
                        else
                        {
                            _logger.LogInformation($"Container Stop & Remove:{rc.Image}");
                            await containerService.StopAndDeleteContainer(rc.ID);
                        }
                    }
                }
                foreach (var targetContainer in containerSettingList.ContainerSettings)
                {
                    
                    string target_image = targetContainer.Image;
                    string target_image_tag = targetContainer.Tag;


                    await containerService.StartContainer(targetContainer);

                }
                await Task.Delay(5000);
            }
        }

        private async Task<FarmingSetting> LoadFarmingSetting()
        {
            var farmingSettingJsonService = new JsonService<FarmingSetting>();
            return await farmingSettingJsonService.FromFile(FarmingSeetingFileName);
        }

        private async Task<ContainerSettingsList> LoadContainerSettingsList()
        {
            var farmingSetting = await LoadFarmingSetting();

            #region LoadContainerSetting
            var jsonService = new JsonService<ContainerSettingsList>();
            ContainerSettingsList containerSettingsList;

            if (farmingSetting.InputType == FARMING_SETTING_INPUT_TYPE_FILE)
            {
                containerSettingsList = await jsonService.FromFile(farmingSetting.URI);
            }
            else
            {
                containerSettingsList = await jsonService.FromURL(farmingSetting.URI);
            }
            #endregion

            return containerSettingsList;
        }
    }
}
