using Docker.DotNet;
using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Farming.Services
{
    public class ContainerService
    {
        private readonly string DOCKER_STATE_RUNNING = "running";
        private DockerClient client = new DockerClientConfiguration().CreateClient();

        public Action<string> MessageCalled;
        public ContainerService()
        {
        }

        /// <summary>
        /// ImageNameのコンテナを起動
        /// </summary>
        /// <param name="ImageName"></param>
        /// <returns></returns>
        public async Task StartContainer(Model.ContainerSetting targetContainer)
        {
            var container = await GetContainer(targetContainer.Image, targetContainer.Tag);

            if (container is not null && await IsExist(container.ID) == true)
            {
                if (await IsRunning(container.ID) == false)
                {
                    MessageCalled?.Invoke($"Start Container:{container.Image} Name:{targetContainer.Name}");

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
                await NetworkConstruction(targetContainer);

                await RunContainerCommand(targetContainer);
            }
        }

        private async Task NetworkConstruction(Model.ContainerSetting targetContainer)
        {
            if (targetContainer.Networks != null && targetContainer.Networks.Count() > 0)
            {
                var ExistNetworks = await client.Networks.ListNetworksAsync();

                foreach (string network in targetContainer.Networks)
                {
                    if (!ExistNetworks.Any(x => network == x.Name))
                    {
                        var param = new NetworksCreateParameters() { Name = targetContainer.Networks[0] };

                        await client.Networks.CreateNetworkAsync(param);
                    }
                }
            }
        }

        private async Task<ImagesListResponse> FindImage(Model.ContainerSetting targetContainer)
        {
            var imagesListParameters = new ImagesListParameters();
            imagesListParameters.All = true;

            var Images = await client.Images.ListImagesAsync(imagesListParameters);
            var find = Images.SingleOrDefault(x => x.RepoTags != null && x.RepoTags.Contains($"{targetContainer.Image}:{targetContainer.Tag}"));

            return find;

        }

        private async Task<bool> PullContainerCommmand(Model.ContainerSetting targetContainer)
        {
            var progressJSONMessage = new ProgressJSONMessage
            {
                _onJSONMessageCalled = (m) =>
                {
                    // Status could be 'Pulling from...'
                    MessageCalled?.Invoke($"{m.ID} - {m.Status} {m.From} - {m.Stream}");
                }
            };

            try
            {
                AuthConfig? authConfig = null;
                if (targetContainer.UserName is { Length: > 0 } && targetContainer.Password is { Length: > 0 })
                {
                    //認証情報を設定
                    authConfig = new AuthConfig
                    {
                        Username = targetContainer.UserName
                        ,
                        Password = targetContainer.Password
                    };
                }

                await client.Images.CreateImageAsync(
                    new ImagesCreateParameters
                    {
                        FromImage = targetContainer.Image,
                        Tag = targetContainer.Tag,
                    },
                    authConfig,
                    progressJSONMessage);
            }
            catch (Exception ex)
            {
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

        public async Task<IList<ContainerListResponse>> GetAllContainer()
        {
            IList<ContainerListResponse> containers = await client.Containers.ListContainersAsync(
                new ContainersListParameters()
                {
                    Limit = 100,
                });

            return containers;
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

        public async Task StopContainer(string id)
        {
            await client.Containers.StopContainerAsync(id, new ContainerStopParameters());

        }

        public async Task StopAndDeleteContainer(string id)
        {
            await StopContainer(id);
            await client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true });

        }
        public async Task RunContainerCommand(Model.ContainerSetting targetContainer)
        {
            MessageCalled?.Invoke($"Run Container:{targetContainer.Image}");

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
                    hostConfig.DNS = new List<string>();
                    hostConfig.DNSOptions = new List<string>();
                    hostConfig.DNSOptions = new List<string>();
                }

                if (targetContainer.Volumes is not null)
                {
                    hostConfig.Binds = new List<string>();
                    targetContainer.Volumes.ToList().ForEach(x => hostConfig.Binds.Add(x));
                }

                var networkConfig = new NetworkingConfig();
                if (targetContainer.Networks is not null)
                {
                    networkConfig.EndpointsConfig = new Dictionary<string, EndpointSettings>();

                    string networkName = targetContainer.Networks[0];
                    var endpointSetting = new EndpointSettings();
                    networkConfig.EndpointsConfig.Add(networkName, endpointSetting);
                    hostConfig.NetworkMode = networkName;
                }

                if (string.IsNullOrEmpty(targetContainer.Ulimits) == false)
                {
                    hostConfig.Ulimits = new List<Ulimit>();
                    hostConfig.Ulimits.Add(MakeUlimit(targetContainer.Ulimits));

                }

                var cp = new CreateContainerParameters
                {
                    Image = $"{targetContainer.Image}:{targetContainer.Tag}",
                    Name = $"{targetContainer.Name}",
                    Env = targetContainer.Envs,
                    HostConfig = hostConfig,
                    NetworkingConfig = networkConfig

                };
                var response = await client.Containers.CreateContainerAsync(cp);
                await StartContainerCommand(response.ID);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Ulimitsを作成
        /// 現状memlockしかなさそうなので、固定で作成
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        private Ulimit MakeUlimit(string d)
        {
            Ulimit ret = new();

            int Hard;
            int Soft;
            try
            {
                var sp = d.Split(",");
                Hard = Int32.Parse(sp[0]);
                Soft = Int32.Parse(sp[1]);
            }
            catch
            {
                throw new Exception("Invalid Ulimits");
            }
            ret.Name = "memlock";
            ret.Hard = Hard;
            ret.Soft = Soft;
            return ret;
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
