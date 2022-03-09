namespace YLang.AST;

public class IntegerExpression : Expression
{
    public long Value { get; private set; }

    public IntegerExpression(long value, Position pos, string file)
        => (Value, Pos, File) = (value, pos, file);
    public override string ToString()
        => Value.ToString();
}

