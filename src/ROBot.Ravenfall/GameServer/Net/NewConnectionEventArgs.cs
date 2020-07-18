﻿using Shinobytes.Ravenfall.RavenNet;

namespace Shinobytes.Ravenfall.RavenNet
{
    public struct NewConnectionEventArgs
    {
        /// <summary>
        /// The data received from the client in the handshake.
        /// </summary>
        public readonly MessageReader HandshakeData;

        /// <summary>
        /// The <see cref="Connection"/> to the new client.
        /// </summary>
        public readonly Connection Connection;

        public NewConnectionEventArgs(MessageReader handshakeData, Connection connection)
        {
            this.HandshakeData = handshakeData;
            this.Connection = connection;
        }
    }
}
