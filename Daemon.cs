using NurApiDotNet;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace daemon
{
    public class Daemon : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IOptions<DaemonConfig> _config;
        public Daemon(ILogger<Daemon> logger, IOptions<DaemonConfig> config)
        {
            _logger = logger;
            _config = config;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting daemon...");
            try
            {
                hNur = new NurApi();

                hNur.ConnectedEvent += new EventHandler<NurApi.NurEventArgs>(hNur_ConnectedEvent);
                hNur.DisconnectedEvent += new EventHandler<NurApi.NurEventArgs>(hNur_DisconnectedEvent);

                hNur.InventoryStreamEvent += new EventHandler<NurApi.InventoryStreamEventArgs>(hNur_InventoryStreamReady);

                if (hNur.IsConnected())
                {
                    hNur.Disconnect();
                }
                _logger.LogInformation($"Trying to connect to reader at {_config.Value.ReaderHost}:{_config.Value.ReaderPort}");
                hNur.ConnectSocket(_config.Value.ReaderHost, _config.Value.ReaderPort);

                if (!hNur.IsConnected())
                {
                    throw new Exception("Not connected to reader");
                }

                hNur.ClearTags();
                hNur.StartInventoryStream();
                running = true;
            }
            catch (Exception ex)
            {
                running = false;
                _logger.LogError("Could not initialize NurApi, error: " + ex.ToString());
                Environment.Exit(-1);
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping daemon...");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing...");
        }

        Boolean running = false;

        NurApi? hNur = null;

        NurApi.TagStorage tags = new NurApi.TagStorage();

        void hNur_ConnectedEvent(object sender, NurApi.NurEventArgs e)
        {
            _logger.LogInformation("Connected to reader");
            hNur?.SetLogLevel(NurApi.LOG_ERROR);
            // hNur.TxLevel = 0;   // Set Tx power to max level
        }

        void hNur_DisconnectedEvent(object sender, NurApi.NurEventArgs e)
        {
            // hNur.Connect();
        }

        void hNur_InventoryStreamReady(object sender, NurApi.InventoryStreamEventArgs e)
        {
            try
            {
                NurApi.TagStorage intTagStorage = hNur.GetTagStorage();
                lock (intTagStorage)
                {
                    for (int i = 0; i < intTagStorage.Count; i++)
                    {
                        NurApi.Tag tag;
                        if (tags.AddTag(intTagStorage[i], out tag))
                        {
                            _logger.LogDebug("New Tag - EPC: {0}, ANT: {1}, RSSI: {2}",
                                tag.GetEpcString(), tag.antennaId, tag.rssi);
                        }
                        else
                        {
                            _logger.LogDebug("Known Tag - EPC: {0}, ANT: {1}, RSSI: {2}",
                                tag.GetEpcString(), tag.antennaId, tag.rssi);
                        }
                    }
                    hNur.ClearTags();
                }

                if (e.data.stopped && running)
                {
                    hNur.StartInventoryStream();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Inventory error: " + ex.Message);
            }
        }
    }
}

