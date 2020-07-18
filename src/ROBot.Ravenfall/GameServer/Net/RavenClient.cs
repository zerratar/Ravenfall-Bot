using ROBot.Core;
using Shinobytes.Ravenfall.RavenNet.Modules;
using Shinobytes.Ravenfall.RavenNet.Packets;
using System;
using System.Linq;
using System.Net;
using System.Reflection;


namespace Shinobytes.Ravenfall.RavenNet
{
    public class RavenClient : IRavenClient
    {
        private readonly ILogger logger;
        private readonly Authentication auth;
        private readonly object connectionMutex = new object();

        private RavenNetworkConnection client;

        public event EventHandler Disconnected;
        public event EventHandler Connected;

        public IModuleManager Modules { get; }

        private readonly INetworkPacketController packetHandlers;

        public Authentication Auth => auth;
        public bool IsConnected => client.IsConnected;
        public bool IsConnecting => client.IsConnecting;

        public RavenClient(ILogger logger, IModuleManager moduleManager, INetworkPacketController controller)
        {
            this.logger = logger;
            this.Modules = moduleManager;
            this.auth = this.Modules.AddModule(new Authentication(this));
            this.packetHandlers = RegisterPacketHandlers(controller);
        }

        private void OnConnected(object sender, EventArgs e)
        {
            Connected?.Invoke(this, e);
        }

        private void OnDisconnected(object sender, EventArgs e)
        {
            Disconnected?.Invoke(this, e);
        }

        public void Send<T>(short packetId, T packet, SendOption sendOption)
        {
            client.Send(packetId, packet, sendOption);
        }

        public void Send<T>(T packet, SendOption sendOption)
        {
            client.Send(packet, sendOption);
        }

        public void ConnectAsync(IPAddress address, int port)
        {
            lock (connectionMutex)
            {
                CreateClient();

                client.ConnectAsync(address, port);
            }
        }

        public bool Connect(IPAddress address, int port)
        {
            lock (connectionMutex)
            {
                CreateClient();

                if (client.Connect(address, port))
                {
                    logger.WriteDebug("Connected to GS");
                    return true;
                }

                logger.WriteError("Unable to connect to server.");
                return false;
            }
        }

        public void Dispose()
        {
            this.client.Connected -= this.OnConnected;
            this.client.Disconnected -= this.OnDisconnected;

            Modules.Dispose();
            client.Dispose();
        }

        private void CreateClient()
        {
            if (this.client != null)
            {
                this.client.Connected -= this.OnConnected;
                this.client.Disconnected -= this.OnDisconnected;
            }

            this.client = new RavenNetworkConnection(logger, packetHandlers);
            this.client.Disconnected += OnDisconnected;
            this.client.Connected += OnConnected;
        }

        private static INetworkPacketController RegisterPacketHandlers(INetworkPacketController controller)
        {
            var packetHandlers = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(x => !x.IsAbstract && typeof(INetworkPacketHandler).IsAssignableFrom(x))
                .ToArray();

            foreach (var handler in packetHandlers)
            {
                var declaringType = handler.GetInterfaces().OrderByDescending(x => x.FullName).FirstOrDefault();
                var packetType = declaringType.GetGenericArguments().FirstOrDefault();
                var packetId = (short)packetType.GetField("OpCode").GetValue(null);
                controller.Register(packetType, handler, packetId);
            }

            return controller;
        }
    }
}