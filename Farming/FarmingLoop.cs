using Docker.DotNet;
using Docker.DotNet.Models;
using Farming.Model;
using Farming.Services;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                await Task.WhenAny(_executingTask, Task.Delay(5000, cancellationToken));
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
        public string FarmingContainerFileName = "FarmingContainer.json";

        private async Task MainLoop()
        {
            var farmingContainerService = new FarmingContainerService();

            var targetContainers = await farmingContainerService.FromFile(FarmingContainerFileName);

            while (!_stoppingCts.IsCancellationRequested)
            {
                foreach (var targetContainer in targetContainers.ContainerServices)
                {
                    string target_image = targetContainer.Image;
                    string target_image_tag = targetContainer.Tag;


                    await StartContainer(targetContainer);

                }
                await Task.Delay(5000);
            }
        }
        DockerClient client = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();

        /// <summary>
        /// ImageNameのコンテナを起動
        /// </summary>
        /// <param name="ImageName"></param>
        /// <returns></returns>
        private async Task StartContainer(ContainerService targetContainer)
        {
            var container = await GetContainer(targetContainer.Image, targetContainer.Tag);

            if (container is not null && await IsExist(container.ID) == true)
            {
                if (await IsRunning(container.ID) == false)
                {

                    await StartContainerCommand(container.ID);
                }
            }
            else
            {
                //Container Not Found
                if (await FindImage(targetContainer) is null)
                {
                    //Image Pull
                    await PullContainerCommmand(targetContainer);
                }
                await RunContainerCommand(targetContainer);
            }

        }

        private async Task<ImagesListResponse> FindImage(ContainerService targetContainer)
        {
            var imagesListParameters = new ImagesListParameters();
            imagesListParameters.All = true;

            var Images = await client.Images.ListImagesAsync(imagesListParameters);
            var find = Images.SingleOrDefault(x => x.RepoTags.Contains($"{targetContainer.Image}:{targetContainer.Tag}"));

            return find;

        }
        private async Task<bool> PullContainerCommmand(ContainerService targetContainer)
        {
            var progressJSONMessage = new ProgressJSONMessage
            {
                _onJSONMessageCalled = (m) =>
                {
                    // Status could be 'Pulling from...'
                    Console.WriteLine($"{System.Reflection.MethodInfo.GetCurrentMethod().Module}->{System.Reflection.MethodInfo.GetCurrentMethod().Name}: _onJSONMessageCalled - {m.ID} - {m.Status} {m.From} - {m.Stream}");
                }
            };

            try
            {
                await client.Images.CreateImageAsync(
                    new ImagesCreateParameters
                    {
                        FromImage = targetContainer.Image,
                        Tag = targetContainer.Tag,
                    },
                    null,
                    progressJSONMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
            return true;
        }

        private async Task<ContainerListResponse> GetContainer(string ImageName, string ImageNameTag)
        {
            IList<ContainerListResponse> containers = await client.Containers.ListContainersAsync(
                new ContainersListParameters()
                {
                    Limit = 100,
                });

            return containers.SingleOrDefault(x => x.Image == $"{ImageName}:{ImageNameTag}");
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

        private async Task<bool> StartContainerCommand(string id)
        {
            var ret = await client.Containers.StartContainerAsync(id, new ContainerStartParameters());
            return ret;
        }
        public async Task RunContainerCommand(ContainerService targetContainer)
        {
            try
            {
                var hostConfig = new HostConfig();


                var portBindings = new Dictionary<string, IList<PortBinding>>();
                if (targetContainer.Ports is not null)
                {
                    foreach (var p in targetContainer.Ports)
                    {
                        var ps = p.Split(':');

                        var ports = new List<PortBinding>();
                        ports.Add(new PortBinding { HostPort = ps[1] });

                        portBindings.Add(ps[0], ports);

                    }

                    hostConfig.PortBindings = portBindings;
                }

                if (targetContainer.Volumes is not null)
                {
                    hostConfig.Binds = new List<string>();
                    targetContainer.Volumes.ToList().ForEach(x => hostConfig.Binds.Add(x));
                }

                var cp = new CreateContainerParameters
                {
                    Image = $"{targetContainer.Image}:{targetContainer.Tag}",
                    Env = targetContainer.Envs,
                    HostConfig = hostConfig

                };
                var response = await client.Containers.CreateContainerAsync(cp);
                await StartContainerCommand(response.ID);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }


        private class ProgressJSONMessage : IProgress<JSONMessage>
        {
            internal Action<JSONMessage> _onJSONMessageCalled;

            void IProgress<JSONMessage>.Report(JSONMessage value)
            {
                _onJSONMessageCalled(value);
            }
        }

    }
}
