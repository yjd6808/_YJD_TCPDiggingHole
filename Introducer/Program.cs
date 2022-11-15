using System.Net;
using System.Net.Sockets;
using Introducer;
using Shared;

TcpIntroducer introducer = new TcpIntroducer(9999);
introducer.Start();

Console.WriteLine("서버가 시작되었습니다.");

while (true)
{
    ConsoleEx.WriteLine("===========================================");
    ConsoleEx.WriteLine("[커맨드 키]");
    ConsoleEx.WriteLine("├ A: 종료");
    ConsoleEx.WriteLine("├ S: 피어 목록 출력");
    ConsoleEx.WriteLine("├ D: 브로드캐스트 랜덤메시지 전송");
    ConsoleEx.WriteLine("└ F: 특정 세션에게 랜덤메시지 전송");

    var key = Console.ReadKey();
    ConsoleEx.WriteLine("\n===========================================");

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
