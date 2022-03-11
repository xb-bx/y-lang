namespace YLang.AST;

public class DereferenceExpression : Expression
{
    public Expression Expr { get; private set; }
    public DereferenceExpression(Expression expr, Position pos)
    {
        (Expr, Pos, File) = (expr, pos, expr.File);
    }
    public override string ToString()
        => $"*{Expr}";
}

