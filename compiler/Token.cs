namespace YLang;

public struct Token
{
    public TokenType Type;
    public string Value;
    public string File;
    public Position Pos;
    public override string ToString()
        => $"[{Type}]: {Value} at {Pos} in {File}";
    public static readonly Token UndefinedId = new Token { Type = TokenType.Id, Value = "<undefined>" };
}

