namespace YLang.AST;

public class NewObjExpression : Expression
{
    public TypeExpression Type { get; private set; }
    public List<Expression> Args { get; private set; }
    public NewObjExpression(TypeExpression type, List<Expression> args, Position pos, string file)
        => (Type, Args, Pos, File) = (type, args, pos, file);
    public override string ToString()
        => $"new {Type}({string.Join(", ", Args)})";
}

public class BoxExpression : Expression 
{
    public Expression Expr { get; private set; }
    public BoxExpression(Expression expr, Position pos)
        => (Expr, Pos, File) = (expr, pos, expr.File);
    public override string ToString()
        => $"box {Expr}";
}
