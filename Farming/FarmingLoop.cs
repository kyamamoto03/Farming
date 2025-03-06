using Amazon;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.Runtime;
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
        private readonly IConfiguration _configuration;

        private readonly string FARMING_SETTING_CONTAINER_HOST_AWS = "AWS";

        private readonly string FARMING_SETTING_INPUT_TYPE_FILE = "file";
        private readonly string FARMING_SETTING_TRUE = "true";
        private readonly string MY_CONTAINER_NAME = "farming";

        // AWS認証情報を保持するプロパティを追加
        private string AwsAccessKeyId { get; set; }

        private string AwsSecretAccessKey { get; set; }
        private string AwsRegion { get; set; }

        public FarmingLoop(ILogger<FarmingLoop> logger, FarmingSetting setting, IConfiguration configuration)
        {
            _logger = logger;
            farmingSetting = setting;
            _configuration = configuration;

            // AWS認証情報を設定ファイルから取得
            AwsAccessKeyId = _configuration["AWS:AccessKeyId"];
            AwsSecretAccessKey = _configuration["AWS:SecretAccessKey"];
            AwsRegion = _configuration["AWS:Region"] ?? "ap-northeast-1"; // デフォルトリージョン

            var sb = new StringBuilder();
            sb.AppendLine($"InputType:{farmingSetting.InputType}");
            sb.AppendLine($"URI:{farmingSetting.URI}");
            sb.AppendLine($"ContainerRemove:{farmingSetting.ContainerRemove}");
            sb.AppendLine($"WaitTime:{farmingSetting.WaitTime}");
            sb.AppendLine($"ContainerHost:{farmingSetting.ContainerHost}");
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

            // AWSの設定情報をログに出力（シークレットキーはマスク）
            if (farmingSetting.ContainerHost == FARMING_SETTING_CONTAINER_HOST_AWS)
            {
                sb.AppendLine($"AWS Region: {AwsRegion}");
                sb.AppendLine($"AWS Access Key ID: {MaskString(AwsAccessKeyId)}");
                sb.AppendLine($"AWS Secret Access Key: {MaskString(AwsSecretAccessKey)}");
            }

            _logger.LogInformation(sb.ToString());
        }

        // 機密情報を一部マスクするヘルパーメソッド
        private static string MaskString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "[未設定]";

            if (input.Length <= 4)
                return "****";

            return String.Concat(input.AsSpan(0, 4), "".PadRight(input.Length - 4, '*'));
        }

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
                    var hours = ExtractHours(time);
                    var minutes = ExtractMinutes(time);

                    // 結果を表示
                    restartTimings.Add(new(hours, minutes, 0));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"時間形式のエラーが発生しました: {ex.Message}");
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
                                    if (container != null)
                                    {
                                        _logger.LogInformation("Container Restart : {ContainerImage}", container.Image);
                                        await containerService.StopContainer(container.ID);

                                        // AWS ECRからの認証処理が必要な場合
                                        if (farmingSetting.ContainerHost == FARMING_SETTING_CONTAINER_HOST_AWS)
                                        {
                                            await AuthenticateToECR(targetContainer);
                                        }

                                        await containerService.StartContainer(targetContainer);
                                    }
                                }
                            }
                        }

                        //起動ループ
                        foreach (var targetContainer in containerSettingList.ContainerSettings)
                        {
                            var target_image = targetContainer.Image;
                            var target_image_tag = targetContainer.Tag;

                            // AWS ECRからのイメージ取得処理を追加
                            if (farmingSetting.ContainerHost == FARMING_SETTING_CONTAINER_HOST_AWS)
                            {
                                // AWS ECRからの認証処理
                                await AuthenticateToECR(targetContainer);
                            }

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

        /// <summary>
        /// AWS ECRへの認証処理を行うメソッド
        /// </summary>
        /// <param name="targetContainer"></param>
        /// <returns></returns>
        private async Task AuthenticateToECR(ContainerSetting targetContainer)
        {
            try
            {
                _logger.LogInformation($"AWSのコンテナ取得開始{targetContainer.Image}:{targetContainer.Tag}");

                // 認証情報の検証
                if (string.IsNullOrEmpty(AwsAccessKeyId) || string.IsNullOrEmpty(AwsSecretAccessKey))
                {
                    _logger.LogWarning("アクセスキーもしくはシークレットキーがありません。");
                }
                else
                {
                    // 明示的に認証情報を指定
                    var credentials = new BasicAWSCredentials(AwsAccessKeyId, AwsSecretAccessKey);
                    var region = RegionEndpoint.GetBySystemName(AwsRegion);

                    // AWS ECRクライアントの作成（認証情報とリージョンを指定）
                    var ecrClient = new AmazonECRClient(credentials, region);
                    await GetECRAuthTokenAndSetToContainer(ecrClient, targetContainer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ECRエラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ECR認証トークンを取得してコンテナ設定に設定するヘルパーメソッド
        /// </summary>
        /// <param name="ecrClient"></param>
        /// <param name="targetContainer"></param>
        /// <returns></returns>
        private async Task GetECRAuthTokenAndSetToContainer(IAmazonECR ecrClient, ContainerSetting targetContainer)
        {
            var authResponse = await ecrClient.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());

            if (authResponse.AuthorizationData.Count > 0)
            {
                var authData = authResponse.AuthorizationData[0];

                // Base64エンコードされたトークンをデコード
                var data = Convert.FromBase64String(authData.AuthorizationToken);
                var decodedToken = Encoding.UTF8.GetString(data);

                // ユーザー名とパスワードを取得（形式: "AWS:password"）
                var parts = decodedToken.Split(':');
                var username = parts[0]; // "AWS"
                var password = parts[1]; // 実際のトークン

                // コンテナ設定に認証情報を設定
                targetContainer.UserName = username;
                targetContainer.Password = password;

                // プロキシエンドポイントからレジストリURLを取得
                var proxyEndpoint = authData.ProxyEndpoint;
                _logger.LogInformation($"ECRに認証しました。: {proxyEndpoint}");

                // イメージ名がECRリポジトリの完全なURLでない場合は、ECRのURLを使用するように調整
                if (!targetContainer.Image.Contains(".dkr.ecr."))
                {
                    var originalImage = targetContainer.Image;
                    // プロキシエンドポイントからホスト部分を抽出（https://を除去）
                    var registryHost = proxyEndpoint.Replace("https://", "");

                    // イメージ名を完全なECRパスに変更
                    targetContainer.Image = $"{registryHost}/{originalImage}";
                    _logger.LogInformation($"イメージ名を {originalImage} から {targetContainer.Image} に修正しました。");
                }
            }
            else
            {
                _logger.LogError("ECRトークンの取得に失敗しました。");
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
            var parts = timeString.Split(':');
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
            var parts = timeString.Split(':');
            if (parts.Length != 2)
            {
                throw new FormatException("無効な時間形式です。");
            }
            return int.Parse(parts[1]);
        }
    }
}
