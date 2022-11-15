using Participant;
using Shared;

ConsoleEx.WriteLine("클라이언트가 시작되었습니다.");

var pktParser = new ParticipantPktParser();
pktParser.Initialize();

var participant = new TcpParticipant(pktParser);



participant.OnConnectedSuccess += client =>
{
    // 서버에 접속하는 경우 기본적으로 사설 IP 주소 정보를 전달해줘야한다.
    if (participant.Id == -1) 
        client.SendAsync(new PktConnectionMessage(participant.LocalEndPoint.ToString()));


    /* 홀펀칭 수행 후 재접속하는 경우 2가지 정보를 추가로 전댈해줘야한다.
     *
     *   1. 클라이언트의 ID정보 (접속을 끊었다가 다시 들어오는 것이기 때문에 서버측에서 누군지 알기위해)
     *   2. 클라이언트와 연결된 다른 P2P 클라이언트 ID 리스트 (서버에서 한눈에 파악할 수 있도록 하기 위함)
     */
    else   
        client.SendAsync(new PktConnectionMessage(participant.LocalEndPoint.ToString(), participant.Id, participant.GetConnectedPeers()));
};

participant.OnSent += (client, pkt) => ConsoleEx.PacketLog($"[보냄][{pkt}]", ConsoleColor.Green);
participant.OnReceived += (client, pkt) => ConsoleEx.PacketLog($"[받음][{pkt}]", ConsoleColor.DarkYellow);

if (!participant.TryConnectSynchronously(Config.IntroducerEndPoint, 1000))
{
    ConsoleEx.WriteLine("서버에 연결하는데 실패했습니다.");
    return;
}

while (true)
{
    string myId = participant.Id != -1 ? $"ID: {participant.Id}" : "ID: 갱신 중";
    string serverConnectedStr = participant.IsConnected() ? "서버와 연결됨" : "서버와 연결되지 않음";
    ConsoleEx.WriteLine("===========================================");

    if (participant.PublicEndPoint == null)
        ConsoleEx.WriteLine($"[커맨드 키][{myId}][[{serverConnectedStr}]");
    else
    {
        ConsoleEx.WriteLine($"[커맨드 키][{myId}][{serverConnectedStr}]");
        ConsoleEx.WriteLine($"당신의 사설 IP: {participant.LocalEndPoint}");
        ConsoleEx.WriteLine($"당신의 공인 IP: {participant.PublicEndPoint}");
    }

    ConsoleEx.WriteLine("├ A: 서버와 연결 종료");
    ConsoleEx.WriteLine("├ S: 피어 목록 출력");
    ConsoleEx.WriteLine("├ D: 특정 피어와 홀펀칭 수행");
    ConsoleEx.WriteLine("├ F: 특정 피어에게 랜덤 메시지 전송");
    ConsoleEx.WriteLine("├ G: 특정 피어와 연결 종료");
    ConsoleEx.WriteLine("├ H: 서버에게 에코 랜덤 메시지 전송");
    ConsoleEx.WriteLine("└ J: 프로그램 종료");
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
