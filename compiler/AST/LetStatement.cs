namespace YLang.AST;

public class LetStatement : Statement
{
    public string Name { get; private set; }
    public TypeExpression? Type { get; private set; }
    public Expression Value { get; private set; }
    public LetStatement(string name, TypeExpression? type, Expression value, Position pos)
        => (Name, Type, Value, Pos, File) = (name, type, value, pos, value.File);
    public override string ToString()
        => $"let {Name}: {Type} = {Value};";

}

