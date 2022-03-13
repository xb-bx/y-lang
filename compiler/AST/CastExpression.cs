namespace YLang.AST;

public class CastExpression : Expression 
{
    public Expression Value { get; private set; }
    public TypeExpression Type { get; private set; }
    public CastExpression(Expression val, TypeExpression type, Position pos)
        => (Value, Type, File, Pos) = (val, type, val.File, pos);
    public override string ToString()
        => $"cast({Value}, {Type})";
}
