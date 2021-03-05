using Farming.Model;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Farming.Services
{
    public class FarmingContainerService<T>
    {
        public async Task<T> FromFile(string JsonFileName)
        {
            var json = await File.ReadAllTextAsync(JsonFileName);

            return JsonSerializer.Deserialize<T>(json);

        }

        public async Task<T> FromURL(string URL)
        {
            HttpClient client = new HttpClient();
            var json = await client.GetStringAsync(URL);

            var ret = JsonSerializer.Deserialize<T>(json);

            return ret;
        }
    }
}
