/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/12/2022 10:22:18 PM
 * * * * * * * * * * * * *
 * 중개 서버(Introducer)로부터 메시지 처리
 *
 * * * * * * * * * * * * * 
 */

using System.Diagnostics;
using System.Net;

using Shared;

namespace Participant;

public class ParticipantPktParser : PktParser<TcpParticipant>
{
    public override void Initialize()
    {
        _ptkHandlers.Add(Pkt.Type.SessionListAck, SessionListAck);
        _ptkHandlers.Add(Pkt.Type.RefreshInfoAck, RefreshInfoAck);
        _ptkHandlers.Add(Pkt.Type.P2PConnectRequestAck, P2PConnectRequestAck);
        _ptkHandlers.Add(Pkt.Type.EchoMessage, EchoMessage);
        _ptkHandlers.Add(Pkt.Type.ServerMessage, ServerMessage);
    }

    private void ServerMessage(PktBase pktBase, TcpParticipant participant)
    {
        var pkt = pktBase as PktServerMessage;
        Debug.Assert(pkt != null);

        ConsoleEx.WriteLine($"서버로부터 메시지 수신: {pkt.Message}", ConsoleColor.DarkGray);
    }

    private void EchoMessage(PktBase pktBase, TcpParticipant participant)
    {
        var pkt = pktBase as PktEchoMessage;
        Debug.Assert(pkt != null);

        ConsoleEx.WriteLine($"[에코] 서버로부터 메시지 수신: {pkt.Message}", ConsoleColor.DarkGray);
    }

    private void P2PConnectRequestAck(PktBase pktBase, TcpParticipant participant)
    {
        var pkt = pktBase as PktP2PConnectRequestAck;
        Debug.Assert(pkt != null);

        participant.ConnectToPeer(pkt.TargetId, true);
    }

    private void SessionListAck(PktBase pktBase, TcpParticipant participant)
    {
        var pkt = pktBase as PktSessionListAck;
        Debug.Assert(pkt != null);

        participant.UpdatePeers(pkt.SessionInfos);
    }

    private void RefreshInfoAck(PktBase pktBase, TcpParticipant participant)
    {
        var pkt = pktBase as PktRefreshInfoAck;
        Debug.Assert(pkt != null);

        participant.UpdateMe(pkt.Info.Id, IPEndPoint.Parse(pkt.Info.PublicEndPoint));
    }
}
