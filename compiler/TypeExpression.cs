using YLang.AST;
namespace YLang;

public class TypeExpression : Expression
{
    public virtual string Name { get; }
    public TypeExpression(string name, Position pos, string file)
        => (Name, Pos, File) = (name, pos, file);
    protected TypeExpression() 
        => Name = null!;
    public override string ToString()
        => Name;
}
