using System.Diagnostics;
using YLang.AST;

namespace YLang;

public static class Parser
{
    private ref struct Context
    {
        public int Pos = 0;
        public List<Token> Tokens = null!;
        public List<Error> Errors = new();
        public HashSet<string> Included = new(), Symbols = null!;
        public Context() { }
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
                fallback.Pos = res.Pos;
                fallback.File = res.File;
                Errors.Add(new Error($"Unexpected {res}", res.File, res.Pos));
                return fallback;
            }
        }

        public bool MatchKw(string kw, out Token token)
            => Match(MatchGroup.MatchKeyword(kw), out token);
        public bool MatchOp(string op, out Token token)
            => Match(MatchGroup.MatchOp(op), out token);


    }
    private static List<Token> Preprocess(List<Token> tokens, HashSet<string> definedSymbols)
    {
        void Skip(ref int i, List<Token> tokens)
        {
            while (tokens[i] is not Token { Type: TokenType.Preprocessor, Value: "endif" })
            {
                if (tokens[i] is Token { Type: TokenType.Preprocessor, Value: "if" })
                {
                    i++;
                    Skip(ref i, tokens);
                }
                i++;
            }
            i--;
        }
        var result = new List<Token>(tokens.Count);
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Type is TokenType.Preprocessor)
            {
                if (token.Value is "if")
                {
                    if (i + 1 < tokens.Count && tokens[i + 1] is Token { Type: TokenType.Id, Value: var symbol })
                    {
                        i++;
                        if (!definedSymbols.Contains(symbol))
                        {
                            Skip(ref i, tokens);
                        }
                    }
                }
            }
            else
            {
                result.Add(token);
            }
        }
        return result;
    }
    public static List<Statement> Parse(List<Token> tokens, out List<Error> errors, HashSet<string> definedSymbols)
    {
        tokens = Preprocess(tokens, definedSymbols);
        tokens.ForEach(x => Console.WriteLine(x));
        var ctx = new Context() { Tokens = tokens, Symbols = definedSymbols };
        errors = ctx.Errors;
        var res = new List<Statement>();
        while (ctx.Pos < tokens.Count && tokens[ctx.Pos].Type != TokenType.EOF)
        {
            var m = ctx.Match(MatchGroup.MatchKeyword("fn").OrKeyword("let").OrKeyword("include").OrKeyword("struct").OrKeyword("dllimport"), out var t);
            Console.WriteLine($"{m} {t}");
            if (m)
            {
                switch (t.Value)
                {
                    case "fn": res.Add(ParseFunction(ref ctx, t.Pos)); break;
                    case "let": res.Add(Let(ref ctx, t.Pos)); break;
                    case "struct": res.Add(Struct(ref ctx, t.Pos)); break;
                    case "dllimport": res.Add(DllImport(ref ctx, t.Pos)); break;
                    case "include":
                        {
                            var file = ctx.ForceMatch(MatchGroup.Match(TokenType.String), new Token { Type = TokenType.String, Value = "<undefined>" });
                            if (file.Value != "<undefined>")
                            {
                                if (File.Exists(file.Value))
                                {
                                    if (!ctx.Included.Contains(file.Value))
                                    {
                                        ctx.Included.Add(file.Value);
                                        var toks = Lexer.Tokenize(File.ReadAllText(file.Value), Path.GetFileName(file.Value), out var errs);
                                        toks = Preprocess(toks, definedSymbols);
                                        ctx.Errors.AddRange(errs);
                                        ctx.ForceMatch(MatchGroup.Semicolon);
                                        ctx.Tokens.InsertRange(ctx.Pos, toks.Take(toks.Count - 1));
                                    }
                                }
                                else
                                {
                                    ctx.Errors.Add(new Error($"Cannot find file {file.Value}", file.File, file.Pos));
                                }
                            }
                        }
                        break;
                }
            }
            else
                ctx.Pos++;
        }
        return res;
    }

    private static Statement DllImport(ref Context ctx, Position pos)
    {
        var cconv = CallingConvention.Windows64;
        if(ctx.Match(MatchGroup.MatchKeyword("Y").OrKeyword("WindowsX64"), out var conv))
            cconv = Enum.Parse<CallingConvention>(conv.Value);
        var dllname = ctx.ForceMatch(MatchGroup.Match(TokenType.String));
        var imports = new List<ExternFunctionDefinition>();
        ctx.ForceMatch(MatchGroup.LBRC);
        while (!ctx.Match(MatchGroup.RBRC, out _))
        {
            imports.Add(ExternFn(ref ctx));
        }
        return new DllImportStatement(dllname.Value, imports, dllname.File, pos, cconv);
    }

    private static ExternFunctionDefinition ExternFn(ref Context ctx)
    {
        var pos = ctx.ForceMatch(MatchGroup.MatchKeyword("extern")).Pos;
        ctx.ForceMatch(MatchGroup.MatchKeyword("fn"));
        var fnname = ctx.ForceMatch(MatchGroup.Match(TokenType.Id), Token.UndefinedId);
        var parameters = new List<Parameter>();
        ctx.ForceMatch(MatchGroup.LP);
        while (!ctx.Match(MatchGroup.RP, out _))
        {
            var pname = ctx.ForceMatch(MatchGroup.Id, Token.UndefinedId);
            ctx.ForceMatch(MatchGroup.Colon);
            var type = ParseType(ref ctx);
            ctx.Match(MatchGroup.Match(TokenType.Comma), out var _);
            parameters.Add(new Parameter(pname.Value, type));
        }
        TypeExpression? retType = null;
        if (ctx.Match(MatchGroup.Colon, out _))
        {
            retType = ParseType(ref ctx);
        }
        string? importname = null;
        if(ctx.Match(MatchGroup.MatchKeyword("from"), out _))
        {
            importname = ctx.ForceMatch(MatchGroup.Match(TokenType.String)).Value;
        }
        ctx.ForceMatch(MatchGroup.Semicolon);
        return new ExternFunctionDefinition(fnname.Value, parameters, fnname.File, pos, retType, importname);
    }

    private static Statement Struct(ref Context ctx, Position pos)
    {
        var name = ctx.ForceMatch(MatchGroup.Id, Token.UndefinedId);
        ctx.ForceMatch(MatchGroup.LBRC);
        var fields = new List<FieldDefinitionStatementBase>();
        var constructors = new List<ConstructorDefinitionStatement>();
        var functions = new List<FnDefinitionStatement>();
        while (!ctx.Match(MatchGroup.RBRC, out _))
        {
            var token = ctx.ForceMatch(MatchGroup.MatchKeyword("constructor").OrKeyword("union").Or(TokenType.Id).OrKeyword("fn"));
            Console.WriteLine($"STRUCT: {token}");
            switch (token)
            {
                case { Type: TokenType.Keyword, Value: "constructor" }:
                    constructors.Add(Constructor(ref ctx));
                    break;
                case { Type: TokenType.Id }:
                    ctx.Pos--;
                    fields.Add(Field(ref ctx));
                    break;
                case { Type: TokenType.Keyword, Value: "fn", Pos: var position }:
                    functions.Add(ParseFunction(ref ctx, position));
                    break;
                case { Type: TokenType.Keyword, Value: "union", Pos: var unionpos, File: var unionfile }:
                    var unionfields = new List<FieldDefinitionStatement>();
                    ctx.ForceMatch(MatchGroup.LBRC);
                    while(!ctx.Match(MatchGroup.RBRC, out _))
                    {
                        unionfields.Add(Field(ref ctx));
                    }
                    fields.Add(new UnionDefinitionStatement(unionfields, unionfile, unionpos));
                    break;
            }
        }

        return new StructDefinitionStatement(name.Value, fields, constructors, functions, name.Pos, name.File);
    }
    private static ConstructorDefinitionStatement Constructor(ref Context ctx)
    {
        ctx.ForceMatch(MatchGroup.LP);
        var parameters = new List<Parameter>();
        while (!ctx.Match(MatchGroup.RP, out _))
        {
            var pname = ctx.ForceMatch(MatchGroup.Id, Token.UndefinedId);
            ctx.ForceMatch(MatchGroup.Colon);
            var type = ParseType(ref ctx);
            ctx.Match(MatchGroup.Match(TokenType.Comma), out var _);
            parameters.Add(new Parameter(pname.Value, type));
        }

        var body = Statement(ref ctx);
        return new ConstructorDefinitionStatement(parameters, body);
    }

    private static FieldDefinitionStatement Field(ref Context ctx)
    {
        var name = ctx.ForceMatch(MatchGroup.Id, Token.UndefinedId);
        ctx.ForceMatch(MatchGroup.Colon);
        var type = ParseType(ref ctx);
        ctx.ForceMatch(MatchGroup.Semicolon);
        return new FieldDefinitionStatement(name.Value, type, name.Pos);
    }

    private static FnDefinitionStatement ParseFunction(ref Context ctx, Position pos)
    {
        var name = ctx.ForceMatch(MatchGroup.Id, Token.UndefinedId);
        var parameters = new List<Parameter>();
        ctx.ForceMatch(MatchGroup.LP);
        while (!ctx.Match(MatchGroup.RP, out _))
        {
            var pname = ctx.ForceMatch(MatchGroup.Id, Token.UndefinedId);
            ctx.ForceMatch(MatchGroup.Colon);
            var type = ParseType(ref ctx);
            ctx.Match(MatchGroup.Match(TokenType.Comma), out var _);
            parameters.Add(new Parameter(pname.Value, type));
        }
        TypeExpression? retType = null;
        if (ctx.Match(MatchGroup.Colon, out _))
        {
            retType = ParseType(ref ctx);
        }
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
        while (!ctx.Match(MatchGroup.RBRC, out _))
        {
            if (ctx.Match(TokenType.EOF, out var eof))
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
        while (!ctx.Match(MatchGroup.RBRC, out _))
        {
            sts.Add(Statement(ref ctx));
        }
        return new BlockStatement(sts, pos, file);
    }
    private static Statement AssignOrFunctionCall(ref Context ctx)
    {
        ctx.Pos--;
        Console.WriteLine(ctx.Tokens[ctx.Pos]);
        var expr = SimpleExpression(ref ctx);
        Console.WriteLine(expr);
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
        if (ctx.MatchKw("else", out _))
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
        while (ctx.MatchOp("*", out _))
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
        if (ctx.Match(MatchGroup.Colon, out _))
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
        var first = BinaryAnd(ref ctx);
        if (ctx.MatchOp("||", out _))
        {
            var snd = BooleanOr(ref ctx);
            return new BinaryExpression(first, snd, "||");
        }
        return first;
    }
    private static Expression BinaryAnd(ref Context ctx)
    {
        var first = BinaryXor(ref ctx);
        if (ctx.MatchOp("&", out _))
        {
            var snd = BinaryAnd(ref ctx);
            return new BinaryExpression(first, snd, "&");
        }
        return first;
    }
    private static Expression BinaryXor(ref Context ctx)
    {
        var first = BinaryOr(ref ctx);
        if (ctx.MatchOp("^", out _))
        {
            var snd = BinaryXor(ref ctx);
            return new BinaryExpression(first, snd, "^");
        }
        return first;
    }
    private static Expression BinaryOr(ref Context ctx)
    {
        var first = Boolean(ref ctx);
        if (ctx.MatchOp("|", out _))
        {
            var snd = BinaryOr(ref ctx);
            return new BinaryExpression(first, snd, "|");
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
        if (ctx.Match(MatchGroup.MatchOp(">>").OrOp("<<"), out var op))
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
        if (ctx.Match(MatchGroup.MatchOp("*").OrOp("/").OrOp("%"), out var op))
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
        Expression expr = null!;
        if (ctx.Match(MatchGroup.MatchOp("*"), out var op))
        {
            pos = op.Pos;
            expr = Deref(ref ctx);
        }
        return expr is null ? Ref(ref ctx) : new DereferenceExpression(expr, pos);
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
        var type = ParseType(ref ctx);
        ctx.ForceMatch(MatchGroup.LP);
        var args = CommanSeperated(ref ctx, MatchGroup.RP);
        return new NewObjExpression(type, args, pos, type.File);
    }
    private static Expression BoxExpression(ref Context ctx, Position pos)
    {
        var expr = Expression(ref ctx);
        return new BoxExpression(expr, pos);
    }
    private static Expression SimpleExpression(ref Context ctx)
    {
        var matched = ctx.ForceMatch(MatchGroup
                .Match(TokenType.Integer)
                .Or(TokenType.HexInteger)
                .Or(TokenType.BinInteger)
                .Or(TokenType.Id)
                .Or(TokenType.Char)
                .Or(TokenType.String)
                .OrKeyword("cast")
                .OrKeyword("new")
                .OrKeyword("box")
                .OrKeyword("typeof")
                .OrKeyword("null")
                .Or(MatchGroup.LP)
                .OrKeyword("false")
                .OrKeyword("true"), Token.UndefinedId);
        if (matched.Value == "<undefined>")
            return new IntegerExpression(0, matched.Pos, matched.File);
        Expression expr = matched switch
        {
            { Type: TokenType.Integer, Value: var val, Pos: var pos } => Int(matched, ref ctx, pos),
            { Type: TokenType.HexInteger, Value: var val, Pos: var pos } => HexInt(matched, ref ctx, pos),
            { Type: TokenType.BinInteger, Value: var val, Pos: var pos } => BinInt(matched, ref ctx, pos),
            { Type: TokenType.Char, Value: var c, Pos: var pos, File: var file } => Char(c, file, pos),
            { Type: TokenType.String, Value: var str, Pos: var pos, File: var file } => new StringExpression(str, file, pos),
            { Type: TokenType.Id, Value: var val, Pos: var pos, File: var file } => new VariableExpression(val, pos, file),
            { Type: TokenType.Keyword, Value: "cast", Pos: var pos } => Cast(ref ctx, pos),
            { Type: TokenType.Keyword, Value: "typeof", Pos: var pos } => TypeOf(ref ctx, pos),
            { Type: TokenType.Bracket, Value: "(" } => ExpressionThen(ref ctx, MatchGroup.Match(TokenType.Bracket, ")")),
            { Type: TokenType.Keyword, Value: "new", Pos: var pos } => NewObjExpression(ref ctx, pos),
            { Type: TokenType.Keyword, Value: "box", Pos: var pos } => BoxExpression(ref ctx, pos),
            { Type: TokenType.Keyword, Value: "false", Pos: var pos, File: var file } => new BoolExpression(false, pos, file),
            { Type: TokenType.Keyword, Value: "true", Pos: var pos, File: var file } => new BoolExpression(true, pos, file),
            { Type: TokenType.Keyword, Value: "null", Pos: var pos, File: var file } => new NullExpression(pos, file),
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

    private static Expression TypeOf(ref Context ctx, Position pos)
    {
        ctx.ForceMatch(MatchGroup.LP);
        var type = ParseType(ref ctx);
        ctx.ForceMatch(MatchGroup.RP);
        return new TypeOfExpression(type, pos);
    }

    private static Expression Cast(ref Context ctx, Position pos)
    {
        ctx.ForceMatch(MatchGroup.LP);
        var val = Expression(ref ctx);
        ctx.ForceMatch(MatchGroup.Match(TokenType.Comma));
        var type = ParseType(ref ctx);
        ctx.ForceMatch(MatchGroup.RP);
        return new CastExpression(val, type, pos);
    }
    private static Expression Char(string c, string file, Position pos)
    {
        return new CharExpression(c[0], pos, file);
    }

    private static Expression Int(Token token, ref Context ctx, Position pos)
    {
        var value = token.Value;
        //TODO: support uints
        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
            {
                ctx.Errors.Add(new Error($"Integer cannot contain {value[i]}", token.File, new Position { Line = pos.Line, Column = pos.Column + i }));
                return new IntegerExpression(0, pos, token.File);
            }
        }
        if (long.TryParse(value, out var val))
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
        for (int i = 2 /* skip '0x' */; i < value.Length; i++)
        {
            if (!Uri.IsHexDigit(value[i]))
            {
                ctx.Errors.Add(new Error($"Hex integer cannot contain {value[i]}", token.File, new Position { Line = pos.Line, Column = pos.Column + i }));
                return new IntegerExpression(0, pos, token.File);
            }
        }
        if (long.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var val))
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
        for (int i = 2 /* skip '0b' */; i < value.Length; i++)
        {
            if (value[i] != '0' && value[i] != '1')
            {
                ctx.Errors.Add(new Error($"Binary integer cannot contain {value[i]}", token.File, new Position { Line = pos.Line, Column = pos.Column + i }));
                return new IntegerExpression(0, pos, token.File);
            }
        }
        if (Helpers.Try(() => Convert.ToInt64(value.AsSpan(2).ToString(), 2), out var val))
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

