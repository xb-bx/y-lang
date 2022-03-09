namespace YLang.AST;

public class WhileStatement : Statement 
{
    public Expression Cond { get; private set; }
    public Statement Body { get; private set; }
    public WhileStatement(Expression cond, Statement body, Position pos)
        => (Cond, Body, Pos, File) = (cond, body, pos, cond.File);
}
