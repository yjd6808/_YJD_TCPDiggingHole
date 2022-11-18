using CTree;

using Introducer;
using Shared;

Console.Write("바인드할 포트를 입력해주세요. ");
int.TryParse(Console.ReadLine(), out int port);

if (port < 100 || port > short.MaxValue)
{
    Console.WriteLine($"포트는 100 ~ {short.MaxValue}사이로 입력해주세요.");
    return;
}

TcpIntroducer introducer = new TcpIntroducer(port);
introducer.Start();

while (true)
{
    ConsoleTree commandTree = new("===========================================\n[실행가능한 커맨드 목록]")
    {
        ItemForegroundColor = ConsoleColor.White, 
        BridgeForegroundColor = ConsoleColor.Cyan
    };

    commandTree.Add(new ConsoleTreeItem($"리스닝 포트: {introducer.LocalEndPoint?.Port}")
    {
        BridgeLength = 4,
        ForegroundColor = ConsoleColor.Green
    });
    commandTree.Add(new ConsoleTreeItem($"접속중인 클라이언트 수: {introducer.GetSessionList().Count}")
    {
        BridgeLength = 4,
        ForegroundColor = ConsoleColor.Green
    });
    commandTree.AddDummy();
    commandTree.Add("A: 종료");
    commandTree.Add("S: 클라이언트 목록 출력");
    commandTree.Add("D: 브로드캐스트 랜덤메시지 전송");
    commandTree.Add("F: 특정 클라이언트에게 랜덤메시지 전송");
    ConsoleEx.Lock();
    commandTree.Print();
    ConsoleEx.Unlock();

    var key = Console.ReadKey();

    switch (key.Key)
    {
        case ConsoleKey.A:
            introducer.Stop();
            return;
        case ConsoleKey.S:
            introducer.PrintSessions();
            break;
        case ConsoleKey.D:
            introducer.BroadcastRandomMessage(Guid.NewGuid().ToString());
            break;
        case ConsoleKey.F:
        {
            ConsoleEx.Write("\n메시지를 보낼 대상의 ID입력 > ");
            long.TryParse(Console.ReadLine(), out long id);
            introducer.SendMessage(id, Guid.NewGuid().ToString());
            break;
        }
    }
}
