using NurApiDotNet;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using SocketIOClient;

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

        private Timer timer;

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
                _logger.LogInformation($"Reader - Trying to connect to reader at {_config.Value.ReaderHost}:{_config.Value.ReaderPort}");
                hNur.ConnectSocket(_config.Value.ReaderHost, _config.Value.ReaderPort);

                if (!hNur.IsConnected())
                {
                    throw new Exception("Reader - Not connected to reader");
                }

                hNur.ClearTags();
                hNur.StartInventoryStream();
                running = true;

                timer = new Timer(new TimerCallback((object obj) =>
                {
                    tags.Clear();
                }), null, 0, 5000);

                SetupWebSocketClient().Wait();
            }
            catch (Exception ex)
            {
                running = false;
                _logger.LogError("Reader - Could not initialize NurApi, error: " + ex.ToString());
                Environment.Exit(-1);
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping daemon...");
            hNur.Disconnect();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing...");
        }

        Boolean running = false;

        NurApi hNur = null;

        NurApi.TagStorage tags = new NurApi.TagStorage();

        void hNur_ConnectedEvent(object sender, NurApi.NurEventArgs e)
        {
            _logger.LogInformation("Reader - Connected to reader");
            hNur.TxLevel = _config.Value.TxLevel;
        }

        void hNur_DisconnectedEvent(object sender, NurApi.NurEventArgs e)
        {
            hNur.Connect();
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
                            var message = new
                            {
                                rfid = tag.GetEpcString(),
                                controlPointId = _config.Value.ControlPointId
                            };
                            _logger.LogInformation(message.rfid);
                            Task.Run(async () => await webSocketClient.EmitAsync("rfid", message));
                        }
                        hNur.ClearTags();
                    }
                }

                if (e.data.stopped && running)
                {
                    hNur.StartInventoryStream();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Reader - Inventory error: " + ex.Message);
            }
        }

        private SocketIO webSocketClient = null;
        private async Task SetupWebSocketClient()
        {
            webSocketClient = new SocketIO(_config.Value.GatewayAddress);
            webSocketClient.OnConnected += (sender, e) =>
            {
                _logger.LogInformation("WebSocketClient connected");
            };
            await webSocketClient.ConnectAsync();
        }
    }
}

