using Farming.Model;
using Farming.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        #endregion IHostedService

        #region IDisposable

        public void Dispose()
        {
        }

        #endregion IDisposable

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
            sb.AppendLine($"WaitTime:{farmingSetting.WaitTime}");
            sb.Append($"Ignore:");

            foreach (var i in farmingSetting.Ignore)
            {
                sb.Append($"{i},");
            }
            sb.AppendLine();

            _logger.LogInformation(sb.ToString());
        }

        private string FARMING_SETTING_INPUT_TYPE_FILE = "file";
        private string FARMING_SETTING_TRUE = "true";

        private readonly string MY_CONTAINER_NAME = "farming";

        private async Task MainLoop()
        {
            _logger.LogInformation("MainLoop Start");

            var containerService = new ContainerService();

            containerService.MessageCalled = (x => _logger.LogInformation(x));

            //再起動する時間と分を合わせた時刻情報
            var restartTiming = new TimeSpan(farmingSetting.RestartHour, farmingSetting.RestartMinute, 0);
            //待機時間の半分
            var halfWaitTime = TimeSpan.FromMilliseconds(farmingSetting.WaitTime / 2);

            while (!_stoppingCts.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation($"{DateTime.Now}\tMainLoop");
                    //コンテナ情報読み込み
                    var containerSettingList = await LoadContainerSettingsList();
                    if (containerSettingList != null)
                    {
                        ///jsonにないcontainerを削除ループ
                        var RunningContainers = await containerService.GetAllContainer();
                        foreach (var rc in RunningContainers)
                        {
                            if (!containerSettingList.ContainerSettings.Any(x => $"{x.Image}:{x.Tag}" == $"{rc.Image}") == true)
                            {
                                if (IsIgnoreContainer(rc.Image))
                                {
                                    if (farmingSetting.ContainerRemove == FARMING_SETTING_TRUE)
                                    {
                                        _logger.LogInformation($"Container Stop & Remove:{rc.Image}");
                                        await containerService.StopAndDeleteContainer(rc.ID);
                                    }
                                    else
                                    {
                                        _logger.LogInformation($"Container Stop :{rc.Image}");
                                        await containerService.StopContainer(rc.ID);
                                    }
                                }
                            }
                        }

                        //コンテナ再起動
                        if ((DateTime.Now.TimeOfDay >= restartTiming - halfWaitTime) && (DateTime.Now.TimeOfDay <= restartTiming + halfWaitTime))
                        {
                            foreach (var targetContainer in containerSettingList.ContainerSettings)
                            {
                                var container = await containerService.GetContainer(targetContainer.Image, targetContainer.Tag);
                                _logger.LogInformation("Container Restart : {ContainerImage}", container.Image);
                                await containerService.StopContainer(container.ID);
                                await containerService.StartContainer(targetContainer);
                            }
                        }

                        //起動ループ
                        foreach (var targetContainer in containerSettingList.ContainerSettings)
                        {
                            string target_image = targetContainer.Image;
                            string target_image_tag = targetContainer.Tag;

                            await containerService.StartContainer(targetContainer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
                await Task.Delay(farmingSetting.WaitTime);
            }
        }

        private bool IsIgnoreContainer(string ImageName)
        {
            var ignoreList = new List<string>();
            ignoreList.Add(MY_CONTAINER_NAME);

            foreach (var i in farmingSetting.Ignore)
            {
                ignoreList.Add(i);
            }

            var ret = ignoreList.Any(x => ImageName.Contains(x));

            return !ret;
        }

        /// <summary>
        /// ContainerSetting読み込み
        /// InputTypeが"file"の場合はローカルフォルダから読み込む
        /// </summary>
        /// <returns></returns>
        private async Task<ContainerSettingsList> LoadContainerSettingsList()
        {
            var jsonService = new JsonService<ContainerSettingsList>();
            ContainerSettingsList containerSettingsList;
            try
            {
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
            catch (Exception ex)
            {
                _logger.LogError(farmingSetting.URI + "が取得できません");
                return null;
            }
        }
    }
}
