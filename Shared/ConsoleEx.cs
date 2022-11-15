/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/13/2022 10:38:05 PM
 * * * * * * * * * * * * *
 * 콘솔 출력 편하게 하기위함
 *
 * * * * * * * * * * * * * 
 */

namespace Shared;

public class ConsoleEx
{
    public static void WriteLine()
    {
        using var _ = DisposeLock.AutoLock(Config.PrintLock);
        Console.WriteLine();
    }
    public static void WriteLine(string msg, ConsoleColor color = ConsoleColor.Gray)
    {
        using var _ = DisposeLock.AutoLock(Config.PrintLock);
        ConsoleColor defaultColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ForegroundColor = defaultColor;
    }

    public static void Write(string msg, ConsoleColor color = ConsoleColor.Gray)
    {
        using var _ = DisposeLock.AutoLock(Config.PrintLock);
        ConsoleColor defaultColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(msg);
        Console.ForegroundColor = defaultColor;
    }

    public static void PacketLog(string msg, ConsoleColor color = ConsoleColor.Gray)
    {
        if (!Config.PrintPacketLog)
            return;

        using var _ = DisposeLock.AutoLock(Config.PrintLock);
        ConsoleColor defaultColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ForegroundColor = defaultColor;
    }

    public static void DebugLogLine(string msg, ConsoleColor color = ConsoleColor.Gray)
    {
        if (!Config.PrintDebugLog)
            return;

        using var _ = DisposeLock.AutoLock(Config.PrintLock);
        ConsoleColor defaultColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ForegroundColor = defaultColor;
    }

    public static void DebugLog(string msg, ConsoleColor color = ConsoleColor.Gray)
    {
        if (!Config.PrintDebugLog)
            return;

        using var _ = DisposeLock.AutoLock(Config.PrintLock);
        ConsoleColor defaultColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(msg);
        Console.ForegroundColor = defaultColor;
    }
}
