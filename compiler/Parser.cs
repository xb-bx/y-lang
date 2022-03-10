using System.Diagnostics;
using YLang.AST;

namespace YLang;

public static class Parser
{
    private ref struct Context
    {
        public int Pos;
        public List<Token> Tokens = null!;
        public List<Error> Errors = new();
        public bool Match(MatchGroup match, out Token token)
        {
            if (Pos < Tokens.Count)
            {
                token = Tokens[Pos++];
                var res = match.Match(token);
                if (!res)
                    Pos--;
                return res;
            }
            else
            {
                token = default;
                return false;
            }
        }
        public bool Match(TokenType type, out Token token)
            => Match(MatchGroup.Match(type), out token);
        public Token ForceMatch(MatchGroup match, Token fallback = default)
        {
            if (Match(match, out Token res))
            {
                return res;
            }
            else
            {
                var stack = new StackTrace();
                fallback.Pos = res.Pos;
                fallback.File = res.File;
                Errors.Add(new Error($"Unexpected {res} at {stack}", res.File, res.Pos));
                return fallback;
            }
        }

        public bool MatchKw(string kw, out Token token)
            => Match(MatchGroup.MatchKeyword(kw), out token);
        public bool MatchOp(string op, out Token token)
            => Match(MatchGroup.MatchOp(op), out token);


    }
    public static List<Statement> Parse(List<Token> tokens, string filename, out List<Error> errors)
    {
        var ctx = new Context() { Tokens = tokens };
        errors = ctx.Errors;
        var res = new List<Statement>();
        while (ctx.Pos < tokens.Count && tokens[ctx.Pos].Type != TokenType.EOF)
        {
            Console.WriteLine(ctx.Pos);
            var m = ctx.Match(MatchGroup.MatchKeyword("fn"), out var t);
            Console.WriteLine(m);
            Console.WriteLine(ctx.Pos);
            if(m)
                res.Add(ParseFunction(ref ctx, t.Pos));
            else 
                ctx.Pos++;
        }
        return res;
    }
    private static FnDefinitionStatement ParseFunction(ref Context ctx, Position pos)
    {
        var name = ctx.ForceMatch(MatchGroup.Id, Token.UndefinedId);
        var parameters = new List<Parameter>();
        ctx.ForceMatch(MatchGroup.LP);
        while(!ctx.Match(MatchGroup.RP, out _))
        {
            var pname = ctx.ForceMatch(MatchGroup.Id, Token.UndefinedId);
            ctx.ForceMatch(MatchGroup.Colon);
            var type = ParseType(ref ctx);
            ctx.Match(MatchGroup.Match(TokenType.Comma), out var _);
            parameters.Add(new Parameter(pname.Value, type));
        }
        ctx.ForceMatch(MatchGroup.Colon);
        var retType = ParseType(ref ctx);
        var body = Statement(ref ctx);
        return new FnDefinitionStatement(name.Value, parameters, retType, body, pos);
    }
    private static Statement Statement(ref Context ctx)
    {
        var tok = ctx.ForceMatch(MatchGroup
                .MatchKeyword("let")
                .OrKeyword("asm")
                .OrKeyword("ret")
                .OrKeyword("if")
                .OrKeyword("while")
                .OrOp("*")
                .Or(TokenType.Id)
                .Or(MatchGroup.LBRC), 
        Token.UndefinedId);
        Console.WriteLine(string.Join(", ", ctx.Errors.Select(x => x.ToString())));
        var st = tok switch
        {
            { Type: TokenType.Keyword, Value: "let", Pos: var pos } => Let(ref ctx, pos),
            { Type: TokenType.Id } => AssignOrFunctionCall(ref ctx),
            { Type: TokenType.Operator, Value: "*" } => DerefStatement(ref ctx),
            { Type: TokenType.Keyword, Value: "ret", Pos: var pos } => Ret(ref ctx, pos),
            { Type: TokenType.Keyword, Value: "if", Pos: var pos } => IfElse(ref ctx, pos),
            { Type: TokenType.Bracket, Value: "{", Pos: var pos, File: var file } => Block(ref ctx, pos, file),
            { Type: TokenType.Keyword, Value: "while", Pos: var pos } => While(ref ctx, pos),
            { Type: TokenType.Keyword, Value: "asm", Pos: var pos } => InlineAsm(ref ctx, pos),
            _ => throw new(tok.ToString())
        };
        return st;
    }
    private static Statement InlineAsm(ref Context ctx, Position pos)
    {
        ctx.ForceMatch(MatchGroup.LBRC);
        var body = new List<Token>();
        while(!ctx.Match(MatchGroup.RBRC, out _))
        {
            if(ctx.Match(TokenType.EOF, out var eof))
            {
                ctx.Errors.Add(new Error("Unexpected EOF", eof.File, pos));
                break;
            }
            else 
            {
                body.Add(ctx.Tokens[ctx.Pos++]);
            }
        }
        return new InlineAsmStatement(body, pos);
    }
    private static Statement While(ref Context ctx, Position pos)
    {
        var cond = Expression(ref ctx);
        var body = Statement(ref ctx);
        return new WhileStatement(cond, body, pos);
    }
    private static Statement Block(ref Context ctx, Position pos, string file)
    {
        var sts = new List<Statement>();
        while(!ctx.Match(MatchGroup.RBRC, out _))
        {
            sts.Add(Statement(ref ctx));
        }
        Console.WriteLine(ctx.Tokens[ctx.Pos]);
        return new BlockStatement(sts, pos, file);
    }
    private static Statement AssignOrFunctionCall(ref Context ctx)
    {
        ctx.Pos--;
        var expr = SimpleExpression(ref ctx);
        if (expr is not FunctionCallExpression and not ValueCallExpression and not MethodCallExpression)
        {
            ctx.ForceMatch(MatchGroup.EQ);
            var value = Expression(ref ctx);
            ctx.ForceMatch(MatchGroup.Semicolon);
            return new AssignStatement(expr, value);
        }
        else
        {
            ctx.ForceMatch(MatchGroup.Semicolon);
            return new CallStatement(expr);
        }
    }
    private static Statement DerefStatement(ref Context ctx)
    {
        ctx.Pos--;
        var expr = Parser.Deref(ref ctx);
        ctx.ForceMatch(MatchGroup.EQ);
        var value = Expression(ref ctx);
        ctx.ForceMatch(MatchGroup.Semicolon);
        return new AssignStatement(expr, value);
    }
    private static Statement Ret(ref Context ctx, Position pos)
    {
        if (ctx.Match(TokenType.Semicolon, out var t))
        {
            return new RetStatement(null, pos, t.File);
        }
        else
        {
            var expr = Expression(ref ctx);
            ctx.ForceMatch(MatchGroup.Semicolon);
            return new RetStatement(expr, pos, expr.File);
        }
    }
    private static Statement IfElse(ref Context ctx, Position pos)
    {
        var cond = Expression(ref ctx);
        var body = Statement(ref ctx);
        if(ctx.MatchKw("else", out _))
        {
            var els = Statement(ref ctx);
            return new IfElseStatement(cond, body, els, pos);
        }
        else 
        {
            return new IfElseStatement(cond, body, null, pos);
        }
    }
    private static TypeExpression ParseType(ref Context ctx)
    {
        var tok = ctx.ForceMatch(MatchGroup.Id.OrOp("*"), Token.UndefinedId);
        var type = tok switch 
        {
            { Type: TokenType.Operator, Value: "*", Pos: var pos } => PtrType(ref ctx, pos),
            { Type: TokenType.Id, Value: var val, Pos: var pos, File: var file } => new TypeExpression(val, pos, file),
            _ => throw new Exception($"It shouldnt happen {tok}")
        };
        return type;
    }
    private static PtrType PtrType(ref Context ctx, Position pos)
    {
        int depth = 1;
        while(ctx.MatchOp("*", out _))
        {
            depth++;
        }
        var type = ctx.ForceMatch(MatchGroup.Id, Token.UndefinedId);
        return new PtrType(new TypeExpression(type.Value, type.Pos, type.File), depth, pos);
    }
    private static Statement Let(ref Context ctx, Position pos)
    {
        var name = ctx.ForceMatch(MatchGroup.Id, Token.UndefinedId);
        TypeExpression? type = null;
        if(ctx.Match(MatchGroup.Colon, out _))
        {
            type = ParseType(ref ctx);
        }
        ctx.ForceMatch(MatchGroup.EQ);
        var value = Expression(ref ctx);
        ctx.ForceMatch(MatchGroup.Semicolon);
        return new LetStatement(name.Value, type, value, pos);
    }
    private static Expression ExpressionThen(ref Context ctx, MatchGroup match)
    {
        var expr = Expression(ref ctx);
        ctx.ForceMatch(match);
        return expr;
    }
    private static Expression Expression(ref Context ctx)
        => BooleanAnd(ref ctx);
    private static List<Expression> CommanSeperated(ref Context ctx, MatchGroup end)
    {
        var res = new List<Expression>();
        if (ctx.Match(end, out _))
        {
            return res;
        }
        else
        {
            do
            {
                res.Add(Expression(ref ctx));
            }
            while (ctx.Match(TokenType.Comma, out _));
            ctx.ForceMatch(end);
        }
        return res;
    }
    private static Expression BooleanAnd(ref Context ctx)
    {
        var first = BooleanOr(ref ctx);
        if (ctx.MatchOp("&&", out _))
        {
            var snd = BooleanAnd(ref ctx);
            return new BinaryExpression(first, snd, "&&");
        }
        return first;
    }
    private static Expression BooleanOr(ref Context ctx)
    {
        var first = Boolean(ref ctx);
        if (ctx.MatchOp("||", out _))
        {
            var snd = BooleanOr(ref ctx);
            return new BinaryExpression(first, snd, "||");
        }
        return first;
    }
    private static Expression Boolean(ref Context ctx)
    {
        var first = Shift(ref ctx);
        if (ctx.Match(MatchGroup.MatchOp(">").OrOp(">=").OrOp("<").OrOp("<=").OrOp("==").OrOp("!="), out var op))
        {
            var snd = Boolean(ref ctx);
            return new BinaryExpression(first, snd, op.Value);
        }
        return first;
    }
    private static Expression Shift(ref Context ctx)
    {
        var first = Additive(ref ctx);
        if(ctx.Match(MatchGroup.MatchOp(">>").OrOp("<<"), out var op))
        {
            var snd = Shift(ref ctx);
            return new BinaryExpression(first, snd, op.Value);
        }
        return first;
    }
    private static Expression Additive(ref Context ctx)
    {
        var first = Multiplicative(ref ctx);
        if (ctx.Match(MatchGroup.MatchOp("+").OrOp("-"), out var op))
        {
            var snd = Additive(ref ctx);
            return new BinaryExpression(first, snd, op.Value);
        }
        return first;
    }
    private static Expression Multiplicative(ref Context ctx)
    {
        var first = Negate(ref ctx);
        if (ctx.Match(MatchGroup.MatchOp("*").OrOp("/"), out var op))
        {
            var snd = Multiplicative(ref ctx);
            return new BinaryExpression(first, snd, op.Value);
        }
        return first;
    }
    private static Expression Negate(ref Context ctx)
    {
        if (ctx.Match(MatchGroup.MatchOp("-"), out var op))
        {
            return new NegateExpression(Deref(ref ctx), op.Pos);
        }
        return Deref(ref ctx);
    }
    private static Expression Deref(ref Context ctx)
    {
        Position pos = default;
        int derefCount = 0;
        while (ctx.Match(MatchGroup.MatchOp("*"), out var op))
        {
            if (derefCount == 1)
                pos = op.Pos;
            derefCount++;
        }
        if (derefCount > 0)
        {
            return new DereferenceExpression(Ref(ref ctx), derefCount, pos);
        }
        else
        {
            return Ref(ref ctx);
        }
    }
    private static Expression Ref(ref Context ctx)
    {
        if (ctx.MatchOp("&", out var op))
        {
            return new RefExpression(SimpleExpression(ref ctx), op.Pos);
        }
        return SimpleExpression(ref ctx);
    }
    private static Expression NewObjExpression(ref Context ctx, Position pos)
    {
        var name = ctx.ForceMatch(MatchGroup.Id, new Token { Type = TokenType.Keyword, Value = "Undefined" });
        ctx.ForceMatch(MatchGroup.LP);
        var args = CommanSeperated(ref ctx, MatchGroup.RP);
        return new NewObjExpression(name.Value, args, pos, name.File);
    }
    private static Expression SimpleExpression(ref Context ctx)
    {
        var matched = ctx.ForceMatch(MatchGroup
                .Match(TokenType.Integer)
                .Or(TokenType.HexInteger)
                .Or(TokenType.BinInteger)
                .Or(TokenType.Id)
                .OrKeyword("new")
                .Or(MatchGroup.LP)
                .OrKeyword("false")
                .OrKeyword("true"), Token.UndefinedId);
        if(matched.Value == "<undefined>")
            return new IntegerExpression(0, matched.Pos, matched.File);
        Expression expr = matched switch
        {
            { Type: TokenType.Integer, Value: var val, Pos: var pos } => Int(matched, ref ctx, pos),
            { Type: TokenType.HexInteger, Value: var val, Pos: var pos} => HexInt(matched, ref ctx, pos),
            { Type: TokenType.BinInteger, Value: var val, Pos: var pos} => BinInt(matched, ref ctx, pos),
            { Type: TokenType.Id, Value: var val, Pos: var pos, File: var file } => new VariableExpression(val, pos, file),
            { Type: TokenType.Bracket, Value: "(" } => ExpressionThen(ref ctx, MatchGroup.Match(TokenType.Bracket, ")")),
            { Type: TokenType.Keyword, Value: "new", Pos: var pos } => NewObjExpression(ref ctx, pos),
            { Type: TokenType.Keyword, Value: "false", Pos: var pos, File: var file } => new BoolExpression(false, pos, file),
            { Type: TokenType.Keyword, Value: "true", Pos: var pos, File: var file } => new BoolExpression(true, pos, file),
            _ => throw new()
        };
        while (ctx.Match(MatchGroup.Match(TokenType.Bracket, "(").Or(TokenType.Bracket, "[").Or(TokenType.Dot), out var token))
        {
            expr = token.Value switch
            {
                "(" => expr = expr switch
                {
                    VariableExpression varr => new FunctionCallExpression(varr.Name, CommanSeperated(ref ctx, MatchGroup.RP), varr.Pos, varr.File),
                    MemberAccessExpression member => new MethodCallExpression(member.MemberName, CommanSeperated(ref ctx, MatchGroup.RP), member.Expr),
                    ValueCallExpression value => new ValueCallExpression(value, CommanSeperated(ref ctx, MatchGroup.RP)),
                    var x => throw new Exception($"It wont throw {x.GetType()} {x}"),
                },
                "[" => new IndexExpression(expr, CommanSeperated(ref ctx, MatchGroup.RBR), token.Pos),
                "." => new MemberAccessExpression(expr, ctx.ForceMatch(MatchGroup.Id, new Token { Type = TokenType.Id, Value = "Undefined" }).Value, token.Pos),
                _ => throw new Exception("It wont throw")
            };
        }
        return expr;
    }
    private static Expression Int(Token token, ref Context ctx, Position pos)
    {
        var value = token.Value;
        //TODO: support uints
        for(int i = 0; i < value.Length; i++)
        {
            if(!char.IsDigit(value[i]))
            {
                ctx.Errors.Add(new Error($"Integer cannot contain {value[i]}", token.File, new Position { Line = pos.Line, Column = pos.Column + i}));
                return new IntegerExpression(0, pos, token.File);
            }
        }
        if(long.TryParse(value, out var val))
        {
            return new IntegerExpression(val, pos, token.File);
        }
        else 
        {
            ctx.Errors.Add(new Error($"Invalid integer {value}", token.File, pos));
            return new IntegerExpression(0, pos, token.File);
        }
    }
    private static Expression HexInt(Token token, ref Context ctx, Position pos)
    {
        var value = token.Value;
        for(int i = 2 /* skip '0x' */; i < value.Length; i++)
        {
            if(!Uri.IsHexDigit(value[i]))
            {
                ctx.Errors.Add(new Error($"Hex integer cannot contain {value[i]}", token.File, new Position { Line = pos.Line, Column = pos.Column + i}));
                return new IntegerExpression(0, pos, token.File);
            }
        }
        if(long.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var val))
        {
            return new IntegerExpression(val, pos, token.File);
        }
        else 
        {
            ctx.Errors.Add(new Error($"Invalid hex integer {value}", token.File, pos));
            return new IntegerExpression(0, pos, token.File);
        }
    }
    private static Expression BinInt(Token token, ref Context ctx, Position pos)
    {
        var value = token.Value;
        for(int i = 2 /* skip '0b' */; i < value.Length; i++)
        {
            if(value[i] is not '1' or '0')
            {
                ctx.Errors.Add(new Error($"Binary integer cannot contain {value[i]}", token.File, new Position { Line = pos.Line, Column = pos.Column + i}));
                return new IntegerExpression(0, pos, token.File);
            }
        }
        if(Helpers.Try(() => Convert.ToInt64(value.AsSpan(2).ToString(), 2), out var val))
        {
            return new IntegerExpression(val, pos, token.File);
        }
        else 
        {
            ctx.Errors.Add(new Error($"Invalid binary integer {value}", token.File, pos));
            return new IntegerExpression(0, pos, token.File);
        }
    }
}

