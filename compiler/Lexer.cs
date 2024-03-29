using System.Text;

namespace YLang;

public static class Lexer
{
    private static readonly HashSet<string> keywords = new()
    {
        "false",
        "cast",
        "true",
        "if",
        "else",
        "while",
        "fn",
        "ret",
        "break",
        "continue",
        "let",
        "struct",
        "new",
        "box",
        "asm",
        "null",
        "include",
        "constructor",
        "typeof",
        "dllimport",
        "extern",
        "from",
        "YLang",
        "WindowsX64",
        "interface",
        "union",
        "enum",
        "stackalloc",
    };
    private static readonly HashSet<char> brackets = new()
    {
        '(',
        ')',
        '[',
        ']',
        '{',
        '}'
    };
    private static readonly HashSet<char> operators = new()
    {
        '+',
        '-',
        '*',
        '/',
        '>',
        '<',
        '=',
        '!',
        '&',
        '|',
        '%',
        '^',
    };
    private static readonly Dictionary<char, string> secondPartOperators = new()
    {
        ['='] = "=",
        ['>'] = "=>",
        ['<'] = "=<",
        ['!'] = "=",
        ['|'] = "|",
        ['&'] = "&",
    };
    private static readonly Dictionary<char, TokenType> special = new()
    {
        [';'] = TokenType.Semicolon,
        [':'] = TokenType.Colon,
        [','] = TokenType.Comma,
        ['.'] = TokenType.Dot,
    };

