/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/12/2022 10:22:18 PM
 * * * * * * * * * * * * *
 * P2P 메시지 처리
 *
 * * * * * * * * * * * * * 
 */

using System.Diagnostics;

using Shared;

namespace Participant;

public class PeerPktParser : PktParser<TcpClientEx>
{
    public override PktParser<TcpClientEx> Initialize()
    {
        _ptkHandlers.Add(Pkt.Type.P2PEchoMessage, P2PEchoMessage);
        return this;
    }

    private void P2PEchoMessage(PktBase ptkBase, TcpClientEx client)
    {
        var pkt = ptkBase as PktP2PEchoMessage;
        Debug.Assert(pkt != null);

        ConsoleEx.WriteLine($"클라이언트 {pkt.Sender}({client.RemoteEndPoint})로부터 에코 메시지 수신: {pkt.Message}", ConsoleColor.Magenta);

        if (!pkt.Echo)
        {
            pkt.Echo = true;
            client.SendAsync(pkt);
        }
    }
}
