namespace YLang;

public class Error
{
    public string Message { get; private set; }
    public string File { get; private set; }
    public Position Pos { get; private set; }
    public Error(string msg, string file, Position pos)
        => (Message, File, Pos) = (msg, file, pos);
    public override string ToString()
        => $"{File}:{Pos.Line}:{Pos.Column}:{Message}";
    
}

