using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Fcm.Logging;
using System.IO;
using System.Threading.Tasks;

namespace Fcm.Socketing
{
    public class ClientSocket
    {
        private readonly Action<byte[]> _onMessageHandler;
        private readonly ILogger<ClientSocket> _log;
        private readonly SocketOptions _options;
        private readonly IEnumerable<ISocketEventListener> _eventListener;
        private Socket _socket;
        private ManualResetEvent _connectEvent;
        private static int _recving = 0;
        private static int _sending = 0;
        private static int _closing = 0;
        private readonly byte[] _recvBuffer;
        private MemoryStream _sendingStream;
        private readonly List<byte> _recvData;
        private readonly ConcurrentQueue<PackSenderData> _sendingQueue;
        private SocketAsyncEventArgs _recvArgs;
        private SocketAsyncEventArgs _sendArgs;

        public ClientSocket(
            Action<byte[]> onMessageHandler,
            ILogger<ClientSocket> log,
            SocketOptions options,
            IEnumerable<ISocketEventListener> eventListener = null)
        {
            _onMessageHandler = onMessageHandler;
            _log = log;
            _options = options ?? throw new ArgumentNullException("SocketOptions cannot be null!");

            _recvBuffer = new byte[_options.ReceiveBufferSize];
            _sendingStream = new MemoryStream();
            _recvData = new List<byte>();
            _sendingQueue = new ConcurrentQueue<PackSenderData>();
        }

        public void Start(int timeoutMilliSeconds = 5000)
        {
            _connectEvent = new ManualResetEvent(false);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            var socketArgs = new SocketAsyncEventArgs();
            socketArgs.RemoteEndPoint = _options.RemoteEndPoint;
            socketArgs.Completed += tryConnect;

            try
            {
                if (!_socket.ConnectAsync(socketArgs))
                    tryConnect(_socket, socketArgs);
                else
                    _connectEvent.WaitOne(timeoutMilliSeconds);
            }
            catch (TimeoutException ex)
            {
                _log.Error(ex, "Server[{0}] connect timeout! timeout expired millisecond {1}", _options.RemoteEndPoint, timeoutMilliSeconds);
                onConnectFailed(SocketError.TimedOut);
                socketArgs = null;
            }
        }

        public void SendMessage(byte[] msg)
        {
            if (msg == null || msg.Length == 0)
                return;

            var package = new PackSenderData(msg);
            _sendingQueue.Enqueue(package);

            trySend();
        }

        private void tryConnect(object sender, SocketAsyncEventArgs args)
        {
            _socket = (Socket)sender;

            if (args.SocketError != SocketError.Success)
            {
                _log.Error("Server[{0}] connect failed! State[{1}]", _options.RemoteEndPoint, args.SocketError.ToString());
                onConnectFailed(args.SocketError);
                args.Dispose();
                _connectEvent.Set();
                return;
            }

            _log.Info("Server[{0}] connected!", _options.RemoteEndPoint);
            _connectEvent.Set();
            onConnected();

            trySend();
            tryReceive();
        }

        private void trySend()
        {
            if (Interlocked.CompareExchange(ref _sending, 1, 0) != 0)
                return;

            if (_sendArgs == null)
            {
                _sendArgs = new SocketAsyncEventArgs();
                _sendArgs.Completed += send;
            }

            Task.Factory.StartNew(sendMsg);
        }

        private void sendMsg()
        {
            if (isClosing()) return;

            _sendingStream.SetLength(0);
            while (true)
            {
                if (!_sendingQueue.TryDequeue(out PackSenderData package))
                    break;

                _sendingStream.Write(package.DataCount, 0, package.DataCount.Length);
                _sendingStream.Write(package.SendingData, 0, package.SendingData.Length);

                if (_sendingStream.Length >= _options.SendingBufferSize)
                    break;
            }

            if (_sendingQueue.Count == 0 && _sendingStream.Length == 0)
            {
                Interlocked.Exchange(ref _sending, 0);
                trySend();
                return;
            }

            _sendArgs.SetBuffer(_sendingStream.GetBuffer(), 0, (int)_sendingStream.Length);
            if (_socket.SendAsync(_sendArgs))
                send(_socket, _sendArgs);
        }

        private void send(object sender, SocketAsyncEventArgs args)
        {
            try
            {
                if (args.SocketError != SocketError.Success)
                {
                    close(args.SocketError, null, "Send data failed! SockerError[{0}]", args.SocketError.ToString());
                    return;
                }
            }
            catch (Exception ex)
            {
                close(SocketError.Shutdown, ex, "Send data failed! Exception[{0}]", ex.Message);
            }
            finally
            {
                sendMsg();
            }
        }

