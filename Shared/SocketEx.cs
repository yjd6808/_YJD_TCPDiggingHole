/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/15/2022 10:29:42 PM
 * * * * * * * * * * * * *
 * 소켓 확장기능 추가
 * 
 */

using System.Net;
using System.Net.Sockets;

namespace Shared
{
    public static class SocketEx
    {

        public static void BindV4(this Socket socket, int port)
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
        }

        public static Socket CreateTcpSocket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public static void SetkeepAlive(this Socket socket, bool value)
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, value);
        }

        public static bool GetkeepAlive(this Socket socket)
        {
            return (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive)! != 0;
        }

        public static void SetReuseAddress(this Socket socket, bool value)
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, value);
        }

        public static bool GetReuseAddress(this Socket socket)
        {
            return (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress)! != 0;
        }

        public static int GetEphemeralPort()
        {
            TcpListener l = new(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
