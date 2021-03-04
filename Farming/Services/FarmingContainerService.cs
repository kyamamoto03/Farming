using Farming.Model;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Farming.Services
{
    public class FarmingContainerService
    {
        public async Task<ContainerServiceBase> FromFile(string JsonFileName)
        {
            var json = await File.ReadAllTextAsync(JsonFileName);

            return JsonSerializer.Deserialize<ContainerServiceBase>(json);

        }
    }
}
