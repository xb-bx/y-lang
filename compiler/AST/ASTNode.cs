namespace YLang.AST;

public abstract class ASTNode
{
    public Position Pos { get; protected set; }
    public string File { get; protected set; }
}

