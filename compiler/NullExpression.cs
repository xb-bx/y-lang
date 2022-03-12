using YLang.AST;
namespace YLang
{
    public class NullExpression : Expression
    {
        public NullExpression(Position pos, string file) 
            => (Pos, File) = (pos, file);
        public override string ToString()
            => "null";
    }
}