    private ref struct Context
    {
        public int Pos = 0;
        public string File = "source";
        public string Code = null!;
        public List<Error> Errors = new();
        public Position CurrentPos = default;
        public StringBuilder Builder = new();
        public Context() {}
    }
    public static List<Token> Tokenize(string code, string file, out List<Error> errors)
    {
        var res = new List<Token>();
        var ctx = new Context { Code = code, File = file };
        errors = ctx.Errors;
        ctx.CurrentPos = new();
        ref int pos = ref ctx.Pos;
        ref Position curPos = ref ctx.CurrentPos;
        while (pos < code.Length)
        {
            switch (code[pos])
            {
                case '#': 
                    {
                        pos++;
                        curPos.Column++;
                        var id = Id(ref ctx);
                        id.Type = TokenType.Preprocessor;
                        res.Add(id);
                    }
                    break;
                case char comm when comm is '/' && pos + 1 < code.Length && code[pos+1] is '/' or '*':
                    Comment(ref ctx);
                    break;
                case char c when char.IsDigit(c):
                    res.Add(Int(ref ctx));
                    break;
                case char c when operators.Contains(c):
                    {
                        secondPartOperators.TryGetValue(c, out var cc);
                        string value = "";
                        Position startPos = curPos;
                        if (secondPartOperators.TryGetValue(c, out string s) && pos + 1 < code.Length && s.Contains(code[pos + 1]))
                        {
                            value = ctx.Builder.Clear().Append(c).Append(code[pos + 1]).ToString();
                            pos++;
                            curPos.Column++;
                        }
                        else
                        {
                            value = c.ToString();
                        }
                        pos++;
                        curPos.Column++;
                        res.Add(new Token { Type = TokenType.Operator, Value = value, Pos = startPos, File = ctx.File });
                    }
                    break;
                case char c when special.TryGetValue(c, out var type):
                    res.Add(new Token { Type = type, Value = c.ToString(), Pos = curPos, File = ctx.File });
                    pos++;
                    curPos.Column++;
                    break;
                case char c when brackets.Contains(c):
                    res.Add(new Token { Type = TokenType.Bracket, Value = c.ToString(), Pos = curPos, File = ctx.File });
                    pos++;
                    curPos.Column++;
                    break;
                case char c when char.IsLetter(c) || c == '_':
                    res.Add(Id(ref ctx));
                    break;
                case '"':
                    res.Add(Str(ref ctx));
                    break;
                case '\'':
                    res.Add(Char(ref ctx));
                    break;
                case '\n':
                    curPos = new Position(1, curPos.Line + 1);
                    pos++;
                    break;
                default:
                    curPos.Column++;
                    pos++;
                    break;
            }
        }
        res.Add(new Token { Type = TokenType.EOF, Value = "EOF", Pos = curPos, File = ctx.File });
        return res;
    }
    private static void MultilineComment(ref Context ctx) 
    {
        ref int pos = ref ctx.Pos;
        ref Position cpos = ref ctx.CurrentPos;
        pos++;
        cpos.Column++;
        while (pos < ctx.Code.Length) 
        {
            if (ctx.Code[pos] == '*' && pos + 1 < ctx.Code.Length && ctx.Code[pos + 1] == '/')
            {
                pos += 2;
                cpos.Column += 2;
                break;
            }
            else if (ctx.Code[pos] == '\n') 
            {
                pos++;
                cpos.Column = 1;
                cpos.Line++;
            }
            else { pos++; cpos.Column++; }
        }
    }
    private static void Comment(ref Context ctx) 
    { 
        ref int pos = ref ctx.Pos;
        ref int cpos = ref ctx.CurrentPos.Column;
        pos++;
        cpos++;
        if (pos < ctx.Code.Length && ctx.Code[pos] == '*')
        {
            MultilineComment(ref ctx);
        }
        else if (pos < ctx.Code.Length && ctx.Code[pos] == '/')
        {
            pos++;
            cpos++;
            while (pos < ctx.Code.Length && ctx.Code[pos] is not '\n')
            { 
                pos++;
                cpos++;
            }
        }
        else 
        {
            ctx.Errors.Add(new Error("Expected single-line or multi-line comment", ctx.File, ctx.CurrentPos));
        }
    }
    private static Token Id(ref Context ctx)
    {
        ref int pos = ref ctx.Pos;
        ref Position curPos = ref ctx.CurrentPos;
        Position start = ctx.CurrentPos;
        var sb = ctx.Builder;
        var code = ctx.Code;
        sb.Clear();
        while (pos < code.Length && char.IsLetterOrDigit(code[pos]) || code[pos] == '_')
        {
            sb.Append(code[pos++]);
            curPos.Column++;
        }
        var value = sb.ToString();
        var type = keywords.Contains(value) ? TokenType.Keyword : TokenType.Id;
        return new Token { Type = type, Value = value, Pos = start, File = ctx.File };
    }
    private static Token Int(ref Context ctx)
    {
        ref int pos = ref ctx.Pos;
        ref Position curPos = ref ctx.CurrentPos;
        Position start = curPos;
        var code = ctx.Code;
        var sb = ctx.Builder.Clear();
        bool containsSpecifier = false;
        while (pos < code.Length && (Uri.IsHexDigit(code[pos]) || (code[pos] is 'x' or 'b' && !containsSpecifier)))
        {
            if(code[pos] is 'x' or 'b')
                containsSpecifier = true;
            sb.Append(code[pos++]);
            curPos.Column++;
        }
        var type = TokenType.Integer;
        var value = sb.ToString();
        if(value.StartsWith("0x"))
            type = TokenType.HexInteger;
        else if(value.StartsWith("0b"))
            type = TokenType.BinInteger;
        else
            type = TokenType.Integer;
        return new Token { Type = type, Value = value, Pos = start, File = ctx.File };
    }
    private static Token Str(ref Context ctx)
    {
        ref int pos = ref ctx.Pos;
        pos++;
        ref Position curPos = ref ctx.CurrentPos;
        curPos.Column++;
        Position start = curPos;
        var code = ctx.Code;
        var sb = ctx.Builder.Clear();
        while(pos < code.Length && code[pos] is not '"')
        {
            if(code[pos] == '\\' && pos + 1 < code.Length)
            {
                var c = code[++pos] switch 
                {
                    '\\' => '\\',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '0' => '\0',
                    '"' => '\"',
                    _ => '4',
                };
                if(c == '4')
                    ctx.Errors.Add(new Error($"Unknown escape sequence '\\{c}'", ctx.File, new Position(curPos.Line, curPos.Column + pos)));
                sb.Append(c);
                pos++;
                curPos.Column += 2;
            }
            else 
            {
                sb.Append(code[pos++]);
                curPos.Column++;
            }
        }        
        pos++;
        curPos.Column++;
        return new Token { Type = TokenType.String, Pos = start, Value = sb.ToString(), File = ctx.File };
    }
    private static Token Char(ref Context ctx)
    {
        ref int pos = ref ctx.Pos;
        ref Position curPos = ref ctx.CurrentPos;
        Position start = curPos;
        var code = ctx.Code;
        var c = '\0';
        if(pos + 2 >= code.Length || code[pos + 2] is not '\'')
        {
            ctx.Errors.Add(new Error("Invalid character", ctx.File, start));
            pos += 3;
            curPos.Column += 3;
        }
        else 
        {
            c = code[pos + 1];
            pos += 3;
            curPos.Column += 3;
        }
        return new Token { Type = TokenType.Char, Value = c.ToString(), Pos = start, File = ctx.File};
    }
}
