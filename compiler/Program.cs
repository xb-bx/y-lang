using System.Text;
using YLang.IR;
using YLang.AST;

namespace YLang;

public class Program
{
    public static void Main(string[] args)
    {
        var tokens = Lexer.Tokenize(File.ReadAllText(args[0]), "source.sl", out var lerrors);
        foreach (var token in tokens)
            Console.WriteLine(token);
        Console.WriteLine(lerrors.Count);
        var statements = Parser.Parse(tokens, "source.sl", out var errors);
        foreach (var statement in statements)
            Console.WriteLine($"{statement.GetType()}\n{Format(((statement as FnDefinitionStatement).Body as BlockStatement).Statements)}");
        var cerrors = Compiler.Compile(statements, "prog.asm");
        foreach (var error in errors.Concat(lerrors).Concat(cerrors))
            Console.WriteLine(error);
    }
    public static string Format(List<Statement> statements, int indentMult = 4)
    {
        var sb = new StringBuilder();
        foreach (var statement in statements)
            Format(statement, sb, 0, indentMult);
        return sb.ToString();
    }
    public static void Format(Statement stat, StringBuilder sb, int indent, int indentMult = 4)
    {
        switch (stat)
        {
            case LetStatement let:
                sb.Append(' ', indent * indentMult).AppendLine($"let {let.Name}: {let.Type} = {let.Value};");
                break;
            case AssignStatement assign:
                sb.Append(' ', indent * indentMult).AppendLine($"{assign.Expr} = {assign.Value};");
                break;
            case CallStatement call:
                sb.Append(' ', indent * indentMult).AppendLine($"{call.Call};");
                break;
            case RetStatement ret:
                sb.Append(' ', indent * indentMult).AppendLine($"ret {ret.Value};");
                break;
            case IfElseStatement ifelse:
                sb.Append(' ', indent * indentMult).Append("if ").AppendLine(ifelse.Condition.ToString());
                Format(ifelse.Body, sb, indent + 1);
                if (ifelse.Else is not null)
                {
                    sb.Append(' ', indent * indentMult).AppendLine("else");
                    Format(ifelse.Else, sb, indent + 1);
                }
                break;
            case BlockStatement block:
                sb.Append(' ', (indent - 1) * indentMult).AppendLine("{");
                foreach (var st in block.Statements)
                    Format(st, sb, indent);
                sb.Append(' ', (indent - 1) * indentMult).AppendLine("}");
                break;
            case WhileStatement wh:
                sb.Append(' ', indent * indentMult).Append("while ").AppendLine(wh.Cond.ToString());
                Format(wh.Body, sb, indent + 1, indentMult);
                break;
            case FnDefinitionStatement fn:
                sb.Append(' ', indent * indentMult).AppendLine($"fn {fn.Name}({string.Join(", ", fn.Params)}): {fn.RetType}");
                Format(fn.Body, sb, indent + 1, indentMult);
                break;
            case InlineAsmStatement asm:
                {
                    sb.Append(' ', indent * indentMult).AppendLine("asm");
                    var body = asm.Body;
                    sb.Append(' ', indent * indentMult).AppendLine("{");
                    if (body.Count > 0)
                    {
                        int prevLine = body[0].Pos.Line;
                        indent++;
                        sb.Append(' ', indent * indentMult);
                        foreach (var token in body)
                        {
                            if (prevLine != token.Pos.Line)
                            {
                                sb.AppendLine().Append(' ', indent * indentMult);
                                prevLine = token.Pos.Line;
                            }
                            sb.Append(token.Value).Append(' ');
                        }
                        sb.AppendLine();
                        indent--;
                    }
                    sb.Append(' ', indent * indentMult).AppendLine("}");
                }
                break;
        }
    }
}
public static class Compiler
{
    private ref struct Context
    {
        public Dictionary<string, TypeInfo> Types = null!;
        public List<FnInfo> Fns = null!;
        public List<Error> Errors = new();
        public TypeInfo? GetTypeInfo(TypeExpression typeexpr)
        {
            if (typeexpr is PtrType ptr)
            {
                if (Types.TryGetValue(ptr.UnderlayingType.Name, out var type))
                {
                    return new PtrTypeInfo(type, ptr.PtrDepth);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                if (Types.TryGetValue(typeexpr.Name, out var type))
                {
                    return type;
                }
                else
                {
                    return null;
                }
            }
        }
        public TypeInfo Void = null!;
        public TypeInfo I64 = null!;
        public TypeInfo Bool = null!;
    }
    public static List<Error> Compile(List<Statement> statements, string output)
    {
        var ctx = new Context();
        AddDefaultTypes(ref ctx);
        var fns = statements.OfType<FnDefinitionStatement>().ToList();
        if (fns.Count == 0)
            return new();
        AddFunctions(ref ctx, fns);
        foreach (var (key, val) in ctx.Types)
            Console.WriteLine($"{key} = {val}");
        foreach (var fn in ctx.Fns)
            Console.WriteLine($"{fn}");
        var res = new List<string>();
        foreach (var fn in ctx.Fns)
        {
            Console.WriteLine($"Compiling {fn}");
            var f = CompileFn(ref ctx, fn);
            Console.WriteLine("VARS");
            f.Variables.ForEach(Console.WriteLine);
            f.Info.Compiled.ForEach(Console.WriteLine);
            res.AddRange(IRCompiler.Compile(f.Info.Compiled, f.Variables, fn));
        }
        var result = new StringBuilder();
        result
            .AppendLine("format PE64 CONSOLE")
            .AppendLine("entry __start")
            .AppendLine("include 'win64axp.inc'")
            .AppendLine("true = 1")
            .AppendLine("false = 1")
            .AppendLine("section '.code' code readable executable")
            .AppendLine("__start:")
            .AppendLine("call main")
            .AppendLine("invoke ExitProcess, 0")
            .AppendLine(string.Join('\n', res))
            .AppendLine("section '.idata' import data readable writeable")
            .AppendLine("library kernel32,'kernel32.dll'")
            .AppendLine("include 'api\\kernel32.inc'")
            .AppendLine("include 'api\\user32.inc'");
        File.WriteAllText(output, result.ToString());
        return ctx.Errors;
    }
    private ref struct FunctionContext
    {
        public List<Variable> Variables = new();
        public FnInfo Info;
        public int TempCount = 0, LabelCount = 0;
        public TypeInfo RetType = null!;
        public Label NewLabel()
            => new Label(LabelCount++);
    }
    private static FunctionContext CompileFn(ref Context ctx, FnInfo info)
    {
        if (info.RetType != ctx.Void && !CheckAllCodePathReturns(info.FnDef.Body))
        {
            ctx.Errors.Add(new Error("Not all code paths returns", info.FnDef.File, info.FnDef.Pos));
            return new();
        }
        var res = new List<InstructionBase>();
        var fctx = new FunctionContext();
        fctx.Info = info;
        info.Compiled = res;
        foreach(var arg in info.FnDef.Params)
        {
            var type = ctx.GetTypeInfo(arg.Type);
            if(type is null)
            {
                type = ctx.I64;
                ctx.Errors.Add(new Error($"Undefined type {arg.Type}", arg.File, arg.Type.Pos));
            }
            fctx.Variables.Add(new Variable(arg.Name, type, true));
        }
        fctx.RetType = info.RetType;
        CompileStatement(ref fctx, ref ctx, info.FnDef.Body, res);
        return fctx;
    }
    private static void CompileStatement(ref FunctionContext fctx, ref Context ctx, Statement s, List<InstructionBase> instructions)
    {
        switch (s)
        {
            case LetStatement let:
                if (fctx.Variables.FirstOrDefault(x => x.Name == let.Name) is not null)
                {
                    ctx.Errors.Add(new Error($"Variable {let.Name} is already declared", let.File, let.Pos));
                }
                else if (let.Type is not null)
                {
                    var letType = ctx.GetTypeInfo(let.Type);
                    if (letType is null)
                    {
                        ctx.Errors.Add(new Error($"Undefined type {let.Type}", let.Type.File, let.Type.Pos));
                        return;
                    }
                    var valueType = InferExpressionType(let.Value, ref fctx, ref ctx);
                    if (!letType.Equals(valueType))
                    {
                        ctx.Errors.Add(new Error($"Cannot assign value of type {valueType} to variable of type {letType}", let.File, let.Value.Pos));
                        return;
                    }
                    var res = CompileExpression(let.Value, ref fctx, ref ctx, instructions);
                    var varr = new Variable(let.Name, letType);
                    fctx.Variables.Add(varr);
                    instructions.Add(new Instruction(Operation.Equals, res, null, varr));
                }
                else
                {
                    var valueType = InferExpressionType(let.Value, ref fctx, ref ctx);
                    var res = CompileExpression(let.Value, ref fctx, ref ctx, instructions);
                    var varr = new Variable(let.Name, valueType);
                    fctx.Variables.Add(varr);
                    instructions.Add(new Instruction(Operation.Equals, res, null, varr));
                }
                break;
            case AssignStatement ass:
                {
                    if (ass.Expr is VariableExpression varr)
                    {
                        if (fctx.Variables.FirstOrDefault(x => x.Name == varr.Name) is Variable v)
                        {
                            var type = InferExpressionType(ass.Value, ref fctx, ref ctx);
                            if (!type.Equals(v.Type))
                            {
                                ctx.Errors.Add(
                                    new Error(
                                        $"Cannot store value of type {type} to variable of type {v.Type}", varr.File, varr.Pos
                                    )
                                );
                            }
                            var res = CompileExpression(ass.Value, ref fctx, ref ctx, instructions);
                            instructions.Add(new Instruction(Operation.Equals, res, null, v));
                        }
                        else
                        {
                            ctx.Errors.Add(new Error($"Undefined variable {varr.Name}", varr.File, varr.Pos));
                        }
                    }
                    else if (ass.Expr is DereferenceExpression deref)
                    {
                        var valueType = InferExpressionType(ass.Value, ref fctx, ref ctx);
                        if (deref.DerefDepth == 1)
                        {
                            var type = InferExpressionType(deref.Expr, ref fctx, ref ctx);
                            if (!type.Equals(valueType))
                            {
                                ctx.Errors.Add(new Error($"Cannot store value of type {valueType} to {type}", ass.File, ass.Pos));
                            }
                            var res = CompileExpression(ass.Value, ref fctx, ref ctx, instructions);
                            var destexpr = CompileExpression(deref.Expr, ref fctx, ref ctx, instructions) as Variable;
                            instructions.Add(new Instruction(Operation.SetRef, res, null, destexpr));
                        }
                        else if (deref.DerefDepth > 1)
                        {
                            var newderef = new DereferenceExpression(deref.Expr, deref.DerefDepth - 1, deref.Pos);
                            PtrTypeInfo derefType = InferExpressionType(newderef, ref fctx, ref ctx) as PtrTypeInfo;
                            if (!derefType.Equals(valueType))
                            {
                                ctx.Errors.Add(new Error($"Cannot store value of type {valueType} to {derefType}", ass.File, ass.Pos));
                            }
                            var res = CompileExpression(ass.Value, ref fctx, ref ctx, instructions);
                            var dest = CompileExpression(newderef, ref fctx, ref ctx, instructions) as Variable;
                            instructions.Add(new Instruction(Operation.SetRef, res, null, dest));
                        }
                    }
                }
                break;
            case BlockStatement block:
                foreach (var statement in block.Statements)
                    CompileStatement(ref fctx, ref ctx, statement, instructions);
                break;
            case InlineAsmStatement asm:
                {
                    var ls = new List<string>();
                    var body = asm.Body;
                    if (body.Count > 0)
                    {
                        int prevLine = body[0].Pos.Line;
                        var sb = new StringBuilder();
                        foreach (var token in body)
                        {
                            if (prevLine != token.Pos.Line)
                            {
                                ls.Add(sb.ToString());
                                sb.Clear();
                                prevLine = token.Pos.Line;
                            }
                            sb.Append(token.Value).Append(' ');
                        }
                        ls.Add(sb.ToString());
                    }
                    instructions.Add(new InlineAsmInstruction(ls));
                }
                break;
            case CallStatement call:
                CompileExpression(call.Call, ref fctx, ref ctx, instructions);
                break;
            case RetStatement ret:
                {
                    if (fctx.RetType == ctx.Void)
                    {
                        if (ret.Value is not null)
                        {
                            ctx.Errors.Add(new Error("Void functions cannot return value", ret.File, ret.Pos));
                        }
                        else
                        {
                            instructions.Add(new Instruction(Operation.Ret, null, null, null!));
                        }
                    }
                    else
                    {
                        var valueType = InferExpressionType(ret.Value, ref fctx, ref ctx);
                        if (valueType.Equals(fctx.RetType))
                        {
                            var res = CompileExpression(ret.Value, ref fctx, ref ctx, instructions);
                            instructions.Add(new Instruction(Operation.Ret, res, null, null!));
                        }
                        else
                        {
                            ctx.Errors
                                .Add
                                (
                                    new Error(
                                        $"Cannot return value of type {valueType} from function with return type {fctx.RetType}",
                                        ret.File,
                                        ret.Pos
                                    )
                                );
                        }
                    }
                }
                break;
            case IfElseStatement ifElse:
            {
                var elsestart = fctx.NewLabel();
                var end = fctx.NewLabel();
                var condType = InferExpressionType(ifElse.Condition, ref fctx, ref ctx);
                if(condType != ctx.Bool)
                    ctx.Errors.Add(new Error($"Condition must be boolean", ifElse.File, ifElse.Condition.Pos));
                var cond = CompileExpression(ifElse.Condition, ref fctx, ref ctx, instructions);
                var jmp = new Jmp(elsestart, cond, JumpType.JmpFalse);
                instructions.Add(jmp);
                CompileStatement(ref fctx, ref ctx, ifElse.Body, instructions);
                var jmptoend = new Jmp(end, null, JumpType.Jmp);
                instructions.Add(jmptoend);
                instructions.Add(elsestart);
                if(ifElse.Else is Statement @else)
                    CompileStatement(ref fctx, ref ctx, @else, instructions);
                instructions.Add(end);
            }
            break;
            case WhileStatement wh:
            {
                var condition = fctx.NewLabel();
                var jmptocond = new Jmp(condition, null, JumpType.Jmp);
                instructions.Add(jmptocond);
                var loopbody = fctx.NewLabel();
                instructions.Add(loopbody);
                CompileStatement(ref fctx, ref ctx, wh.Body, instructions);
                var condType = InferExpressionType(wh.Cond, ref fctx, ref ctx);
                
                if(condType != ctx.Bool)
                    ctx.Errors.Add(new Error($"Condition must be boolean", wh.File, wh.Cond.Pos));
                instructions.Add(condition);
                var cond = CompileExpression(wh.Cond, ref fctx, ref ctx, instructions);
                var jmpif = new Jmp(loopbody, cond, JumpType.JmpTrue);
                instructions.Add(jmpif);
            }
            break;
        }
    }
    private static Source CompileExpression(Expression expr, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        return expr switch
        {
            BinaryExpression bin => CompileBinary(bin, ref fctx, ref ctx, instructions),
            IntegerExpression i => new Constant<long>(i.Value),
            VariableExpression varr => CompileVar(varr, ref fctx, ref ctx),
            BoolExpression b => new Constant<bool>(b.Value),
            NegateExpression neg => CompileNeg(neg, ref fctx, ref ctx, instructions),
            RefExpression r => CompileRef(r, ref fctx, ref ctx, instructions),
            IndexExpression index => CompileIndex(index, ref fctx, ref ctx, instructions),
            FunctionCallExpression fncall => CompileFnCall(fncall, ref fctx, ref ctx, instructions),
        };
    }
    private static Source CompileFnCall(FunctionCallExpression fncall, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instrs)
    {
        var types = new List<TypeInfo>();
        foreach (var arg in fncall.Args)
        {
            types.Add(InferExpressionType(arg, ref fctx, ref ctx));
        }
        if (ctx.Fns.FirstOrDefault(x => x.Name == fncall.Name && types.SequenceEqual(x.Params)) is FnInfo fn)
        {
            var res = new Variable($"__temp_{fctx.TempCount++}", fn.RetType);
            fctx.Variables.Add(res);
            var args = new List<Source>();
            foreach (var arg in fncall.Args)
                args.Add(CompileExpression(arg, ref fctx, ref ctx, instrs));
            instrs.Add(new FnCallInstruction(fn, args, res));
            return res;
        }
        else
        {
            ctx.Errors.Add(new Error($"Undefined function {fncall.Name}", fncall.File, fncall.Pos));
            return new Constant<long>(0);
        }
    }
    private static Source CompileIndex(IndexExpression index, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instrs)
    {
        if (index.Indexes.Count > 1)
            throw new NotImplementedException();
        var type = InferExpressionType(index.Indexed, ref fctx, ref ctx);
        if (type is PtrTypeInfo ptr)
        {
            TypeInfo destType = null!;
            if (ptr.Depth == 1)
                destType = ptr.Underlaying;
            else
                destType = new PtrTypeInfo(ptr.Underlaying, ptr.Depth - 1);
            var dest = new Variable($"__temp_{fctx.TempCount++}", destType);
            fctx.Variables.Add(dest);
            var expr = CompileExpression(index.Indexes[0], ref fctx, ref ctx, instrs);
            var source = CompileExpression(index.Indexed, ref fctx, ref ctx, instrs);
            instrs.Add(new Instruction(Operation.Index, source, expr, dest));
            return dest;
        }
        else
        {
            ctx.Errors.Add(new Error($"Cannot index value of type {type}", index.File, index.Pos));
            return new Constant<long>(0);
        }
    }
    private static Source CompileRef(RefExpression r, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instrs)
    {
        if (r.Expr is VariableExpression varr)
        {
            var type = InferExpressionType(varr, ref fctx, ref ctx);
            PtrTypeInfo destType = null!;
            if (type is PtrTypeInfo ptr)
            {
                destType = new PtrTypeInfo(ptr.Underlaying, ptr.Depth + 1);
            }
            else
            {
                destType = new PtrTypeInfo(type, 1);
            }
            var v = CompileExpression(varr, ref fctx, ref ctx, instrs);
            var res = new Variable($"__temp_{fctx.TempCount++}", destType);
            fctx.Variables.Add(res);
            instrs.Add(new Instruction(Operation.Ref, v, null, res));
            return res;
        }
        else
        {
            throw new NotImplementedException();
        }

    }
    private static Source CompileNeg(NegateExpression neg, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instrs)
    {
        var type = InferExpressionType(neg.Expr, ref fctx, ref ctx);
        if (type.Equals(ctx.I64))
        {
            var res = CompileExpression(neg.Expr, ref fctx, ref ctx, instrs);
            var dest = new Variable($"__temp_{fctx.TempCount++}", type);
            fctx.Variables.Add(dest);
            var negi = new Instruction(Operation.Neg, res, null, dest);
            instrs.Add(negi);
            return dest;
        }
        else
        {
            ctx.Errors.Add(new Error($"Cannot negate value of type {type}", neg.File, neg.Pos));
            return new Constant<long>(0);
        }
    }
    private static Source CompileVar(VariableExpression varr, ref FunctionContext fctx, ref Context ctx)
    {
        if (fctx.Variables.FirstOrDefault(x => x.Name == varr.Name) is Variable v)
        {
            return v;
        }
        else
        {
            ctx.Errors.Add(new Error($"Undefined variable {varr.Name}", varr.File, varr.Pos));
            return new Constant<long>(0);
        }
    }

    private static Source CompileBinary(BinaryExpression bin, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        var leftType = InferExpressionType(bin.Left, ref fctx, ref ctx);
        var rightType = InferExpressionType(bin.Right, ref fctx, ref ctx);
        if (leftType != rightType)
        {
            ctx.Errors.Add(new Error($"Cannot apply operator '{bin.Op}'", bin.File, bin.Pos));
        }
        Source src1 = CompileExpression(bin.Left, ref fctx, ref ctx, instructions);
        Source src2 = CompileExpression(bin.Right, ref fctx, ref ctx, instructions);
        var type = InferExpressionType(bin, ref fctx, ref ctx);
        var res = new Variable($"__temp_{fctx.TempCount++}", type);
        fctx.Variables.Add(res);
        var op = bin.Op switch
        {
            "+" => Operation.Add,
            "-" => Operation.Sub,
            "*" => Operation.Mul,
            "/" => Operation.Div,
            ">>" => Operation.Shr,
            "<<" => Operation.Shl,
            ">" => Operation.GT,
            ">=" => Operation.GTEQ,
            "<" => Operation.LT,
            "<=" => Operation.LTEQ,
            "==" => Operation.EQEQ,
            "&&" => Operation.AND,
            "||" => Operation.OR,
            _ => throw new(bin.Op)
        };
        instructions.Add(new Instruction(op, src1, src2, res));
        return res;
    }

    private static TypeInfo InferExpressionType(Expression expr, ref FunctionContext fctx, ref Context ctx)
    {
        switch (expr)
        {
            case IntegerExpression:
                return ctx.I64;
            case BoolExpression b:
                return ctx.Bool;
            case NegateExpression neg:
                return InferExpressionType(neg.Expr, ref fctx, ref ctx);
            case DereferenceExpression deref:
                {
                    var underlaying = InferExpressionType(deref.Expr, ref fctx, ref ctx);
                    if (underlaying is PtrTypeInfo ptr)
                    {
                        if (deref.DerefDepth > ptr.Depth)
                        {
                            ctx.Errors.Add(new Error($"Cannot dereference type {ptr.Underlaying}", expr.File, expr.Pos));
                            return ptr.Underlaying;
                        }
                        else if (deref.DerefDepth < ptr.Depth)
                        {
                            return new PtrTypeInfo(ptr.Underlaying, ptr.Depth - deref.DerefDepth);
                        }
                        else
                        {
                            return ptr.Underlaying;
                        }
                    }
                    else
                    {
                        ctx.Errors.Add(new Error($"Cannot derefence type {underlaying}", expr.File, expr.Pos));
                        return underlaying;
                    }
                }
            case VariableExpression v:
                if (fctx.Variables.FirstOrDefault(x => x.Name == v.Name) is Variable varr)
                {
                    return varr.Type;
                }
                else
                {
                    ctx.Errors.Add(new Error($"Undefined variable {v.Name}", v.File, v.Pos));
                    return ctx.I64;
                }
            case RefExpression r:
                return InferExpressionType(r.Expr, ref fctx, ref ctx) switch
                {
                    PtrTypeInfo ptr => new PtrTypeInfo(ptr.Underlaying, ptr.Depth + 1),
                    TypeInfo t => new PtrTypeInfo(t, 1),
                };
            case BinaryExpression bin:
                if (bin.Op is "==" or "!=" or "<=" or "<" or ">" or ">=" or "&&" or "||")
                    return ctx.Bool;
                else
                    return InferExpressionType(bin.Left, ref fctx, ref ctx);
            case IndexExpression index:
                {
                    var type = InferExpressionType(index.Indexed, ref fctx, ref ctx);
                    if (type is PtrTypeInfo ptr)
                    {
                        if (ptr.Depth == 1)
                            return ptr.Underlaying;
                        else
                            return new PtrTypeInfo(ptr.Underlaying, ptr.Depth - 1);
                    }
                    else
                    {
                        ctx.Errors.Add(new Error($"Cannot index type {type}", index.File, index.Pos));
                        return type;
                    }
                }
            case FunctionCallExpression fncall:
                {

                    var types = new List<TypeInfo>();
                    foreach (var arg in fncall.Args)
                    {
                        types.Add(InferExpressionType(arg, ref fctx, ref ctx));
                    }
                    if (ctx.Fns.FirstOrDefault(x => x.Name == fncall.Name && types.SequenceEqual(x.Params)) is FnInfo fn)
                    { 
                        return fn.RetType;
                    }
                    else
                    {
                        ctx.Errors.Add(new Error($"Undefined function {fncall.Name}", fncall.File, fncall.Pos));
                        return ctx.Void;
                    } 
                }
            default: throw new Exception($"OOOOOO NOOOO {expr.GetType()}");
        }
    }
    private static bool CheckAllCodePathReturns(Statement stat)
    {
        return stat switch
        {
            RetStatement => true,
            BlockStatement block => block.Statements.Count > 0 && block.Statements.Any(x => CheckAllCodePathReturns(x)),
            IfElseStatement ifelse => CheckAllCodePathReturns(ifelse.Body) && ifelse.Else is null ? true : CheckAllCodePathReturns(ifelse.Else!),
            _ => false
        };
    }
    private static void AddFunctions(ref Context ctx, List<FnDefinitionStatement> fns)
    {
        var funcs = new List<FnInfo>();
        foreach (var fn in fns)
        {
            var name = fn.Name;
            var parameters = new List<TypeInfo>();
            foreach (var p in fn.Params)
            {
                if (ctx.GetTypeInfo(p.Type) is TypeInfo type)
                {
                    parameters.Add(type);
                }
                else
                {
                    ctx.Errors.Add(new Error($"Undefined type {p.Type}", p.File, p.Pos));
                }
            }
            var retType = ctx.GetTypeInfo(fn.RetType)!;
            if (retType is null)
            {
                ctx.Errors.Add(new Error($"Undefined type {fn.RetType}", fn.File, fn.RetType.Pos));
                retType = ctx.Types["void"];
            }
            funcs.Add(new FnInfo(name, parameters, retType, fn));
        }
        ctx.Fns = funcs;
    }

    private static void AddDefaultTypes(ref Context ctx)
    {
        var types = new[] {
            new TypeInfo("i64", 8),
            new TypeInfo("void", 0),
            new TypeInfo("bool", 1),
        };
        ctx.Types = types.ToDictionary(x => x.Name);
        ctx.Void = ctx.Types["void"];
        ctx.I64 = ctx.Types["i64"];
        ctx.Bool = ctx.Types["bool"];
    }
}
public class FnInfo
{
    public string Name { get; private set; }
    public string NameInAsm { get; private set; }
    public List<TypeInfo> Params { get; private set; }
    public List<InstructionBase>? Compiled { get; set; }
    public TypeInfo RetType { get; private set; }
    public FnDefinitionStatement FnDef { get; private set; }
    public FnInfo(string name, List<TypeInfo> @params, TypeInfo retType, FnDefinitionStatement fndef)
    {
        (Name, Params, RetType, FnDef) = (name, @params, retType, fndef);
        NameInAsm = Name;
    }
    public override string ToString()
        => $"{Name}({string.Join(", ", Params)}): {RetType}";
}
public class TypeInfo
{
    public string Name { get; protected set; }
    public int Size { get; protected set; }
    public TypeInfo(string name, int size)
        => (Name, Size) = (name, size);
    protected TypeInfo()
        => Name = null!;
    public override string ToString()
        => Name;
    public override bool Equals(object? obj)
        => obj is TypeInfo type and not PtrTypeInfo ? type.Name == Name && type.Size == Size : false;

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Size);
    }
}
public class PtrTypeInfo : TypeInfo
{
    public TypeInfo Underlaying { get; private set; }
    public int Depth { get; private set; }
    public PtrTypeInfo(TypeInfo underlaying, int depth)
        => (Underlaying, Size, Depth, Name) = (underlaying, 8, depth, $"{new string('*', depth)}{underlaying.Name}");

    public override bool Equals(object? obj)
    {
        return obj is PtrTypeInfo info &&
               EqualityComparer<TypeInfo>.Default.Equals(Underlaying, info.Underlaying) &&
               Depth == info.Depth;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Name, Size, Underlaying, Depth);
    }
}



