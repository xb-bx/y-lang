using System.Diagnostics;
using static YLang.Helpers;

namespace YLang;

[App]
public class Program
{
    ///<summary>
    ///Compile the source file
    ///</summary>
    ///<param alias="o" name="optimize">Enable optimization</param>
    ///<param alias="t" name="target">Target platform</param>
    ///<param name="source">Source file</param>
    ///<param name="define" alias="d">Define symbols</param>
    ///<param name="dumpIr">Dump IR to selected folder</param>
    ///<param name="disableNullChecks" alias="dnc">Enable null checks</param>
    ///<param name="nogc">No GC</param>
    public static bool Compile(
        FileInfo source,
        Target? target,
        DirectoryInfo? dumpIr = null,
        string[]? define = null,
        bool optimize = false,
        bool disableNullChecks = false,
        bool nogc = false
    )
    {
        if (!source.Exists)
            return false;
        var t =
            target
            ?? (
                Environment.OSVersion.Platform == PlatformID.Win32NT ? Target.Windows : Target.Linux
            );
        var tokens = Lexer.Tokenize(
            File.ReadAllText(source.FullName),
            Path.GetFileName(source.FullName),
            out var lerrors
        );
        //            tokens.ForEach(x => Console.WriteLine(x));
        lerrors.ForEach(x => Console.WriteLine(x));
        Console.WriteLine(tokens.Count);
        var includes = new List<string>();
        includes.Add("std/utils.y");
        if(!nogc) includes.Add("std/gc.y");
        var statements = Parser.Parse(
            tokens,
            out var errors,
            new HashSet<string>(define ?? Array.Empty<string>()) { t.ToString().ToUpper() },
            includes.ToArray()
        );
        //Console.WriteLine(Formater.Format(statements));
        
        var cerrors = Compiler.Compile(
            statements,
            Path.ChangeExtension(source.FullName, ".asm"),
            new CompilerSettings 
            {
                Optimize = optimize,
                DumpIR = dumpIr,
                Target = t,
                NullChecks = !disableNullChecks
            }
        );
        Console.ForegroundColor = ConsoleColor.Red;
        var errs = errors.Concat(lerrors).Concat(cerrors).ToList();
        foreach (var error in errs)
            Console.WriteLine(error);
        Console.ResetColor();
        if (errs.Count > 0)
            return false;
        return RunAndWaitForExitCode("fasm", Path.ChangeExtension(source.FullName, ".asm")) == 0;
    }
    ///<summary>
    ///Run the source file
    ///</summary>
    ///<param name="source">Source file</param>
    ///<param name="define" alias="d">Define symbols</param>
    public static void Run(FileInfo source, string[]? define = null)
    {
        var target = (
            Environment.OSVersion.Platform == PlatformID.Win32NT ? Target.Windows : Target.Linux
        );
        if (Compile(source, target, null, define))
        {
            var linuxFileName = Path.GetFileNameWithoutExtension(source.FullName);
            if (target is Target.Linux)
            {
                if (RunAndWaitForExitCode("/bin/bash", $"-c \"chmod +x {linuxFileName}\"") != 0)
                    return;
            }
            RunAndWaitForExitCode(
                target is Target.Windows
                  ? Path.ChangeExtension(source.FullName, ".exe")
                  : linuxFileName,
                ""
            );
        }
    }
}
