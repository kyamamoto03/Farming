using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
