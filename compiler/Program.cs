using System.Diagnostics;
using static YLang.Helpers;
using System.IO.Compression;

namespace YLang;

[App]
public class Program
{
    private static string baseDir = AppContext.BaseDirectory;
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
    ///<param name="fasm" alias="f">Path to fasm executable</param>
    ///<param name="noConsole" alias="nc">Creates executable without console window</param>
    public static bool Compile(
        FileInfo source,
        Target? target,
        DirectoryInfo? dumpIr = null,
        string[]? define = null,
        bool optimize = false,
        bool disableNullChecks = false,
        bool nogc = false,
        bool noConsole = false,
        string? fasm = null
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
        lerrors.ForEach(x => Console.WriteLine(x));
        Console.WriteLine(tokens.Count);
        var includes = new List<string>();
        includes.Add("std/utils.y");
        if (!nogc) includes.Add("std/gc.y");
        var statements = Parser.Parse(
            tokens,
            out var errors,
            new HashSet<string>(define ?? Array.Empty<string>()) { t.ToString().ToUpper() },
            includes.ToArray()
        );
        //Console.WriteLine(Formater.Format(statements));
        Environment.SetEnvironmentVariable("INCLUDE", Path.Combine(baseDir, "fasm/win/INCLUDE"));
        var fasmbin = fasm ?? Path.Combine(baseDir, Environment.OSVersion.Platform == PlatformID.Win32NT ? "fasm/win/fasm.exe" : "fasm/linux/fasm/fasm.x64");
        var cerrors = Compiler.Compile(
            statements,
            Path.ChangeExtension(source.FullName, ".asm"),
            new CompilerSettings
            {
                Optimize = optimize,
                DumpIR = dumpIr,
                Target = t,
                NullChecks = !disableNullChecks,
                NoConsole = noConsole,
            }
        );
        Console.ForegroundColor = ConsoleColor.Red;
        var errs = errors.Concat(lerrors).Concat(cerrors).ToList();
        foreach (var error in errs)
            Console.WriteLine(error);
        Console.ResetColor();
        if (errs.Count > 0)
            return false;
        if(!File.Exists(fasmbin)) 
        {
            Console.WriteLine("NO FASM EXECUTABLE FOUND");
            Console.WriteLine("Use command download-fasm or specify path manually using --fasm option");
            return false;
        }
        return RunAndWaitForExitCode(fasmbin, Path.ChangeExtension(source.FullName, ".asm")) == 0;
    }
    ///<summary>
    ///Run the source file
    ///</summary>
    ///<param name="source">Source file</param>
    ///<param name="define" alias="d">Define symbols</param>
    ///<param name="fasm" alias="f">Path to fasm executable</param>
    public static void Run(FileInfo source, string[]? define = null, string? fasm = null)
    {
        var target = (
            Environment.OSVersion.Platform == PlatformID.Win32NT ? Target.Windows : Target.Linux
        );
        if (Compile(source, target, null, define, fasm: fasm))
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
    ///<summary>Download latest fasm binaries</summary>
    public static async Task DownloadFasm()
    {
        var winlink = "https://flatassembler.net/fasmw17330.zip";
        var linuxlink = "https://flatassembler.net/fasm-1.73.30.tgz";
        Directory.CreateDirectory(Path.Combine(baseDir, "fasm"));
        var client = new HttpClient();
        using (var windowsStream = await client.GetStreamAsync(winlink))
        using (var arc = new ZipArchive(windowsStream))
        {

            Directory.CreateDirectory(Path.Combine(baseDir, "fasm/win"));
            arc.ExtractToDirectory(Path.Combine(baseDir, "fasm/win"), true);
        }
        if(Environment.OSVersion.Platform is PlatformID.Unix)
        using (var linuxStream = await client.GetStreamAsync(linuxlink))
        using (var arc = new GZipStream(linuxStream, CompressionMode.Decompress))
        {
            Tgz.Unzip(arc, Path.Combine(baseDir, "fasm/linux"));
            RunAndWaitForExitCode("/bin/bash", $"-c \"chmod +x {Path.Combine(baseDir, "fasm/linux/fasm/fasm.x64")}\"");
        }


    }
}

