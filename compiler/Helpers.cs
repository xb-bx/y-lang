using System.Diagnostics;

namespace YLang;

public static class Helpers 
{
    public static bool Try<T>(Func<T> fn, out T res)
    {
        res = default;
        try 
        {
            res = fn();
            return true;
        }
        catch 
        {
            return false;
        }
    }
    public static int RunAndWaitForExitCode(string exe, string args)
    {
        var proc = Process.Start(exe, args);
        proc.WaitForExit();
        return proc.ExitCode;
    }
}

