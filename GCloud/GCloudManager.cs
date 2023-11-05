using Google.Cloud.Compute.V1;

namespace Minecraft_Server_Manager.GCloud;

class GCloudManager
{
    public static GCloudManager Instance { get; private set;} = new GCloudManager(GCloudConfiguration.Instance);

    private readonly InstancesClient _instancesClient;

    private readonly string projectId;
    private readonly string zone;
    private readonly string instance;

    public string? ExternalIp { get; private set; } = null;
    private string? Status = null;

    private GCloudManager(GCloudConfiguration config)
    {
        InstancesClientBuilder builder = new()
        {
            CredentialsPath = "credentials.json",
        };
        _instancesClient = builder.Build();

        projectId = config.ProjectID;
        zone = config.Zone;
        instance = config.InstanceID;

        UpdateState().Wait();
    }

    public async Task UpdateState()
    {
        var req = new GetInstanceRequest()
        {
            Project = projectId,
            Zone = zone,
            Instance = instance
        };

        var instanceInfo = await _instancesClient.GetAsync(req);

        ExternalIp = instanceInfo.NetworkInterfaces[0].AccessConfigs[0].NatIP;
        Status = instanceInfo.Status;
    }

    public async Task StartInstance()
    {
        await UpdateState();
        bool waitForState = true;
        do
        {
            switch (Status)
            {
                case "RUNNING":
                    return;
                case "TERMINATED":
                case "SUSPENDED":
                    waitForState = false;
                    break;
                default:
                    await Task.Delay(1000);
                    await UpdateState();
                    break;
            }
        } while (waitForState);

        if (Status == "TERMINATED")
        {
            var req = new StartInstanceRequest()
            {
                Project = projectId,
                Zone = zone,
                Instance = instance
            };
            await _instancesClient.StartAsync(req);
        }
        else if (Status == "SUSPENDED")
        {
            var req = new ResumeInstanceRequest()
            {
                Project = projectId,
                Zone = zone,
                Instance = instance
            };
            await _instancesClient.ResumeAsync(req);
        }
        
        while (Status != "RUNNING")
        {
            await Task.Delay(1000);
            await UpdateState();
        }
    }

    public async Task StopInstance()
    {
        await UpdateState();
        bool waitForState = true;
        do
        {
            switch (Status)
            {
                case "TERMINATED":
                case "SUSPENDED":
                    return;
                case "RUNNING":
                    waitForState = false;
                    break;
                default:
                    await Task.Delay(1000);
                    await UpdateState();
                    break;
            }
        } while (waitForState);

        var req = new StopInstanceRequest()
        {
            Project = projectId,
            Zone = zone,
            Instance = instance
        };
        await _instancesClient.StopAsync(req);
    
        while (Status != "TERMINATED")
        {
            await Task.Delay(1000);
            await UpdateState();
        }
    }
}