using System.Text;
using YLang.IR;
using YLang.AST;

namespace YLang;

public static class Compiler
{
    private ref struct Context
    {
        public Dictionary<string, TypeInfo> Types = null!;
        public List<FnInfo> Fns = null!;
        public List<Error> Errors = new();
        public List<Variable> Globals = new();
        public TypeInfo? GetTypeInfo(TypeExpression typeexpr)
        {
            if (typeexpr is PtrType ptr)
            {
                if (Types.TryGetValue(ptr.UnderlayingType.Name, out var type))
                {
                    return new PtrTypeInfo(type);
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
        public TypeInfo I64 = null!, I32 = null!, I16 = null!, I8 = null!;
        public TypeInfo U64 = null!, U32 = null!, U16 = null!, U8 = null!;
        public TypeInfo Bool = null!;
    }
    public static List<Error> Compile(List<Statement> statements, string output)
    {
        var ctx = new Context();
        AddDefaultTypes(ref ctx);
        var fns = statements.OfType<FnDefinitionStatement>().ToList();
        var globals = statements.OfType<LetStatement>().ToList();
        var globalctx = new FunctionContext();
        var instrs = new List<InstructionBase>();
        foreach(var global in globals)
        {
            Console.WriteLine(global.Name);
            if(IsConstant(global.Value))
            {
                CompileStatement(ref globalctx, ref ctx, global, instrs);
            }
            else 
            {
                ctx.Errors.Add(new Error("Global variables cannot be initialized only with constant values", global.Value.File, global.Value.Pos));
            }
        }
        globalctx.Variables.ForEach(x => x.IsGlobal = true);
        ctx.Globals = globalctx.Variables;


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
        var globalsinit = IRCompiler.Compile(instrs, new(), null!);
        var result = new StringBuilder();
        result
            .AppendLine("format PE64 CONSOLE")
            .AppendLine("entry __start")
            .AppendLine("include 'win64axp.inc'")
            .AppendLine("True = 1")
            .AppendLine("False = 0")
            .AppendLine("section '.code' code readable executable")
            .AppendLine("__start:")
            .AppendLine(string.Join('\n', globalsinit))
            .AppendLine("call main")
            .AppendLine("invoke ExitProcess, 0")
            .AppendLine(string.Join('\n', res));
        if(ctx.Globals.Count > 0)
        {
            result.AppendLine("section '.data' data readable writable");
            foreach(var global in ctx.Globals)
                result.AppendLine($"{global.Name} dq 0");
        }
        result
            .AppendLine("section '.idata' import data readable writeable")
            .AppendLine("library kernel32,'kernel32.dll', user32, 'user32.dll'")
            .AppendLine("include 'api\\kernel32.inc'")
            .AppendLine("include 'api\\user32.inc'");
        File.WriteAllText(output, result.ToString());
        return ctx.Errors;
    }

    private static bool IsConstant(Expression value)
    {
        return value is IntegerExpression or NullExpression or BoolExpression;
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
        foreach (var arg in info.FnDef.Params)
        {
            var type = ctx.GetTypeInfo(arg.Type);
            if (type is null)
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
                    var valueType = InferExpressionType(let.Value, ref fctx, ref ctx, letType);
                    if (!letType.Equals(valueType))
                    {
                        ctx.Errors.Add(new Error($"Cannot assign value of type {valueType} to variable of type {letType}", let.File, let.Value.Pos));
                        return;
                    }
                    var res = CompileExpression(let.Value, ref fctx, ref ctx, instructions, valueType);
                    var varr = new Variable(let.Name, letType);
                    fctx.Variables.Add(varr);
                    instructions.Add(new Instruction(Operation.Equals, res, null, varr));
                }
                else
                {
                    var valueType = InferExpressionType(let.Value, ref fctx, ref ctx, null);
                    var res = CompileExpression(let.Value, ref fctx, ref ctx, instructions, valueType);
                    var varr = new Variable(let.Name, valueType);
                    fctx.Variables.Add(varr);
                    instructions.Add(new Instruction(Operation.Equals, res, null, varr));
                }
                break;
            case AssignStatement ass:
                {
                    if (ass.Expr is VariableExpression varr)
                    {
                        Variable? v = fctx.Variables.FirstOrDefault(x => x.Name == varr.Name) ?? ctx.Globals.FirstOrDefault(x =>x.Name == varr.Name);
                        if(v is not null)
                        {
                            var type = InferExpressionType(ass.Value, ref fctx, ref ctx, v.Type);
                            if (!type.Equals(v.Type))
                            {
                                ctx.Errors.Add(
                                    new Error(
                                        $"Cannot store value of type {type} to variable of type {v.Type}", varr.File, varr.Pos
                                    )
                                );
                            }
                            var res = CompileExpression(ass.Value, ref fctx, ref ctx, instructions, v.Type);
                            instructions.Add(new Instruction(Operation.Equals, res, null, v));
                        }
                        else
                        {
                            ctx.Errors.Add(new Error($"Undefined variable {varr.Name}", varr.File, varr.Pos));
                        }
                    }
                    else if (ass.Expr is DereferenceExpression deref)
                    {
                        var type = InferExpressionType(deref, ref fctx, ref ctx, null);
                        var valueType = InferExpressionType(ass.Value, ref fctx, ref ctx, type);
                        if (!type.Equals(valueType))
                        {
                            ctx.Errors.Add(new Error($"Cannot store value of type {valueType} to {type}", ass.File, ass.Pos));
                        }
                        var res = CompileExpression(ass.Value, ref fctx, ref ctx, instructions, valueType);
                        var destexpr = CompileExpression(deref.Expr, ref fctx, ref ctx, instructions, null) as Variable;
                        instructions.Add(new Instruction(Operation.SetRef, res, null, destexpr));
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
                CompileExpression(call.Call, ref fctx, ref ctx, instructions, null);
                var fnca = instructions.LastOrDefault() as FnCallInstruction;
                if(fnca is not null && fnca.Dest is not null)
                {   
                    fctx.Variables.Remove(fnca.Dest);
                    fnca.Dest = null;
                }
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
                        var valueType = InferExpressionType(ret.Value, ref fctx, ref ctx, fctx.RetType);
                        if (valueType.Equals(fctx.RetType))
                        {
                            var res = CompileExpression(ret.Value, ref fctx, ref ctx, instructions, fctx.RetType);
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
                    var ifstart = fctx.NewLabel();
                    var condType = InferExpressionType(ifElse.Condition, ref fctx, ref ctx, ctx.Bool);
                    if (condType != ctx.Bool)
                        ctx.Errors.Add(new Error($"Condition must be boolean", ifElse.File, ifElse.Condition.Pos));
                    if(ifElse.Condition is BinaryExpression bin && bin.Op is "&&" or "||")
                    {
                        CompileLazyBoolean(bin, ifstart, elsestart, ref fctx, ref ctx, instructions); 
                    }
                    else 
                    {
                        var cond = CompileExpression(ifElse.Condition, ref fctx, ref ctx, instructions, ctx.Bool);
                        var jmp = new Jmp(elsestart, cond, JumpType.JmpFalse);
                        instructions.Add(jmp);
                    }
                    instructions.Add(ifstart);
                    CompileStatement(ref fctx, ref ctx, ifElse.Body, instructions);
                    var jmptoend = new Jmp(end, null, JumpType.Jmp);
                    instructions.Add(jmptoend);
                    instructions.Add(elsestart);
                    if (ifElse.Else is Statement @else)
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
                    var condType = InferExpressionType(wh.Cond, ref fctx, ref ctx, ctx.Bool);
                    if (condType != ctx.Bool)
                        ctx.Errors.Add(new Error($"Condition must be boolean", wh.File, wh.Cond.Pos));
                    instructions.Add(condition);
                    var cond = CompileExpression(wh.Cond, ref fctx, ref ctx, instructions, ctx.Bool);
                    var jmpif = new Jmp(loopbody, cond, JumpType.JmpTrue);
                    instructions.Add(jmpif);
                }
                break;
        }
    }

    private static void CompileLazyBoolean(BinaryExpression bin, Label ifstart, Label elsestart, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        if(bin.Op is "&&")
        {
            if(bin.Left is BinaryExpression bleft && bleft.Op is "&&" or "||")
            {
                var snd = fctx.NewLabel();
                CompileLazyBoolean(bleft, snd, elsestart, ref fctx, ref ctx, instructions);
                instructions.Add(snd);
            }
            else 
            {
                var first = CompileExpression(bin.Left, ref fctx, ref ctx, instructions, ctx.Bool);
                instructions.Add(new Jmp(elsestart, first, JumpType.JmpFalse));
            }
            if(bin.Right is BinaryExpression bright && bright.Op is "&&" or "||")
            {
                var snd = fctx.NewLabel();
                CompileLazyBoolean(bright, snd, elsestart, ref fctx, ref ctx, instructions);
                instructions.Add(snd);
            }
            else 
            {
                var second = CompileExpression(bin.Right, ref fctx, ref ctx, instructions, ctx.Bool);
                instructions.Add(new Jmp(elsestart, second, JumpType.JmpFalse));
            }
            instructions.Add(new Jmp(ifstart, null, JumpType.Jmp));
        }
        else 
        {
            if(bin.Left is BinaryExpression bleft && bleft.Op is "&&" or "||")
            {
                CompileLazyBoolean(bleft, ifstart, elsestart, ref fctx, ref ctx, instructions);
            }
            else 
            {
                var first = CompileExpression(bin.Left, ref fctx, ref ctx, instructions, ctx.Bool);
                instructions.Add(new Jmp(ifstart, first, JumpType.JmpTrue));
            }
            if(bin.Right is BinaryExpression bright && bright.Op is "&&" or "||")
            {
                CompileLazyBoolean(bright, ifstart, elsestart, ref fctx, ref ctx, instructions);
            }
            else 
            {
                var second = CompileExpression(bin.Right, ref fctx, ref ctx, instructions, ctx.Bool);
                instructions.Add(new Jmp(ifstart, second, JumpType.JmpTrue));
            }
            instructions.Add(new Jmp(elsestart, null, JumpType.Jmp));
        }
    }

    private static Source CompileExpression(Expression expr, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions, TypeInfo? targetType)
    {
        return expr switch
        {
            BinaryExpression bin => CompileBinary(bin, ref fctx, ref ctx, instructions, targetType),
            IntegerExpression i => new Constant<long>(i.Value, ctx.I64),
            VariableExpression varr => CompileVar(varr, ref fctx, ref ctx, targetType),
            BoolExpression b => new Constant<bool>(b.Value, ctx.Bool),
            NegateExpression neg => CompileNeg(neg, ref fctx, ref ctx, instructions, targetType),
            RefExpression r => CompileRef(r, ref fctx, ref ctx, instructions),
            IndexExpression index => CompileIndex(index, ref fctx, ref ctx, instructions),
            FunctionCallExpression fncall => CompileFnCall(fncall, ref fctx, ref ctx, instructions),
            DereferenceExpression deref => CompileDeref(deref, ref fctx, ref ctx, instructions),
            NullExpression => new Constant<long>(0, ctx.I64),
        };
    }


    private static Source CompileDeref(DereferenceExpression deref, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        var type = InferExpressionType(deref, ref fctx, ref ctx, null);
        var dest = new Variable($"__temp_{fctx.TempCount++}", type);
        fctx.Variables.Add(dest);
        var res = CompileExpression(deref.Expr, ref fctx, ref ctx, instructions, type);
        instructions.Add(new Instruction(Operation.Deref, res, null, dest));
        return dest;
    }

    private static Source CompileFnCall(FunctionCallExpression fncall, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instrs)
    {
        var types = new List<TypeInfo>();
        foreach (var arg in fncall.Args)
        {
            types.Add(InferExpressionType(arg, ref fctx, ref ctx, null));
        }
        var fn = ctx.Fns.FirstOrDefault(x => x.Name == fncall.Name && types.SequenceEqual(x.Params));
        var prev = ctx.Errors.Count;
        if(fn is null)
        {
            var posibles = ctx.Fns.Where(x => x.Name == fncall.Name && x.Params.Count == fncall.Args.Count).ToList();
            if(posibles.Count != 0)
            {
                foreach(var posible in posibles)
                {
                    types.Clear();
                    foreach(var (arg, type) in fncall.Args.Zip(posible.Params))
                    {
                        types.Add(InferExpressionType(arg, ref fctx, ref ctx, type));
                    }
                    if(types.SequenceEqual(posible.Params))
                    {
                        fn = posible;
                        break;
                    }
                }
            }
        }
        if (fn is not null)
        {
            if(prev != ctx.Errors.Count)
            {
                var x = ctx.Errors.Count - prev;
                while(x > 0)
                {
                    ctx.Errors.RemoveAt(ctx.Errors.Count - 1);
                    x--;
                }
            }
            var res = new Variable($"__temp_{fctx.TempCount++}", fn.RetType);
            fctx.Variables.Add(res);
            var args = new List<Source>();
            foreach (var (arg, type) in fncall.Args.Zip(fn.Params))
                args.Add(CompileExpression(arg, ref fctx, ref ctx, instrs, type));
            instrs.Add(new FnCallInstruction(fn, args, res));
            return res;
        }
        else
        {
            ctx.Errors.Add(new Error($"Undefined function {fncall.Name}", fncall.File, fncall.Pos));
            return new Constant<long>(0, ctx.I64);
        }
    }
    private static Source CompileIndex(IndexExpression index, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instrs)
    {
        if (index.Indexes.Count > 1)
            throw new NotImplementedException();
        var type = InferExpressionType(index.Indexed, ref fctx, ref ctx, null);
        if (type is PtrTypeInfo ptr)
        {
            TypeInfo destType = ptr.Underlaying;
            var dest = new Variable($"__temp_{fctx.TempCount++}", destType);
            fctx.Variables.Add(dest);
            var expr = CompileExpression(index.Indexes[0], ref fctx, ref ctx, instrs, ctx.U64);
            var source = CompileExpression(index.Indexed, ref fctx, ref ctx, instrs, ptr);
            instrs.Add(new Instruction(Operation.Index, source, expr, dest));
            return dest;
        }
        else
        {
            ctx.Errors.Add(new Error($"Cannot index value of type {type}", index.File, index.Pos));
            return new Constant<long>(0, ctx.I64);
        }
    }
    private static Source CompileRef(RefExpression r, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instrs)
    {
        if (r.Expr is VariableExpression varr)
        {
            var type = InferExpressionType(varr, ref fctx, ref ctx, null);
            PtrTypeInfo destType = new PtrTypeInfo(type);
            var v = CompileExpression(varr, ref fctx, ref ctx, instrs, type);
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
    private static Source CompileNeg(NegateExpression neg, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instrs, TypeInfo? target)
    {
        var type = InferExpressionType(neg.Expr, ref fctx, ref ctx, target);
        if (type.Equals(ctx.I64) || type.Equals(ctx.I32) || type.Equals(ctx.I16) || type.Equals(ctx.I8))
        {
            var res = CompileExpression(neg.Expr, ref fctx, ref ctx, instrs, type);
            var dest = new Variable($"__temp_{fctx.TempCount++}", type);
            fctx.Variables.Add(dest);
            var negi = new Instruction(Operation.Neg, res, null, dest);
            instrs.Add(negi);
            return dest;
        }
        else
        {
            ctx.Errors.Add(new Error($"Cannot negate value of type {type}", neg.File, neg.Pos));
            return new Variable("none", target ?? type);
        }
    }
    private static Source CompileVar(VariableExpression varr, ref FunctionContext fctx, ref Context ctx, TypeInfo? target)
    {
        if (fctx.Variables.FirstOrDefault(x => x.Name == varr.Name) is Variable v)
        {
            return v;
        }
        else if(ctx.Globals.FirstOrDefault(x => x.Name == varr.Name) is Variable gvar)
        {
            return gvar;
        }
        else
        {
            ctx.Errors.Add(new Error($"Undefined variable {varr.Name}", varr.File, varr.Pos));
            return new Constant<long>(0, target ?? ctx.I64);
        }
    }

    private static Source CompileBinary(BinaryExpression bin, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions, TypeInfo? target)
    {
        var exprtarget = ctx.Bool.Equals(target) ? null : target;
        var leftType = InferExpressionType(bin.Left, ref fctx, ref ctx, exprtarget);
        var rightType = InferExpressionType(bin.Right, ref fctx, ref ctx, leftType);
        if (!((leftType is PtrTypeInfo || rightType is PtrTypeInfo) && !(leftType is PtrTypeInfo && rightType is PtrTypeInfo)) && 
                leftType != rightType)
        {
            ctx.Errors.Add(new Error($"Cannot apply operator '{bin.Op}'", bin.File, bin.Pos));
        }
        Source src1 = CompileExpression(bin.Left, ref fctx, ref ctx, instructions, target);
        Source src2 = CompileExpression(bin.Right, ref fctx, ref ctx, instructions, target);
        var type = InferExpressionType(bin, ref fctx, ref ctx, target);
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
            "!=" => Operation.NEQ,
            "&&" => Operation.AND,
            "||" => Operation.OR,
            _ => throw new(bin.Op)
        };
        instructions.Add(new Instruction(op, src1, src2, res));
        return res;
    }

    private static TypeInfo InferInt(IntegerExpression i, ref FunctionContext fctx, ref Context ctx, TypeInfo? target)
    {
        if (target is null)
            return ctx.I32;
        if (target.Equals(ctx.I64))
        {
            return ctx.I64;
        }
        else if (target.Equals(ctx.I32))
        {
            if (i.Value > int.MaxValue)
            {
                ctx.Errors.Add(new Error("Value out of range", i.File, i.Pos));
            }
            else return ctx.I32;
        }
        else if (target.Equals(ctx.U32))
        {
            if (i.Value > uint.MaxValue)
            {
                ctx.Errors.Add(new Error("Value out of range", i.File, i.Pos));
            }
            else return ctx.U32;
        }
        else if (target.Equals(ctx.I16))
        {
            if (i.Value > short.MaxValue)
            {
                ctx.Errors.Add(new Error("Value out of range", i.File, i.Pos));
            }
            else return ctx.I16;
        }
        else if (target.Equals(ctx.U16))
        {
            if (i.Value > ushort.MaxValue)
            {
                ctx.Errors.Add(new Error("Value out of range", i.File, i.Pos));
            }
            else return ctx.U16;
        }
        else if (target.Equals(ctx.I8))
        {
            if (i.Value > sbyte.MaxValue)
            {
                ctx.Errors.Add(new Error("Value out of range", i.File, i.Pos));
            }
            else return ctx.I8;
        }
        else if (target.Equals(ctx.U8))
        {
            if (i.Value > byte.MaxValue)
            {
                ctx.Errors.Add(new Error("Value out of range", i.File, i.Pos));
            }
            else return ctx.U8;
        }
        return ctx.I32;
    }
    private static TypeInfo InferExpressionType(Expression expr, ref FunctionContext fctx, ref Context ctx, TypeInfo? target)
    {
        switch (expr)
        {
            case IntegerExpression i:
                return InferInt(i, ref fctx, ref ctx, target);
            case BoolExpression b:
                return ctx.Bool;
            case NegateExpression neg:
                return InferExpressionType(neg.Expr, ref fctx, ref ctx, target);
            case NullExpression nullexpr:
                return InferNull(ref ctx, target);
            case DereferenceExpression deref:
                {
                    var underlaying = InferExpressionType(deref.Expr, ref fctx, ref ctx, target);
                    if (underlaying is PtrTypeInfo ptr)
                    {
                        return ptr.Underlaying;
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
                else if(ctx.Globals.FirstOrDefault(x => x.Name == v.Name) is Variable gvar)
                {
                    return gvar.Type;
                }
                else
                {
                    ctx.Errors.Add(new Error($"Undefined variable {v.Name}", v.File, v.Pos));
                    return ctx.I64;
                }
            case RefExpression r:
                return InferExpressionType(r.Expr, ref fctx, ref ctx, null) switch
                {
                    PtrTypeInfo ptr => new PtrTypeInfo(ptr),
                    TypeInfo t => new PtrTypeInfo(t),
                };
            case BinaryExpression bin:
                if (bin.Op is "==" or "!=" or "<=" or "<" or ">" or ">=" or "&&" or "||")
                    return ctx.Bool;
                else
                    return InferExpressionType(bin.Left, ref fctx, ref ctx, target);
            case IndexExpression index:
                {
                    var type = InferExpressionType(index.Indexed, ref fctx, ref ctx, null);
                    if (type is PtrTypeInfo ptr)
                    {
                        return ptr.Underlaying;
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
                        types.Add(InferExpressionType(arg, ref fctx, ref ctx, null));
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

    private static TypeInfo InferNull(ref Context ctx, TypeInfo? target)
    {
        if (target is PtrTypeInfo ptr)
            return ptr;
        return new PtrTypeInfo(ctx.Void);
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
            new TypeInfo("i32", 4),
            new TypeInfo("i16", 2),
            new TypeInfo("i8", 1),
            new TypeInfo("u64", 8),
            new TypeInfo("u32", 4),
            new TypeInfo("u16", 2),
            new TypeInfo("u8", 1),
            new TypeInfo("void", 0),
            new TypeInfo("bool", 1),
        };
        ctx.Types = types.ToDictionary(x => x.Name);
        ctx.Void = ctx.Types["void"];
        ctx.I64 = ctx.Types["i64"];
        ctx.I32 = ctx.Types["i32"];
        ctx.I16 = ctx.Types["i16"];
        ctx.I8 = ctx.Types["i8"];
        ctx.U64 = ctx.Types["u64"];
        ctx.U32 = ctx.Types["u32"];
        ctx.U16 = ctx.Types["u16"];
        ctx.U8 = ctx.Types["u8"];
        ctx.Bool = ctx.Types["bool"];
    }
}



