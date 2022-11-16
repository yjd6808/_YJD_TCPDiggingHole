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
    ConsoleEx.WriteLine("===========================================");
    ConsoleEx.WriteLine("[실행 가능한 커맨드 목록]", ConsoleColor.Cyan);
    ConsoleEx.WriteLine($"├─── 리스닝 포트: {introducer.LocalEndPoint?.Port}", ConsoleColor.White);
    ConsoleEx.WriteLine($"├─── 접속중인 클라이언트 수: {introducer.GetSessionList().Count}", ConsoleColor.White);
    ConsoleEx.WriteLine("│ ", ConsoleColor.White);
    ConsoleEx.WriteLine("├ A: 종료", ConsoleColor.White);
    ConsoleEx.WriteLine("├ S: 클라이언트 목록 출력", ConsoleColor.White);
    ConsoleEx.WriteLine("├ D: 브로드캐스트 랜덤메시지 전송", ConsoleColor.White);
    ConsoleEx.WriteLine("└ F: 특정 클라이언트에게 랜덤메시지 전송", ConsoleColor.White);

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
