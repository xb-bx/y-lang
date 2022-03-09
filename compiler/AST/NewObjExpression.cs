namespace YLang.AST;

public class NewObjExpression : Expression
{
    public string TypeName { get; private set; }
    public List<Expression> Args { get; private set; }
    public NewObjExpression(string name, List<Expression> args, Position pos, string file)
        => (TypeName, Args, Pos, File) = (name, args, pos, file);
    public override string ToString()
        => $"new {TypeName}({string.Join(", ", Args)})";
}

