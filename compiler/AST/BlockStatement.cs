namespace YLang.AST;

public class BlockStatement : Statement 
{
    public List<Statement> Statements { get; private set; }
    public BlockStatement(List<Statement> sts, Position pos, string file)
        => (Statements, Pos, File) = (sts, pos, file);
}

