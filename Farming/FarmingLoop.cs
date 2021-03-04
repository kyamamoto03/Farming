using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Farming
{
    public class FarmingLoop : IHostedService, IDisposable
    {
        #region IHostedService
        private readonly CancellationTokenSource _stoppingCts = new CancellationTokenSource();
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
                    Console.WriteLine($"{ex}");
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
                await Task.WhenAny(_executingTask, Task.Delay(5000,cancellationToken));
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {

        }
        #endregion
        private readonly string DOCKER_STATE_RUNNING = "running";
        private readonly string DOCKER_STATE_EXIT = "exited";

        private async Task MainLoop()
        {
            string target_image = "nginx";

            while (!_stoppingCts.IsCancellationRequested)
            {
                var container = await GetContainer(target_image);
                string target_container = container.ID;

                if (await IsExist(target_container) == true)
                {
                    if (await IsRunning(target_container) == false)
                    {
                        
                        await StartContainer(target_container);
                    }
                }

                await Task.Delay(5000);
            }
        }
        DockerClient client = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();

        private async Task<ContainerListResponse> GetContainer(string ImageName)
        {
            IList<ContainerListResponse> containers = await client.Containers.ListContainersAsync(
                new ContainersListParameters()
                {
                    Limit = 100,
                });

            return containers.SingleOrDefault(x => x.Image == ImageName);
        }
        private async Task<bool> IsRunning(string id)
        {
            IList<ContainerListResponse> containers = await client.Containers.ListContainersAsync(
                new ContainersListParameters()
                {
                    Limit = 100,
                });

            if (containers.Any(x => x.ID == id) == true)
            {
                var container = containers.Single(x => x.ID == id);
                if (container.State == DOCKER_STATE_RUNNING)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            throw new Exception("Non Exist Container");
        }

        private async Task<bool> IsExist(string id)
        {
            IList<ContainerListResponse> containers = await client.Containers.ListContainersAsync(
                new ContainersListParameters()
                {
                    Limit = 100,
                });

            if (containers.Any(x => x.ID == id) == true)
            {
                return true;
            }
            return false;
        }

        private async Task<bool> StartContainer(string id)
        {
            var ret = await client.Containers.StartContainerAsync(id,new ContainerStartParameters());
            return ret;
        }
    }
}
