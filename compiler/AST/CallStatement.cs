namespace YLang.AST;

public class CallStatement : Statement
{
    public Expression Call { get; private set; }
    public CallStatement(Expression call)
        => (Call, Pos, File) = (call, call.Pos, call.File);
    public override string ToString()
        => $"{Call};";
}

