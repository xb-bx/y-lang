namespace YLang.AST;

public class DereferenceExpression : Expression
{
    public Expression Expr { get; private set; }
    public int DerefDepth { get; private set; }
    public DereferenceExpression(Expression expr, int depth, Position pos)
    {
        (Expr, DerefDepth, Pos, File) = (expr, depth, pos, expr.File);
        if (Expr is DereferenceExpression deref)
        {
            DerefDepth += deref.DerefDepth;
            Expr = deref.Expr;
        }
    }
    public override string ToString()
        => $"{new string('*', DerefDepth)}{Expr}";
}

