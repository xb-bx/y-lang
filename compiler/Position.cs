namespace YLang;

public struct Position
{
    public int Column, Line;
    public Position(int col, int line)
        => (Column, Line) = (col, line);
    public Position() : this(1, 1) { }
    public override string ToString()
        => $"{Column} {Line}";
}

