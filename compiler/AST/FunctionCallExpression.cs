namespace YLang.AST;

public class FunctionCallExpression : Expression
{
    public string Name { get; private set; }
    public List<Expression> Args { get; private set; }
    public FunctionCallExpression(string name, List<Expression> args, Position pos, string file)
        => (Name, Args, Pos, File) = (name, args, pos, file);
    public override string ToString()
        => $"{Name}({string.Join(", ", Args)})";
}

