using Docker.DotNet;
using Docker.DotNet.Models;
using Farming.Model;
using Farming.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly FarmingSetting farmingSetting;

        public FarmingLoop(ILogger<FarmingLoop> logger, FarmingSetting setting)
        {
            _logger = logger;
            farmingSetting = setting;

            var sb = new StringBuilder();
            sb.AppendLine($"InputType:{farmingSetting.InputType}");
            sb.AppendLine($"URI:{farmingSetting.URI}");
            sb.AppendLine($"ContainerRemove:{farmingSetting.ContainerRemove}");
            sb.AppendLine($"WaitTime:{farmingSetting.ToString()}");

            _logger.LogInformation(sb.ToString());
        }

        private string FARMING_SETTING_INPUT_TYPE_FILE = "file";
        private string FARMING_SETTING_TRUE = "true";

        private async Task MainLoop()
        {
            _logger.LogInformation("MainLoop Start");


            var containerService = new ContainerService();

            containerService.MessageCalled = (x => _logger.LogInformation(x));

            while (!_stoppingCts.IsCancellationRequested)
            {
                //コンテナ情報読み込み
                var containerSettingList = await LoadContainerSettingsList();

                ///jsonにないcontainerを削除ループ
                var RunningContainers = await containerService.GetAllContainer();
                foreach(var rc in RunningContainers)
                {
                    if (!containerSettingList.ContainerSettings.Any(x => $"{x.Image}:{x.Tag}" == $"{rc.Image}") == true)
                    {
                        if (farmingSetting.ContainerRemove == FARMING_SETTING_TRUE)
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

                //起動ループ
                foreach (var targetContainer in containerSettingList.ContainerSettings)
                {
                    
                    string target_image = targetContainer.Image;
                    string target_image_tag = targetContainer.Tag;


                    await containerService.StartContainer(targetContainer);

                }
                await Task.Delay(farmingSetting.WaitTime);
            }
        }

         private async Task<ContainerSettingsList> LoadContainerSettingsList()
        {
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

            return containerSettingsList;
        }
    }
}
