namespace YLang.AST;

public class NegateExpression : Expression
{
    public Expression Expr { get; private set; }
    public NegateExpression(Expression expr, Position pos)
        => (Expr, Pos, File) = (expr, pos, expr.File);
    public override string ToString()
        => $"-{Expr}";
}

