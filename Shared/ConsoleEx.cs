/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/13/2022 10:38:05 PM
 * * * * * * * * * * * * *
 * 콘솔 출력 편하게 하기위함
 *
 * * * * * * * * * * * * * 
 */

using System.Diagnostics;

namespace Shared;

public class ConsoleEx
{
    public static void WriteLine()
    {
        using var _ = DisposeLock.AutoLock(Config.PrintLock);
        Console.WriteLine();
    }

    public static void WriteLine(string msg, ConsoleColor color = ConsoleColor.Gray, string msg2 = "", ConsoleColor color2 = ConsoleColor.Gray)
    {
        using var _ = DisposeLock.AutoLock(Config.PrintLock);

        if (msg2.Length == 0)
        {
            ConsoleColor defaultColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
            Console.ForegroundColor = defaultColor;
        }
        else
        {
            ConsoleColor defaultColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(msg);
            Console.ForegroundColor = color2;
            Console.WriteLine(msg2);
            Console.ForegroundColor = defaultColor;
        }
    }

    public static void Write(string msg, ConsoleColor color = ConsoleColor.Gray)
    {
        using var _ = DisposeLock.AutoLock(Config.PrintLock);
        ConsoleColor defaultColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(msg);
        Console.ForegroundColor = defaultColor;
    }

    public static void LogLine(string msg, ConsoleColor color = ConsoleColor.Green)
    {
        if (!Config.PrintNormalLog)
            return;

        using var _ = DisposeLock.AutoLock(Config.PrintLock);
        ConsoleColor defaultColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ForegroundColor = defaultColor;
    }


    public static void DebugLogLine(string msg, ConsoleColor color = ConsoleColor.DarkYellow)
    {
        if (!Config.PrintDebugLog)
            return;

        using var _ = DisposeLock.AutoLock(Config.PrintLock);
        ConsoleColor defaultColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ForegroundColor = defaultColor;
    }

    public static void DebugLog(string msg, ConsoleColor color = ConsoleColor.DarkYellow)
    {
        if (!Config.PrintDebugLog)
            return;

        using var _ = DisposeLock.AutoLock(Config.PrintLock);
        ConsoleColor defaultColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(msg);
        Console.ForegroundColor = defaultColor;
    }

    public static void Lock()
    {
        Monitor.Enter(Config.PrintLock);
    }

    public static void Unlock()
    {
        Monitor.Exit(Config.PrintLock);
    }

    public static void PrintExecption(Exception e, string msg = "", bool printStackTrace = true, ConsoleColor color = ConsoleColor.Gray)
    {
        if (!Config.PrintExecptionLog)
            return;

        var st = new StackTrace();
        var sf = st.GetFrame(1);

        var userMsg = "사용자 메시지: ";

        if (msg != "")
        {
            userMsg += msg;
            msg = userMsg;
        }

        if (printStackTrace)
        {
            var splits = e.StackTrace.Split("   at ");
            ConsoleEx.WriteLine($"함수: {sf.GetMethod()?.Name}\n{msg}" + e.Message + "\n" + splits[splits.Length - 1] + "\n", color);
        }
        else
            ConsoleEx.WriteLine($"함수: {sf.GetMethod()?.Name}\n{msg}" + e.Message, color);
    }
}
