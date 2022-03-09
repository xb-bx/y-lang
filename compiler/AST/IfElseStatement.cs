namespace YLang.AST;

public class IfElseStatement : Statement 
{
    public Expression Condition { get; private set; }
    public Statement Body { get; private set; }
    public Statement? Else { get; private set; }
    public IfElseStatement(Expression cond, Statement body, Statement? els, Position pos)
        => (Condition, Body, Else, Pos, File) = (cond, body, els, pos, cond.File);
}

