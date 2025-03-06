namespace Farming.Model
{
    public class FarmingSetting
    {
        public string _InputType;

        public string InputType
        {
            get => _InputType;
            set => _InputType = value.ToLower();
        }

        public string URI { get; set; }

        private string _ContainerRemove;

        public string ContainerRemove
        {
            get => _ContainerRemove;
            set => _ContainerRemove = value.ToLower();
        }

        public int WaitTime { get; set; } = 5000;

        public string[] Ignore { get; set; } = System.Array.Empty<string>();

        public string[] RestartTime { get; set; } = ["1:00"];

        /// <summary>
        /// コンテナのホスト
        /// AWSならAWSそれ以外ならDockerHub等
        /// </summary>
        public string ContainerHost { get; set; } = string.Empty;

        /// <summary>
        /// AWSアクセスキー
        /// </summary>
        public string AwsAccessKeyId { get; set; }

        /// <summary>
        /// AWSシークレットアクセスキー
        /// </summary>
        public string AwsSecretAccessKey { get; set; }

        /// <summary>
        /// リージョン
        /// </summary>
        public string AwsRegion { get; set; } = "ap-northeast-1";
    }
}
