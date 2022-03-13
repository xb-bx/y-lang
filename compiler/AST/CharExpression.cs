namespace YLang.AST;

public class CharExpression : Expression 
{
    public char Value { get; private set; }
    public CharExpression(char value, Position pos, string file)
        => (Value, Pos, File) = (value, pos, file);
    public override string ToString()
        => $"'{Value}'";
}

