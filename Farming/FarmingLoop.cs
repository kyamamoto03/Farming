using Farming.Model;
using Farming.Services;
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

        private readonly CancellationTokenSource _stoppingCts = new();
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

            sb.Append($"RestartTime:");
            foreach (var i in farmingSetting.RestartTime)
            {
                sb.Append($"{i},");
            }
            sb.AppendLine();

            _logger.LogInformation(sb.ToString());
        }

        private readonly string FARMING_SETTING_INPUT_TYPE_FILE = "file";
        private readonly string FARMING_SETTING_TRUE = "true";

        private readonly string MY_CONTAINER_NAME = "farming";

        private async Task MainLoop()
        {
            _logger.LogInformation("MainLoop Start");

            var containerService = new ContainerService
            {
                MessageCalled = (x => _logger.LogInformation(x))
            };

            //再起動する時間と分を合わせた時刻情報
            var restartTimings = new List<TimeSpan>();
            foreach (var time in farmingSetting.RestartTime)
            {
                try
                {
                    // 時間と分を抽出して整数に変換
                    int hours = ExtractHours(time);
                    int minutes = ExtractMinutes(time);

                    // 結果を表示
                    restartTimings.Add(new(hours, minutes, 0));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"エラーが発生しました: {ex.Message}");
                }
            }

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

                        //配列で指定された再起動時間すべてに対して、今再起動時間かどうかチェックする
                        foreach (var restartTiming in restartTimings)
                        {
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
            var ignoreList = new List<string>
            {
                MY_CONTAINER_NAME
            };

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
            catch (Exception)
            {
                _logger.LogError(farmingSetting.URI + "が取得できません");
                return null;
            }
        }

        /// <summary>
        /// stringから時間を抜き出す
        /// </summary>
        /// <param name="timeString"></param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        private static int ExtractHours(string timeString)
        {
            string[] parts = timeString.Split(':');
            if (parts.Length != 2)
            {
                throw new FormatException("無効な時間形式です。");
            }
            return int.Parse(parts[0]);
        }

        /// <summary>
        /// stringから分を抜き出す
        /// </summary>
        /// <param name="timeString"></param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        private static int ExtractMinutes(string timeString)
        {
            string[] parts = timeString.Split(':');
            if (parts.Length != 2)
            {
                throw new FormatException("無効な時間形式です。");
            }
            return int.Parse(parts[1]);
        }
    }
}
