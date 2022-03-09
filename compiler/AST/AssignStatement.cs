namespace YLang.AST;

public class AssignStatement : Statement
{
    public Expression Expr { get; private set; }
    public Expression Value { get; private set; }
    public AssignStatement(Expression expr, Expression value)
        => (Expr, Value, Pos, File) = (expr, value, expr.Pos, expr.File);
    public override string ToString()
        => $"{Expr} = {Value};";

}
public class BoolExpression : Expression 
{
    public bool Value { get; private set; }
    public BoolExpression(bool value, Position pos, string file)
        => (Value, Pos, File) = (value, pos, file);
}
