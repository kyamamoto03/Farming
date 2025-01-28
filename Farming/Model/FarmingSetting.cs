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

        public string[] RestartTime { get; set; } = new string[1] { "1:00" };
    }
}
