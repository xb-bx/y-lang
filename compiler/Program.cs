using System.Diagnostics;

namespace YLang
{
    public enum Target 
    {
        Windows,
        Linux
    }
    [App]
    public class Program
    {
        ///<summary>
        ///Compile the source file
        ///</summary>
        ///<param alias="o" name="optimize">Enable optimization</param>
        ///<param alias="t" name="target">Target platform</param>
        ///<param name="source">Source file</param>
        public static bool Compile(FileInfo source, Target? target, bool optimize = false)
        {
            if(!source.Exists)
                return false;
            var t = target ?? (Environment.OSVersion.Platform == PlatformID.Win32NT ? Target.Windows : Target.Linux);
            var tokens = Lexer.Tokenize(File.ReadAllText(source.FullName), Path.GetFileName(source.FullName), out var lerrors);
            tokens.ForEach(x => Console.WriteLine(x));
            lerrors.ForEach(x => Console.WriteLine(x));
            Console.WriteLine(tokens.Count);
            var statements = Parser.Parse(tokens, out var errors, new HashSet<string>() { t.ToString().ToUpper() });
            foreach(var statement in statements)
                Console.WriteLine(statement.GetType());
            var cerrors = Compiler.Compile(statements, Path.ChangeExtension(source.FullName, ".asm"), t, optimize);
            Console.ForegroundColor = ConsoleColor.Red;
            var errs = errors.Concat(lerrors).Concat(cerrors).ToList();
            foreach (var error in errs)
                Console.WriteLine(error);
            Console.ResetColor();
            if(errs.Count > 0)
                return false;
            Process.Start("fasm", Path.ChangeExtension(source.FullName, ".asm")).WaitForExit();
            return true;
        }
        public static void Run(FileInfo source)
        {
            var target = (Environment.OSVersion.Platform == PlatformID.Win32NT ? Target.Windows : Target.Linux);
            if(Compile(source, target))
            {
                var linuxFileName = Path.GetFileNameWithoutExtension(source.FullName);
                if(target is Target.Linux)
                {
                    Process.Start("/bin/bash", $"-c \"chmod +x {linuxFileName}\"");
                }
                Process.Start(target is Target.Windows ? Path.ChangeExtension(source.FullName, ".exe") : linuxFileName).WaitForExit();
            }
        }
    }

}

