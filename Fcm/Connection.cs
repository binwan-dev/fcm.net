using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Fcm
{
    public delegate void OnConnectedEvent(bool connected);
    public delegate void OnConnectionBreakEvent();
    public delegate void OnSocketExceptionEvent(SocketException ex);

    public class Connection
    {
        private readonly string _host;
        private readonly int _port;
        private Socket _socket;
        private readonly SocketAsyncEventArgs _sendSocketArgs;
        private readonly SocketAsyncEventArgs _receiveSocketArgs;
        public event OnConnectedEvent OnConnectedEvent;
        public event OnConnectionBreakEvent OnConnectionBreadEvent;
        public event OnSocketExceptionEvent OnSocketExceptionEvent;
        private readonly byte[] _receiveBuffer = new byte[1024 * 1024 * 2];
        private readonly IList<byte> _receiveData = new List<byte>();

        public Connection(string serverAddress)
        {
            var arr = serverAddress.Split(':');
            if (arr.Length != 2)
            {
                throw new ArgumentException("The ServerAddress is invalid!");
            }
            _host = arr[0];
            _port = int.Parse(arr[1]);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _sendSocketArgs = new SocketAsyncEventArgs();
            _receiveSocketArgs = new SocketAsyncEventArgs();
            _receiveSocketArgs.Completed += receiveCompleted;
            _receiveSocketArgs.SetBuffer(_receiveBuffer, 0, _receiveBuffer.Length);
        }

        public void Connect()
        {
            var socketArgs = new SocketAsyncEventArgs();
            socketArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(_host), _port);
            socketArgs.Completed += (sender, e) =>
            {
                connectCompleted(sender, e);
            };

            if (!Socket.ConnectAsync(SocketType.Stream, ProtocolType.Tcp, socketArgs))
                connectCompleted(_socket, socketArgs);
        }

        public void Send()
        {
            // _socket.SendAsync();
        }

        private void connectCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (!(sender is Socket socket))
                return;

            try
            {
                OnConnectedEvent?.Invoke(socket.Connected);
                if (!socket.ReceiveAsync(_receiveSocketArgs))
                    receiveCompleted(socket, _receiveSocketArgs);

            }
            catch (SocketException ex)
            {
                socket.Close();
                OnConnectionBreadEvent?.Invoke();
                OnSocketExceptionEvent?.Invoke(ex);
            }
        }

        private void receiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (!(sender is Socket socket))
                return;

            try
            {
                var len = e.BytesTransferred;
                if (len > 0 && e.SocketError != SocketError.Success)
                {
                    socket.Close();
                    OnConnectionBreadEvent?.Invoke();
                    return;
                }


                for (var i = 0; i < len; i++)
                {
                    _receiveData.Add(_buffer[i]);
                }
                while (recvData.Count >= 8)
                {
                    int dataLen = recvData[0] | (recvData[1] >> 8) | (recvData[2] >> 16) | (recvData[3] >> 24);
                    if (recvData.Count - 4 >= dataLen)
                    {
                        int signal = recvData[4] | (recvData[5] >> 8) | (recvData[6] >> 16) | (recvData[7] >> 24);
                        onReceivePackageEvent?.Invoke(signal, recvData.GetRange(8, dataLen - 4).ToArray());
                        recvData.RemoveRange(0, dataLen + 4);
                    }
                    else
                    {
                        break;
                    }
                }
                if (!s.ReceiveAsync(receiveEventArgs))
                {
                    ReceiveEventArgs_Completed(s, e);
                }
            }
            }
            catch (SocketException ex)
            {
                socket.Close();
                OnConnectionBreadEvent?.Invoke();
        OnSocketExceptionEvent?.Invoke(ex);
    }
}
    }
}
