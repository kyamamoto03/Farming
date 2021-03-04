using System;
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
        public string Image { get; set; }
        public string Tag { get; set; }
        public string[] Ports { get; set; }
        public string[] Volumes { get; set; }
        public string[] Envs { get; set; }
    }

}
