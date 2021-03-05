﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Farming.Model
{
    public class ContainerServiceBase
    {
        public ContainerService[] ContainerServices { get; set; }
    }

    public class ContainerService
    {
        private string _Image;
        public string Image
        {
            get => _Image;
            set => _Image = value.ToLower();
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
    }

}
