﻿using ROBot.Core;
using Shinobytes.Ravenfall.RavenNet;
using Shinobytes.Ravenfall.RavenNet.Modules;
using Shinobytes.Ravenfall.RavenNet.Packets;
using Shinobytes.Ravenfall.RavenNet.Packets.Client;

namespace ROBot.Ravenfall.GameServer.PacketHandlers
{
    public class BotAuthResponseHandler : INetworkPacketHandler<BotAuthResponse>
    {
        private readonly ILogger logger;
        private readonly IModuleManager moduleManager;

        public BotAuthResponseHandler(ILogger logger, IModuleManager moduleManager)
        {
            this.logger = logger;
            this.moduleManager = moduleManager;
        }

        public void Handle(BotAuthResponse data, IRavenNetworkConnection connection, SendOption sendOption)
        {
            logger.WriteDebug("Login response: " + data.Status);
            var auth = moduleManager.GetModule<Authentication>();
            if (auth != null)
            {
                auth.SetResult(data.Status);
            }
        }
    }
}
