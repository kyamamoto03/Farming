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

        public string[] Ignore { get; set; } = new string[0];

        public int RestartHour { get; set; } = 1;

        public int RestartMinute { get; set; } = 0;
    }
}
