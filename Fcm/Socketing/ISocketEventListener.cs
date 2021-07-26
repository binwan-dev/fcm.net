using System.Net;
using System.Net.Sockets;

namespace Fcm.Socketing
{
    public interface ISocketEventListener
    {
        void ConnectionFailed(EndPoint remoteEndPoint, SocketError state);

        void ConnectionEstablished();

        void ConnectionShutdown(SocketError state);
    }
}
