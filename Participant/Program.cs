using System.Diagnostics;
using System.Net.Sockets;

using CTree;

using Participant;
using Shared;

var pktParser = new ParticipantPktParser().Initialize();
var participant = new TcpParticipant(pktParser);
participant.OnSent += ParticipantOnSent;
participant.OnReceived += ParticipantOnReceived;
participant.OnConnectedSuccess += ParticipantOnConnectedSuccess;
participant.OnDisconnected += ParticipantOnDisconnected;

if (!participant.TryConnectSynchronously(Config.IntroducerEndPoint, 1000))
{
    ConsoleEx.WriteLine("서버에 연결하는데 실패했습니다.");
    return;
}

while (true)
{
    long myId = participant.Id;
    string myIdStr = participant.Id != -1 ? $"{participant.Id}" : "갱신 중";
    bool serverConnected = participant.IsConnected();
    string serverConnectedStr = serverConnected ? "중개 서버와 연결됨" : "중개 서버와 연결되지 않음";
    ConsoleTree commandTree = new("===========================================\n[실행가능한 커맨드 목록]")
    {
        ItemForegroundColor = ConsoleColor.White,
        BridgeForegroundColor = ConsoleColor.Cyan
    };

    commandTree.Add(new ConsoleTreeItem($"당신의 ID: {myIdStr}")
    {
        ForegroundColor = myId == -1 ? 
            ConsoleColor.DarkYellow : 
            ConsoleColor.Green
    });

    commandTree.Add(new ConsoleTreeItem($"{serverConnectedStr}")
    {
        ForegroundColor = serverConnected ? 
            ConsoleColor.Green : 
            ConsoleColor.Red
    });

    if (participant.PublicEndPoint != null)
    {
        commandTree.Add(new ConsoleTreeItem($"당신의 사설 IP: {participant.LocalEndPoint}") { BridgeLength = 2} );
        commandTree.Add(new ConsoleTreeItem($"당신의 공인 IP: {participant.PublicEndPoint}") { BridgeLength = 2 });
    }

    commandTree.AddDummy();
    commandTree.Add("A: 서버와 연결 종료");
    commandTree.Add("S: 피어 목록 출력");
    commandTree.Add("D: 특정 피어와 홀펀칭 수행");
    commandTree.Add("F: 특정 피어에게 랜덤 메시지 전송");
    commandTree.Add("G: 특정 피어와 연결 종료");
    commandTree.Add("H: 서버에게 에코 랜덤 메시지 전송");
    commandTree.Add("J: 프로그램 종료");
    ConsoleEx.Lock();
    commandTree.Print();
    ConsoleEx.Unlock();
    var key = Console.ReadKey();
    
    switch (key.Key)
    {
        case ConsoleKey.A:
            participant.Disconnect();
            break;
        case ConsoleKey.S:
            participant.PrintPeers();
            break;
        case ConsoleKey.D:
        {
            Console.Write("\n홀펀칭을 수행할 대상의 ID입력 > ");
            long.TryParse(Console.ReadLine(), out long id);
            participant.ConnectToPeer(id);
            break;
        }
        case ConsoleKey.F:
        {
            Console.Write("\n메시지를 보낼 대상의 ID입력 > ");
            long.TryParse(Console.ReadLine(), out long id);
            participant.SendMessageToPeer(id, Guid.NewGuid().ToString());
            break;
        }
        case ConsoleKey.G:
        {
            Console.Write("\n연결 종료할 대상의 ID입력 > ");
            long.TryParse(Console.ReadLine(), out long id);
            participant.DisconnectPeer(id);
            break;
        }
        case ConsoleKey.H:
        {
            participant.SendAsync(new PktEchoMessage { Message = Guid.NewGuid().ToString() });
            break;
        }
        case ConsoleKey.J:
        {
            participant.DisconnectAllPeers();
            participant.Disconnect();
            return;
        }
    }
}




void ParticipantOnSent(TcpClientEx client, PktBase pkt, int sentBytes)
{
    ConsoleEx.LogLine($"중개 서버({client.RemoteEndPoint})로 {pkt}[{sentBytes}바이트] 전송완료", ConsoleColor.Yellow);
}

void ParticipantOnReceived(TcpClientEx client, PktBase pkt, int receivedBytes)
{
    ConsoleEx.LogLine($"중개 서버({client.RemoteEndPoint})로부터 {pkt}[{receivedBytes}바이트] 수신완료", ConsoleColor.Green);
}

void ParticipantOnConnectedSuccess(TcpClientEx client)
{
    // 서버에 접속하게되면 내 사설 IP 정보를 보내주도록 한다.
    Debug.Assert(participant.LocalEndPoint != null);
    ConsoleEx.LogLine($"중개 서버({client.RemoteEndPoint})접속에 성공하였습니다.", ConsoleColor.Green);
    client.SendAsync(new PktConnectionMessage(participant.LocalEndPoint.ToString()));
}

void ParticipantOnDisconnected(TcpClientEx client, bool safeClosed)
{
    if (safeClosed)
        ConsoleEx.LogLine($"중개 서버({client.RemoteEndPoint})와의 연결이 안전하게 끊어졌습니다.", ConsoleColor.Green);
    else
        ConsoleEx.LogLine($"중개 서버({client.RemoteEndPoint})와의 연결이 강제로 끊어졌습니다.", ConsoleColor.Red);
}
