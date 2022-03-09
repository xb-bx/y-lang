namespace YLang;

public sealed class MatchGroup
{
    public string? Value;
    public TokenType Type;
    public MatchGroup? Next;
    public MatchGroup(TokenType type, string? value = null, MatchGroup? next = null)
        => (Type, Value, Next) = (type, value, next);
    public bool Match(Token token)
    {
        if (token.Type == Type && Value is null)
        {
            return true;
        }
        else if (token.Type == Type && Value == token.Value)
        {
            return true;
        }
        else
        {
            return Next?.Match(token) == true;
        }
    }
    public MatchGroup Or(MatchGroup match)
    {
        match.Next = this;
        return match;
    }
    public MatchGroup Or(TokenType type)
    {
        var m = Match(type);
        m.Next = this;
        return m;
    }

    public MatchGroup Or(TokenType type, string value)
    {
        var m = Match(type, value);
        m.Next = this;
        return m;
    }

    public MatchGroup OrKeyword(string kw)
    {
        var m = MatchKeyword(kw);
        m.Next = this;
        return m;
    }
    public MatchGroup OrOp(string op)
    {
        var m = MatchOp(op);
        m.Next = this;
        return m;
    }
    public static MatchGroup Match(TokenType type)
        => new(type);
    public static MatchGroup Match(TokenType type, string str)
        => new(type, str);
    public static MatchGroup MatchKeyword(string kw)
        => Match(TokenType.Keyword, kw);
    public static MatchGroup MatchOp(string op)
        => Match(TokenType.Operator, op);

    public static MatchGroup LP = Match(TokenType.Bracket, "(");
    public static MatchGroup RP = Match(TokenType.Bracket, ")");
    public static MatchGroup LBR = Match(TokenType.Bracket, "[");
    public static MatchGroup RBR = Match(TokenType.Bracket, "]");
    public static MatchGroup LBRC = Match(TokenType.Bracket, "{");
    public static MatchGroup RBRC = Match(TokenType.Bracket, "}");
    public static MatchGroup Dot = Match(TokenType.Dot);
    public static MatchGroup Id = Match(TokenType.Id);
    public static MatchGroup Colon = Match(TokenType.Colon);
    public static MatchGroup Semicolon = Match(TokenType.Semicolon);
    public static MatchGroup EQ = MatchOp("=");
}

