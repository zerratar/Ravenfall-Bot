﻿using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Shinobytes.Ravenfall.RavenNet.Udp
{
    /// <summary>
    ///     Listens for new UDP connections and creates UdpConnections for them.
    /// </summary>
    /// <inheritdoc />
    public class UdpConnectionListener : NetworkConnectionListener
    {
        public const int BufferSize = ushort.MaxValue;
        public int MinConnectionLength = 0;
        public delegate bool AcceptConnectionCheck(byte[] input, out byte[] response);
        public AcceptConnectionCheck AcceptConnection;
        public int TestDropRate = -1;

        /// <summary>
        ///     The socket listening for connections.
        /// </summary>
        private Socket socket;
        private Action<string> Logger;
        private Timer reliablePacketTimer;
        private int dropCounter = 0;
        private bool disposed;

        private readonly ManualResetEventSlim newMessageEvent = new ManualResetEventSlim(false);
        private readonly ConcurrentQueue<ReceivedMessage> receiveQueue = new ConcurrentQueue<ReceivedMessage>();
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly Thread receiveThread;
        private readonly Thread processThread;

        /// <summary>
        ///     The connections we currently hold
        /// </summary>
        private ConcurrentDictionary<EndPoint, UdpServerConnection> allConnections = new ConcurrentDictionary<EndPoint, UdpServerConnection>();

        public int ConnectionCount { get { return this.allConnections.Count; } }

        /// <summary>
        ///     Creates a new UdpConnectionListener for the given <see cref="IPAddress"/>, port and <see cref="IPMode"/>.
        /// </summary>
        /// <param name="endPoint">The endpoint to listen on.</param>
        public UdpConnectionListener(IPEndPoint endPoint, IPMode ipMode = IPMode.IPv4, Action<string> logger = null)
        {
            this.Logger = logger;
            this.EndPoint = endPoint;
            this.IPMode = ipMode;

            if (this.IPMode == IPMode.IPv4)
                this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new HazelException("IPV6 not supported!");

                this.socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                this.socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            }

            socket.ReceiveBufferSize = BufferSize;
            socket.SendBufferSize = BufferSize;
            receiveThread = new Thread(Receive) { Name = "UdpConnectionListener::Receive Thread" };
            processThread = new Thread(Process) { Name = "UdpConnectionListener::Process Thread" };
            reliablePacketTimer = new Timer(ManageReliablePackets, null, 100, Timeout.Infinite);
        }

        ~UdpConnectionListener()
        {
            this.Dispose(false);
        }

        private void ManageReliablePackets(object state)
        {
            foreach (var kvp in this.allConnections)
            {
                var sock = kvp.Value;
                sock.ManageReliablePackets();
            }

            try
            {
                this.reliablePacketTimer.Change(100, Timeout.Infinite);
            }
            catch { }
        }

        /// <inheritdoc />
        public override void Start()
        {
            try
            {
                socket.Bind(EndPoint);
            }
            catch (SocketException e)
            {
                throw new HazelException("Could not start listening as a SocketException occurred", e);
            }

            receiveThread.Start();
            processThread.Start();
        }

        private void Process()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                while (receiveQueue.TryDequeue(out var message))
                {
                    bool aware = true;
                    bool hasHelloByte = message.Buffer[0] == (byte)UdpSendOption.Hello;
                    bool isHello = hasHelloByte && message.ByteCount >= MinConnectionLength;

                    //If we're aware of this connection use the one already
                    //If this is a new client then connect with them!
                    UdpServerConnection connection;
                    if (!this.allConnections.TryGetValue(message.RemoteEndPoint, out connection))
                    {
                        //Check for malformed connection attempts
                        if (!isHello)
                        {
                            message.Recycle();
                            continue;
                        }

                        if (AcceptConnection != null)
                        {
                            if (!AcceptConnection(message.Buffer, out var response))
                            {
                                message.Recycle();
                                SendData(response, response.Length, message.RemoteEndPoint);
                                continue;
                            }
                        }

                        aware = false;
                        connection = new UdpServerConnection(this, (IPEndPoint)message.RemoteEndPoint, this.IPMode);
                        if (!this.allConnections.TryAdd(message.RemoteEndPoint, connection))
                        {
                            throw new HazelException("Failed to add a connection. This should never happen.");
                        }
                    }

                    //Inform the connection of the buffer (new connections need to send an ack back to client)
                    connection.HandleReceive(message.Message, message.ByteCount);

                    //If it's a new connection invoke the NewConnection event.
                    if (!aware)
                    {
                        // Skip header and hello byte;
                        message.Message.Offset = 4;
                        message.Message.Length = message.ByteCount - 4;
                        message.Message.Position = 0;
                        InvokeNewConnection(message.Message, connection);
                    }
                    else if (isHello || (!isHello && hasHelloByte))
                    {
                        message.Recycle();
                    }

                    //message.Recycle();
                }

                try
                {
                    newMessageEvent.Wait(cancellationTokenSource.Token);
                    newMessageEvent.Reset();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void Receive()
        {
            EndPoint remoteEndPoint = new IPEndPoint(IPMode == IPMode.IPv4 ? IPAddress.Any : IPAddress.IPv6Any, 0);
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                MessageReader message = null;
                try
                {
                    message = MessageReader.GetSized(BufferSize);
                    var bytesReceived = socket.ReceiveFrom(message.Buffer, 0, message.Buffer.Length, SocketFlags.None, ref remoteEndPoint);
                    message.Offset = 0;
                    message.Length = bytesReceived;

                    receiveQueue.Enqueue(new ReceivedMessage(message, bytesReceived, (IPEndPoint)remoteEndPoint));
                    newMessageEvent.Set();
                }
                catch (SocketException sx)
                {
                    message?.Recycle();
                    if (sx.SocketErrorCode == SocketError.Interrupted)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    //If the socket's been disposed then we can just end there.
                    message.Recycle();
                    Logger?.Invoke("Stopped due to: " + ex.Message);
                    return;
                }
            }
        }





        /// <summary>
        ///     Sends data from the listener socket.
        /// </summary>
        /// <param name="bytes">The bytes to send.</param>
        /// <param name="endPoint">The endpoint to send to.</param>
        internal void SendData(byte[] bytes, int length, EndPoint endPoint)
        {
            if (length > bytes.Length) return;

#if DEBUG
            if (TestDropRate > 0)
            {
                if (Interlocked.Increment(ref dropCounter) % TestDropRate == 0)
                {
                    return;
                }
            }
#endif

            try
            {
                socket.BeginSendTo(
                    bytes,
                    0,
                    length,
                    SocketFlags.None,
                    endPoint,
                    SendCallback,
                    null
                );
            }
            catch (SocketException e)
            {
                throw new HazelException("Could not send data as a SocketException occurred.", e);
            }
            catch (ObjectDisposedException)
            {
                //Keep alive timer probably ran, ignore
                return;
            }
        }

        private void SendCallback(IAsyncResult result)
        {
            try
            {
                socket.EndSendTo(result);
            }
            catch { }
        }

        /// <summary>
        ///     Sends data from the listener socket.
        /// </summary>
        /// <param name="bytes">The bytes to send.</param>
        /// <param name="endPoint">The endpoint to send to.</param>
        internal void SendDataSync(byte[] bytes, int length, EndPoint endPoint)
        {
            try
            {
                socket.SendTo(
                    bytes,
                    0,
                    length,
                    SocketFlags.None,
                    endPoint
                );
            }
            catch { }
        }

        /// <summary>
        ///     Removes a virtual connection from the list.
        /// </summary>
        /// <param name="endPoint">The endpoint of the virtual connection.</param>
        internal void RemoveConnectionTo(EndPoint endPoint)
        {
            this.allConnections.TryRemove(endPoint, out var conn);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposed) return;

            foreach (var kvp in this.allConnections)
            {
                kvp.Value.Dispose();
            }


            cancellationTokenSource.Cancel();
            receiveThread.Join();
            processThread.Join();
            cancellationTokenSource.Dispose();

            try { this.socket.Shutdown(SocketShutdown.Both); } catch { }
            try { this.socket.Close(); } catch { }
            try { this.socket.Dispose(); } catch { }

            this.reliablePacketTimer.Dispose();
            this.disposed = true;
            base.Dispose(disposing);
        }


        private class ReceivedMessage
        {
            public readonly MessageReader Message;
            public readonly byte[] Buffer;
            public readonly int ByteCount;
            public readonly IPEndPoint RemoteEndPoint;

            public ReceivedMessage(MessageReader message, int byteCount, IPEndPoint remoteEndPoint)
            {
                this.Message = message;
                this.Buffer = this.Message.Buffer;
                ByteCount = byteCount;
                RemoteEndPoint = remoteEndPoint;
            }

            public void Recycle()
            {
                Message.Recycle();
            }
        }
    }
}
