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
        public static bool Compile(FileInfo source, Target target)
        {
            if(!source.Exists)
                return false;
            var tokens = Lexer.Tokenize(File.ReadAllText(source.FullName), Path.GetFileName(source.FullName), out var lerrors);
            Console.WriteLine(tokens.Count);
            var statements = Parser.Parse(tokens, out var errors);
            foreach(var statement in statements)
                Console.WriteLine(statement.GetType());
            var cerrors = Compiler.Compile(statements, Path.ChangeExtension(source.FullName, ".asm"), target);
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
            var target = Environment.OSVersion.Platform == PlatformID.Win32NT ? Target.Windows : Target.Linux;
            if(Compile(source, target))
            {
                Process.Start(target is Target.Windows ? Path.ChangeExtension(source.FullName, ".exe") : Path.GetFileNameWithoutExtension(source.FullName)).WaitForExit();
            }
        }
    }

}

