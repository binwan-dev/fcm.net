using System.Net;

namespace Fcm.Socketing
{
    public class SocketOptions
    {
        public SocketOptions()
        {
            ReceiveBufferSize = 1024 * 1024 * 2;
        }

        public EndPoint RemoteEndPoint { get; set; }

        public int ReceiveBufferSize { get; set; }

        public int SendingBufferSize { get; set; }
    }
}
