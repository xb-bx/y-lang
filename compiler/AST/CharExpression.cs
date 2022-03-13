namespace YLang.AST;

public class CharExpression : Expression 
{
    public char Value { get; private set; }
    public CharExpression(char value, Position pos, string file)
        => (Value, Pos, File) = (value, pos, file);
    public override string ToString()
        => $"'{Value}'";
}
public class StringExpression : Expression
{
    public string Value { get; private set; }
    public StringExpression(string value, string file, Position pos)
        => (Value, File, Pos) = (value, file, pos);
    public override string ToString()
        => $"\"{Value}\"";
}
