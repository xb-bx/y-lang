namespace YLang.AST;

public class BinaryExpression : Expression
{
    public Expression Left { get; private set; }
    public Expression Right { get; private set; }
    public string Op { get; private set; }
    public BinaryExpression(Expression left, Expression right, string op)
        => (Left, Right, Op, Pos, File) = (left, right, op, left.Pos, left.File);
    public override string ToString()
        => $"({Left} {Op} {Right})";
}

