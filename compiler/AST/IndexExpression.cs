namespace YLang.AST;

public class IndexExpression : Expression
{
    public Expression Indexed { get; private set; }
    public List<Expression> Indexes { get; private set; }
    public IndexExpression(Expression indexed, List<Expression> indexes, Position pos)
        => (Indexed, Indexes, Pos, File) = (indexed, indexes, pos, indexed.File);
    public override string ToString()
        => $"{Indexed}[{string.Join(", ", Indexes)}]";
}

