using System.Diagnostics;

namespace YLang
{
    [App]
    public class Program
    {
        public static bool Compile(FileInfo source)
        {
            if(!source.Exists)
                return false;
            var tokens = Lexer.Tokenize(File.ReadAllText(source.FullName), Path.GetFileName(source.FullName), out var lerrors);
            Console.WriteLine(tokens.Count);
            var statements = Parser.Parse(tokens, out var errors);
            foreach(var statement in statements)
                Console.WriteLine(statement.GetType());
            var cerrors = Compiler.Compile(statements, Path.ChangeExtension(source.FullName, ".asm"));
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
            if(Compile(source))
            {
                Process.Start(Path.ChangeExtension(source.FullName, ".exe")).WaitForExit();
            }
        }
    }

}

