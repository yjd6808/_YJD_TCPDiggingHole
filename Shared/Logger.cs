/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 11/13/2022 6:07:55 PM
 * * * * * * * * * * * * *
 * 예외 출력 로깅
 */

using System.Diagnostics;

namespace Shared;

public class Logger
{
    public static void PrintExecption(Exception e, string msg = "", bool printStackTrace = true)
    {
        if (!Config.PrintExecptionLog) 
            return;

        var st = new StackTrace();
        var sf = st.GetFrame(1);

        if (msg != "")
            msg += "\n";

        if (printStackTrace)
        {
            var splits = e.StackTrace.Split("   at ");
            ConsoleEx.WriteLine($"함수: {sf.GetMethod()?.Name}\n{msg}" + e.Message + "\n" + splits[splits.Length - 1] + "\n"); 
        }
        else
            ConsoleEx.WriteLine($"함수: {sf.GetMethod()?.Name}\n{msg}" + e.Message);
    }
}
