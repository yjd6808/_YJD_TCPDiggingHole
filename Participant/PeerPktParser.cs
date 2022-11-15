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
    public override void Initialize()
    {
        _ptkHandlers.Add(Pkt.Type.P2PMessage, P2PMessage);
    }

    private void P2PMessage(PktBase ptkBase, TcpClientEx peer)
    {
        var pkt = ptkBase as PktP2PMessage;
        Debug.Assert(pkt != null);

        ConsoleEx.WriteLine($"[P2P][{pkt.Sender}]: {pkt.Message}", ConsoleColor.DarkMagenta);
    }
}
