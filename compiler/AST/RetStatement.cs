namespace YLang.AST;

public class RetStatement : Statement
{
    public Expression? Value { get; private set; }
    public RetStatement(Expression? value, Position pos, string file)
        => (Value, Pos, File) = (value, pos, file);
    public override string ToString()
        => Value is null ? "ret;" : $"ret {Value};";
}

