using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Farming.Model
{
    public class ContainerSettingsList
    {
        public ContainerSetting[] ContainerSettings { get; set; }
    }

    public class ContainerSetting
    {
        private string _Image;
        public string Image
        {
            get => _Image;
            set => _Image = value.ToLower();
        }
        private string _Name;
        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(_Name) == true)
                {
                    return Image;
                }
                return _Name;
            }
            set => _Name = value.ToLower();
        }
        private string _Tag;
        public string Tag
        {
            get => _Tag;
            set => _Tag = value.ToLower();
        }

        public string[] Ports { get; set; }
        public string[] Volumes { get; set; }
        public string[] Envs { get; set; }
        public string[] Networks { get; set; }
        public string Ulimits { get; set; }

        /// <summary>
        /// ContainerRegistryログインユーザ名
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// ContainerRegistryログインパスワード
        /// </summary>
        public string? Password { get; set; }
    }

}
