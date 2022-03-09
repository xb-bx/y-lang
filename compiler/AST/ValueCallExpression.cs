namespace YLang.AST;

public class ValueCallExpression : Expression
{
    public Expression Expr { get; private set; }
    public List<Expression> Args { get; private set; }
    public ValueCallExpression(Expression expr, List<Expression> args)
        => (Expr, Args, Pos, File) = (expr, args, expr.Pos, expr.File);
    public override string ToString()
        => $"{Expr}({string.Join(", ", Args)})";
}

