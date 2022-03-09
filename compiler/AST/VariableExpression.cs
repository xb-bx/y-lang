namespace YLang.AST;

public class VariableExpression : Expression
{
    public string Name { get; private set; }
    public VariableExpression(string name, Position pos, string file)
        => (Name, Pos, File) = (name, pos, file);
    public override string ToString()
        => Name;
}

