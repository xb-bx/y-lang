namespace YLang.AST;

public class MethodCallExpression : Expression
{
    public string Name { get; private set; }
    public Expression Expr { get; private set; }
    public List<Expression> Args { get; private set; }
    public MethodCallExpression(string name, List<Expression> args, Expression expr)
        => (Name, Args, Expr, Pos, File) = (name, args, expr, expr.Pos, expr.File);

    public override string ToString()
        => $"{Expr}.{Name}({string.Join(", ", Args)})";

}