        private void tryReceive()
        {
            if (Interlocked.CompareExchange(ref _recving, 1, 0) != 0 || isClosing())
                return;

            if (_recvArgs == null)
            {
                _recvArgs = new SocketAsyncEventArgs();
                _recvArgs.Completed += receive;
            }
            _recvArgs.SetBuffer(_recvBuffer, 0, _recvBuffer.Length);

            if (_socket.ReceiveAsync(_recvArgs))
                receive(_socket, _recvArgs);
        }

        private void receive(object sender, SocketAsyncEventArgs args)
        {
            try
            {
                if (args.SocketError != SocketError.Success)
                {
                    close(args.SocketError, null, "Receive data failed! Socket state[{0}]", args.SocketError);
                    return;
                }

                parseData(args);
            }
            catch (Exception ex)
            {
                close(SocketError.Shutdown, ex, "Receive data failed! error[{0}]! more see exception...", ex.Message);
            }
            finally
            {
                args.SetBuffer(null, 0, 0);
                Interlocked.Exchange(ref _recving, 0);
            }
        }

        private void parseData(SocketAsyncEventArgs args)
        {
            foreach (var item in args.BufferList)
            {
                _recvData.AddRange(item.Array);
                if (_recvData.Count >= 8)
                {
                    var dataLen = _recvData[0] |
                        (_recvData[1] >> 8) |
                        (_recvData[2] >> 16) |
                        (_recvData[3] >> 24) |
                        (_recvData[4] >> 32) |
                        (_recvData[5] >> 40) |
                        (_recvData[6] >> 48) |
                        (_recvData[7] >> 56);
                    if (_recvData.Count > dataLen + 8)
                    {
                        _log.Debug("Receive new msg! msg length: {0}", dataLen);
                        _onMessageHandler?.Invoke(_recvData.GetRange(8, dataLen).ToArray());
                        _recvData.RemoveRange(0, dataLen + 8);
                    }
                }
            }
        }

        private struct PackSenderData
        {
            public PackSenderData(byte[] sendingData)
            {
                SendingData = sendingData;

                DataCount = new byte[8];
                DataCount[0] = (byte)sendingData.Length;
                DataCount[1] = (byte)(sendingData.Length >> 8);
                DataCount[2] = (byte)(sendingData.Length >> 16);
                DataCount[3] = (byte)(sendingData.Length >> 24);
                DataCount[4] = (byte)(sendingData.Length >> 32);
                DataCount[5] = (byte)(sendingData.Length >> 40);
                DataCount[6] = (byte)(sendingData.Length >> 48);
                DataCount[7] = (byte)(sendingData.Length >> 56);
            }

            public byte[] SendingData { get; set; }

            public byte[] DataCount { get; set; }
        }

        private void close(SocketError state, Exception ex, string msg, params object[] msgParam)
        {
            if (Interlocked.CompareExchange(ref _closing, 1, 0) != 0)
                return;

            try
            {
                if (_sendArgs != null)
                {
                    _sendArgs.SetBuffer(null, 0, 0);
                    _sendArgs.Completed -= send;
                    _sendArgs.Dispose();
                    _sendArgs = null;
                }
                if (_recvArgs != null)
                {
                    _recvArgs.SetBuffer(null, 0, 0);
                    _recvArgs.Completed -= receive;
                    _recvArgs.Dispose();
                    _recvArgs = null;
                }

                if (_socket != null)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    _socket.Close(10000);
                    _socket.Dispose();
                    _socket = null;
                }

                if (ex != null)
                    _log.Error(ex, "Socket handle error! Exception[{0}]", ex.Message);
                else
                    _log.Warning("Socket was shutdown!");

                onConnectShutdown(state);
            }
            catch (Exception cex)
            {
                _log.Error(cex, "Socket handle closing has error! Exception[{0}]", cex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _closing, 0);
            }
        }

        private bool isClosing() => _closing == 1;

        private void onConnected()
        {
            foreach (var listener in _eventListener)
            {
                try
                {
                    listener.ConnectionEstablished();
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Socket connection establish notify has fatal error! Listener[{0}], Exception[{1}]", listener.GetType().FullName, ex.Message);
                }
            }
        }

        private void onConnectFailed(SocketError state)
        {
            foreach (var listener in _eventListener)
            {
                try
                {
                    listener.ConnectionFailed(_options.RemoteEndPoint, state);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Socket connection failed notify has fatal error! Listener[{0}], Exception[{1}]", listener.GetType().FullName, ex.Message);
                }
            }
        }

        private void onConnectShutdown(SocketError state)
        {
            foreach (var listener in _eventListener)
            {
                try
                {
                    listener.ConnectionShutdown(state);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Socket connection shutdown notify has fatal error! Listener[{0}], Exception[{1}]", listener.GetType().FullName, ex.Message);
                }
            }
        }

    }

}
