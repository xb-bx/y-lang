namespace YLang.AST;

public class MemberAccessExpression : Expression
{
    public Expression Expr { get; private set; }
    public string MemberName { get; private set; }
    public MemberAccessExpression(Expression expr, string name, Position pos)
        => (Expr, MemberName, Pos, File) = (expr, name, pos, expr.File);
    public override string ToString()
        => $"{Expr}.{MemberName}";
}

