using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections;
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

        private async Task MainLoop()
        {
            string target_image = "nginx";
            string target_image_tag = "latest";
            IList<string> Env = new List<string>();
            Env.Add(@"-v c:\docker\nginx:/usr/share/nginx/html:ro");
            Env.Add(@"-p 80:80");

            while (!_stoppingCts.IsCancellationRequested)
            {
                await StartContainer(target_image, target_image_tag, Env);

                await Task.Delay(5000);
            }
        }
        DockerClient client = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();

        /// <summary>
        /// ImageNameのコンテナを起動
        /// </summary>
        /// <param name="ImageName"></param>
        /// <returns></returns>
        private async Task StartContainer(string ImageName, string ImangeNameTag,IList<string>Env)
        {
            var container = await GetContainer(ImageName, ImangeNameTag);

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
                if (FindImage(ImageName, ImangeNameTag) is null)
                {
                    //Image Pull
                    await PullContainerCommmand(ImageName, ImangeNameTag);
                }
                await RunContainerCommand(ImageName, ImangeNameTag, Env);
            }

        }

        private async Task<ImagesListResponse> FindImage(string ImageName, string ImangeNameTag)
        {
            var imagesListParameters = new ImagesListParameters();
            imagesListParameters.All = true;

            var Images = await client.Images.ListImagesAsync(imagesListParameters);
            return Images.SingleOrDefault(x => x.RepoTags.Contains($"{ImageName}:{ImangeNameTag}"));

        }
        private async Task<bool> PullContainerCommmand(string ImageName, string ImangeNameTag)
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
                        FromImage = ImageName,
                        Tag = ImangeNameTag,
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

        private async Task<ContainerListResponse> GetContainer(string ImageName,string ImageNameTag)
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
        public async Task RunContainerCommand(string ImageName, string ImageNameTas,IList<string> Env)
        {
            try
            {
                var ports = new List<PortBinding>();
                ports.Add(new PortBinding { HostPort = "80" });

                var portBindings = new Dictionary<string, IList<PortBinding>>();
                portBindings.Add("80/tcp", ports);

                var hostConfig = new HostConfig();
                hostConfig.PortBindings = portBindings;
                hostConfig.Binds = new List<string>();
                hostConfig.Binds.Add(@"c:\docker\nginx:/usr/share/nginx/html:ro");

                var cp = new CreateContainerParameters
                {
                    Image = $"{ImageName}:{ImageNameTas}",
                    Env = Env,
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
