namespace YLang.AST;

public class RefExpression : Expression
{
    public Expression Expr;
    public RefExpression(Expression expr, Position pos)
        => (Expr, Pos, File) = (expr, pos, expr.File);
    public override string ToString()
        => $"&{Expr}";
}

