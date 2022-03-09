namespace YLang.AST;

public class InlineAsmStatement : Statement 
{
    public List<Token> Body { get; private set; }
    public InlineAsmStatement(List<Token> body, Position pos)
        => (Body, File, Pos) = (body, body.FirstOrDefault().File ?? "<source>", pos);
}
