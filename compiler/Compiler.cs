using System.Text;
using YLang.IR;
using YLang.AST;

namespace YLang;

public static class Compiler
{
    private ref struct Context
    {
        public Dictionary<string, TypeInfo> Types = null!;
        public List<FnInfo> Fns = new();
        public List<Error> Errors = new();
        public List<Variable> Globals = new();
        public Dictionary<string, Constant<string>> StringConstants = new();
        public Context() { }
        public TypeInfo? GetTypeInfo(TypeExpression typeexpr)
        {
            if (typeexpr is PtrType ptr)
            {
                if (Types.TryGetValue(ptr.UnderlayingType.Name, out var type))
                {
                    var typeinfo = type;
                    for (int depth = ptr.PtrDepth; depth > 0; depth--)
                        typeinfo = new PtrTypeInfo(typeinfo);
                    return typeinfo;
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
        public TypeInfo Char = null!;
        public TypeInfo Bool = null!;
    }
    public static List<Error> Compile(List<Statement> statements, string output, CompilerSettings settings)
    {
        if (settings.DumpIR is not null && !settings.DumpIR.Exists)
        {
            settings.DumpIR.Create();
        }
        var ctx = new Context();
        AddDefaultTypes(ref ctx);
        var fns = statements.OfType<FnDefinitionStatement>().ToList();
        var globals = statements.OfType<LetStatement>().ToList();
        var structs = statements.OfType<StructDefinitionStatement>().ToList();
        AddStructs(structs, ref ctx);
        var globalctx = new FunctionContext();
        var instrs = new List<InstructionBase>();
        foreach (var global in globals)
        {
            if (IsConstant(global.Value))
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
        var res = new List<string>();
        List<(List<Variable>, FnInfo)> compiledfns = new();
        var main = ctx.Fns.FirstOrDefault(x => x.Name == "main" && x.Params.Count == 0);
        if (main is null)
        {
            ctx.Errors.Add(new Error("No entry point", ctx.Fns.FirstOrDefault()?.Body?.File ?? "<source>", new Position()));
            return ctx.Errors;
        }
        main.WasUsed = true;
        bool smthWasCompiled = true;
        while (smthWasCompiled)
        {
            smthWasCompiled = false;
            foreach (var fn in ctx.Fns.Where(x => x.WasUsed && x.Compiled == null))
            {
                smthWasCompiled = true;
                Console.WriteLine($"Compiling {fn}");
                var f = CompileFn(ref ctx, fn);
                Console.WriteLine(string.Join('\n', f.Info.Compiled));
                compiledfns.Add((f.Variables, f.Info));
                if (settings.DumpIR is not null)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Variables: ");
                    foreach (var variable in f.Variables)
                        sb.AppendLine(variable.ToString());
                    sb.AppendLine("-------- BODY ---------");
                    foreach (var instr in f.Info.Compiled)
                        sb.AppendLine(instr.ToString());
                    File.WriteAllText(Path.Combine(settings.DumpIR.FullName, $"{f.Info.NameInAsm}.ir"), sb.ToString());
                }
            }
        }
        foreach (var (vars, fn) in compiledfns)
        {
            if (!fn.WasUsed && fn.Name != "main")
                continue;
            var fctx = new FunctionContext();
            fctx.Variables = vars;
            fctx.Info = fn;
            if (settings.Optimize && fn.Compiled is not null)
                Optimize(fn.Compiled);
            res.AddRange(IRCompiler.Compile(fn.Compiled, vars, fn));
        }
        var globalsinit = IRCompiler.Compile(instrs, new(), null!);
        var result = new StringBuilder();
        if (settings.Target is Target.Windows)
        {
            result
                .AppendLine("format PE64 CONSOLE")
                .AppendLine("entry __start")
                .AppendLine("include 'win64axp.inc'")
                .AppendLine("True = 1")
                .AppendLine("False = 0")
                .AppendLine("section '.code' code readable executable")
                .AppendLine("__start:")
                .AppendLine("push rbp")
                .AppendLine("mov rbp, rsp")
                .AppendLine(string.Join('\n', globalsinit))
                .AppendLine("call main")
                .AppendLine("mov rsp, rbp")
                .AppendLine("pop rbp")
                .AppendLine("invoke ExitProcess, 0")
                .AppendLine(string.Join('\n', res));
            if (ctx.Globals.Count > 0 || ctx.StringConstants.Count > 0)
            {
                result.AppendLine("section '.data' data readable writable");
                foreach (var global in ctx.Globals)
                    result.AppendLine($"{global.Name} dq {(global.Type.Size > 8 ? string.Join(", ", Enumerable.Repeat("0", global.Type.Size / 8)) : "0")}");
                foreach (var (s, str) in ctx.StringConstants)
                    result.AppendLine($"{str.Value} db { string.Join(',', s.Select(x => (byte)x).Append((byte)0)) }");
            }
            result
                .AppendLine("section '.idata' import data readable writeable")
                .AppendLine("library kernel32,'kernel32.dll', user32, 'user32.dll'")
                .AppendLine("include 'api\\kernel32.inc'")
                .AppendLine("include 'api\\user32.inc'");
        }
        else
        {
            result
                .AppendLine("format ELF64 executable")
                .AppendLine("entry __start")
                .AppendLine("True = 1")
                .AppendLine("False = 0")
                .AppendLine("segment readable executable")
                .AppendLine("__start:")
                .AppendLine(string.Join('\n', globalsinit))
                .AppendLine("call main")
                .AppendLine("mov eax, 60")
                .AppendLine("xor rdi, rdi")
                .AppendLine("syscall")
                .AppendLine(string.Join('\n', res));
            if (ctx.Globals.Count > 0 || ctx.StringConstants.Count > 0)
            {
                result.AppendLine("segment readable writable");
                foreach (var global in ctx.Globals)
                    result.AppendLine($"{global.Name} dq {(global.Type.Size > 8 ? string.Join(", ", Enumerable.Repeat("0", global.Type.Size / 8)) : "0")}");
                foreach (var (s, str) in ctx.StringConstants)
                    result.AppendLine($"{str.Value} db { string.Join(',', s.Select(x => (byte)x).Append((byte)0)) }");
            }
        }
        File.WriteAllText(output, result.ToString());
        return ctx.Errors;
    }
    private static void Optimize(List<InstructionBase> instrs, int optimization = 8)
    {
        for (int opt = 0; opt < optimization; opt++)
            for (int i = 0; i < instrs.Count; i++)
            {
                if (instrs[i] is Instruction instr)
                {
                    if (instr.Op is Operation.Add or Operation.Sub && instr.Second is Constant<long> val && val.Value == 0)
                    {
                        instrs[i] = new Instruction(Operation.Equals, instr.First, null, instr.Destination);
                        i--;
                    }
                    else if (instr.Op is Operation.Add or Operation.Sub && instr.First is Variable f && f.Name == instr.Destination.Name && instr.Second is Constant<long> cnst && cnst.Value == 1)
                    {
                        instrs[i] = new Instruction(instr.Op is Operation.Add ? Operation.Inc : Operation.Dec, null, null, instr.Destination);
                        i--;
                    }
                    else if (i + 1 < instrs.Count && instrs[i + 1] is Instruction other && other.Op is Operation.Equals && other.First is Variable fst && fst.Name == instr.Destination.Name)
                    {
                        instrs[i] = new Instruction(instr.Op, instr.First, instr.Second, other.Destination);
                        instrs.RemoveAt(i + 1);
                        i--;
                    }
                    else if (instr.Op is Operation.Index && instr.Second is Constant<long> v && v.Value == 0)
                    {
                        instrs[i] = new Instruction(Operation.Deref, instr.First, null, instr.Destination);
                    }
                }

            }
    }
    private static void AddStructs(List<StructDefinitionStatement> structs, ref Context ctx)
    {
        foreach (var structt in structs)
        {
            if (ctx.Types.ContainsKey(structt.Name))
            {
                ctx.Errors.Add(new Error($"Type {structt.Name} already defined", structt.File, structt.Pos));
                continue;
            }
            var fields = new Dictionary<string, FieldInfo>();
            int offset = 0;
            foreach (var field in structt.Fields)
            {
                var type = ctx.GetTypeInfo(field.Type);
                if (type is null)
                {
                    ctx.Errors.Add(new Error($"Unknown type {field.Type}", field.File, field.Type.Pos));
                }
                else
                {
                    fields.Add(field.Name, new FieldInfo(offset, type));
                    offset += type.Size;
                }
            }
            var ctors = new List<FnInfo>();
            var struc = new CustomTypeInfo(structt.Name, fields, ctors);
            ctx.Types.Add(structt.Name, struc);
            foreach (var ctor in structt.Constructors)
            {
                var @params = new List<(string name, TypeInfo type)>();
                @params.Add(("this", new PtrTypeInfo(struc)));
                foreach (var param in ctor.Params)
                {
                    if (ctx.GetTypeInfo(param.Type) is TypeInfo type)
                    {
                        @params.Add((param.Name, type));
                    }
                    else
                    {
                        ctx.Errors.Add(new Error($"Unknown type {param.Type}", param.File, param.Type.Pos));
                    }
                }
                var name = $"{structt.Name}._ctor";
                var fn = new FnInfo(name, @params, ctx.Void, ctor.Body);
                ctx.Fns.Add(fn);
                ctors.Add(fn);
            }
        }
    }
    private static bool IsConstant(Expression value)
    {
        return value is IntegerExpression or NullExpression or BoolExpression or CharExpression or StringExpression
            || (value is NewObjExpression newobj && newobj.Args.All(IsConstant));
    }

    private ref struct FunctionContext
    {
        public List<Variable> Variables = new();
        public FnInfo Info = null!;
        public int TempCount = 0, LabelCount = 0;
        public TypeInfo RetType = null!;
        public Label NewLabel()
            => new Label(LabelCount++);
        public FunctionContext() { }
        public Variable NewVar(string name, TypeInfo type)
        {
            var v = new Variable(name, type);
            Variables.Add(v);
            return v;
        }
        public Variable NewTemp(TypeInfo type) => NewVar($"t{TempCount++}", type);
    }
    private static FunctionContext CompileFn(ref Context ctx, FnInfo info)
    {
        if (info.RetType != ctx.Void && !CheckAllCodePathReturns(info.Body))
        {
            ctx.Errors.Add(new Error("Not all code paths returns", info.Body.File, info.Body.Pos));
            return new();
        }
        var res = new List<InstructionBase>();
        var fctx = new FunctionContext();
        fctx.Info = info;
        info.Compiled = res;
        foreach (var (arg, type) in info.Params)
        {
            fctx.Variables.Add(new Variable(arg, type, true));
        }
        fctx.RetType = info.RetType;
        CompileStatement(ref fctx, ref ctx, info.Body, res);
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
                    var varr = fctx.NewVar(let.Name, letType);
                    instructions.Add(new Instruction(Operation.Equals, res, null, varr));
                }
                else
                {
                    var valueType = InferExpressionType(let.Value, ref fctx, ref ctx, null);
                    var res = CompileExpression(let.Value, ref fctx, ref ctx, instructions, valueType);
                    var varr = fctx.NewVar(let.Name, valueType);
                    instructions.Add(new Instruction(Operation.Equals, res, null, varr));
                }
                break;
            case AssignStatement ass:
                {
                    if (ass.Expr is VariableExpression varr)
                    {
                        Variable? v = fctx.Variables.FirstOrDefault(x => x.Name == varr.Name) ?? ctx.Globals.FirstOrDefault(x => x.Name == varr.Name);
                        if (v is not null)
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
                    else if (ass.Expr is IndexExpression index)
                    {
                        var type = InferExpressionType(index, ref fctx, ref ctx, null);
                        var valueType = InferExpressionType(ass.Value, ref fctx, ref ctx, type);
                        if (!type.Equals(valueType))
                        {

                            ctx.Errors.Add(
                                new Error(
                                    $"Cannot store value of type {valueType} to variable of type {type}", ass.Value.File, ass.Value.Pos
                                )
                            );
                        }

                        var refr = CompileRefOf(index, ref fctx, ref ctx, instructions);
                        var value = CompileExpression(ass.Value, ref fctx, ref ctx, instructions, valueType);
                        instructions.Add(new Instruction(Operation.SetRef, value, null, refr));
                    }
                    else if (ass.Expr is MemberAccessExpression member)
                    {
                        var type = InferExpressionType(member, ref fctx, ref ctx, null);
                        var valueType = InferExpressionType(ass.Value, ref fctx, ref ctx, type);
                        if(!type.Equals(valueType))
                        {
                            ctx.Errors.Add(
                                new Error(
                                    $"Cannot store value of type {valueType} to variable of type {type}", ass.Value.File, ass.Value.Pos
                                )
                            );
                        }
                        var refr = CompileRefOf(member, ref fctx, ref ctx, instructions);
                        var value = CompileExpression(ass.Value, ref fctx, ref ctx, instructions, valueType);
                        instructions.Add(new Instruction(Operation.SetRef, value, null, refr));
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
                            sb.Append(token.Type is TokenType.Char ? $"'{token.Value}'" : token.Value).Append(' ');
                        }
                        ls.Add(sb.ToString());
                    }
                    instructions.Add(new InlineAsmInstruction(ls));
                }
                break;
            case CallStatement call:
                CompileExpression(call.Call, ref fctx, ref ctx, instructions, null);
                var fnca = instructions.LastOrDefault() as FnCallInstruction;
                if (fnca is not null && fnca.Dest is not null)
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
                    if (ifElse.Condition is BinaryExpression bin && bin.Op is "&&" or "||")
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
        if (bin.Op is "&&")
        {
            if (bin.Left is BinaryExpression bleft && bleft.Op is "&&" or "||")
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
            if (bin.Right is BinaryExpression bright && bright.Op is "&&" or "||")
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
            if (bin.Left is BinaryExpression bleft && bleft.Op is "&&" or "||")
            {
                CompileLazyBoolean(bleft, ifstart, elsestart, ref fctx, ref ctx, instructions);
            }
            else
            {
                var first = CompileExpression(bin.Left, ref fctx, ref ctx, instructions, ctx.Bool);
                instructions.Add(new Jmp(ifstart, first, JumpType.JmpTrue));
            }
            if (bin.Right is BinaryExpression bright && bright.Op is "&&" or "||")
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
    private static Variable CompileRefOf(Expression expr, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        switch (expr)
        {
            case VariableExpression varr:
                Variable? v = null!;
                if ((v = fctx.Variables.FirstOrDefault(x => x.Name == varr.Name)) is not null
                || (v = ctx.Globals.FirstOrDefault(x => x.Name == varr.Name)) is not null)
                {
                    var addr = fctx.NewTemp(new PtrTypeInfo(v.Type));
                    instructions.Add(new Instruction(Operation.Ref, v, null, addr));
                    return addr;
                }
                else
                {
                    ctx.Errors.Add(new Error($"Undefined variable {varr.Name}", varr.File, varr.Pos));
                }
                break;
            case MemberAccessExpression member:
                {
                    var type = InferExpressionType(member.Expr, ref fctx, ref ctx, null);
                    if (type is CustomTypeInfo cust && cust.Fields.TryGetValue(member.MemberName, out FieldInfo field))
                    {
                        var addr = CompileRefOf(member.Expr, ref fctx, ref ctx, instructions);
                        var temp = fctx.NewTemp(new PtrTypeInfo(ctx.U8));
                        instructions.Add(new Instruction(Operation.Equals, addr, null, temp));
                        var fieldRef = fctx.NewTemp(new PtrTypeInfo(field.Type));
                        instructions.Add(new Instruction(Operation.Add, temp, new Constant<long>(field.Offset, ctx.U64), fieldRef));
                        return fieldRef;
                    }
                    else if (type is PtrTypeInfo ptr && ptr.Underlaying is CustomTypeInfo cst && cst.Fields.TryGetValue(member.MemberName, out FieldInfo fld))
                    {
                        var addr = CompileExpression(member.Expr, ref fctx, ref ctx, instructions, null);
                        var temp = fctx.NewTemp(new PtrTypeInfo(ctx.U8));
                        instructions.Add(new Instruction(Operation.Equals, addr, null, temp));
                        var fieldRef = fctx.NewTemp(new PtrTypeInfo(fld.Type));
                        instructions.Add(new Instruction(Operation.Add, temp, new Constant<long>(fld.Offset, ctx.U64), fieldRef));
                        return fieldRef;
                    }
                    else
                    {
                        ctx.Errors.Add(new Error($"Type {type} doesn't has field {member.MemberName}", member.File, member.Pos));
                    }
                }
                break;
            case IndexExpression index:
                {
                    var type = InferExpressionType(index.Indexed, ref fctx, ref ctx, null);
                    if (type is PtrTypeInfo ptr)
                    {
                        var indexType = InferExpressionType(index.Indexes[0], ref fctx, ref ctx, ctx.U64);
                        if(!indexType.Equals(ctx.U64))
                        {
                            ctx.Errors.Add(new Error($"Cannot index with type {indexType}", index.Indexes[0].File, index.Indexes[0].Pos)); 
                        }
                        var i = CompileExpression(index.Indexes[0], ref fctx, ref ctx, instructions, indexType);
                        var indexed = CompileExpression(index.Indexed, ref fctx, ref ctx, instructions, null);
                        var res = fctx.NewTemp(type);
                        instructions.Add(new Instruction(Operation.Add, indexed, i, res)); 
                        return res;
                    }
                    else
                    {
                        ctx.Errors.Add(new Error($"Cannot index type {type}", index.File, index.Pos));
                    }
                }
                break;
            default:
                ctx.Errors.Add(new Error($"Cant get address of {expr}", expr.File, expr.Pos));
                break;
        }
        return fctx.NewTemp(ctx.I32);
    }
    private static Source CompileExpression(Expression expr, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions, TypeInfo? targetType)
    {
        return expr switch
        {
            BinaryExpression bin => CompileBinary(bin, ref fctx, ref ctx, instructions, targetType),
            IntegerExpression i => new Constant<long>(i.Value, InferExpressionType(i, ref fctx, ref ctx, targetType)),
            VariableExpression varr => CompileVar(varr, ref fctx, ref ctx, targetType, instructions),
            BoolExpression b => new Constant<bool>(b.Value, ctx.Bool),
            NegateExpression neg => CompileNeg(neg, ref fctx, ref ctx, instructions, targetType),
            RefExpression r => CompileRef(r, ref fctx, ref ctx, instructions),
            IndexExpression index => CompileIndex(index, ref fctx, ref ctx, instructions),
            FunctionCallExpression fncall => CompileFnCall(fncall, ref fctx, ref ctx, instructions),
            DereferenceExpression deref => CompileDeref(deref, ref fctx, ref ctx, instructions),
            NullExpression => new Constant<long>(0, ctx.I64),
            StringExpression s => CompileStr(s.Value, ref ctx),
            CastExpression cast => CompileCast(cast, ref fctx, ref ctx, instructions),
            NewObjExpression newobj => CompileNew(newobj, ref fctx, ref ctx, instructions),
            MemberAccessExpression member => CompileMember(member, ref fctx, ref ctx, instructions),
            CharExpression c => targetType?.Equals(ctx.U8) == true ? new Constant<long>((byte)c.Value, ctx.U8) : new Constant<long>((byte)c.Value, ctx.Char),
        };
    }

    private static Source CompileMember(MemberAccessExpression member, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        var type = InferExpressionType(member, ref fctx, ref ctx, null);
        var memberexprtype = InferExpressionType(member.Expr, ref fctx, ref ctx, null);
        FieldInfo field = null!;

        var temp = fctx.NewTemp(new PtrTypeInfo(ctx.U8));
        if (memberexprtype is PtrTypeInfo ptr && ptr.Underlaying is CustomTypeInfo custom && custom.Fields.TryGetValue(member.MemberName, out field))
        {
            var tempr = fctx.NewTemp(new PtrTypeInfo(field.Type));
            var res = fctx.NewTemp(field.Type);
            var src = CompileExpression(member.Expr, ref fctx, ref ctx, instructions, null);
            instructions.Add(new Instruction(Operation.Equals, src, null, temp));
            instructions.Add(new Instruction(Operation.Add, temp, new Constant<long>(field.Offset, ctx.U64), tempr));
            instructions.Add(new Instruction(Operation.Deref, tempr, null, res));
            return res;
        }
        else if (((memberexprtype as CustomTypeInfo)?.Fields.TryGetValue(member.MemberName, out field) == true))
        {

            var tempref = fctx.NewTemp(type);
            var res = fctx.NewTemp(type);
            var src = CompileExpression(member.Expr, ref fctx, ref ctx, instructions, null);
            instructions.Add(new Instruction(Operation.Ref, src, null, temp));
            instructions.Add(new Instruction(Operation.Add, temp, new Constant<long>(field.Offset, ctx.U64), tempref));
            instructions.Add(new Instruction(Operation.Deref, tempref, null, res));
            return res;
        }
        return new Constant<long>(0, ctx.Void);
    }

    private static Source CompileNew(NewObjExpression newobj, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        var type = InferExpressionType(newobj, ref fctx, ref ctx, null);
        var dest = fctx.NewTemp(type);
        if (type is CustomTypeInfo cust)
        {
            if (cust.Constructors.Count == 0 && newobj.Args.Count == 0)
            {
                return dest;
            }
            var types = new List<TypeInfo>();
            foreach (var arg in newobj.Args)
            {
                types.Add(InferExpressionType(arg, ref fctx, ref ctx, null));
            }
            var fn = cust.Constructors.FirstOrDefault(x => x.Params.Skip(1).Select(x => x.type).SequenceEqual(types));

            var prev = ctx.Errors.Count;
            if (fn is null)
            {
                var posibles = cust.Constructors.Where(x => x.Params.Count == newobj.Args.Count + 1).ToList();
                if (posibles.Count != 0)
                {
                    foreach (var posible in posibles)
                    {
                        types.Clear();
                        foreach (var (arg, (_, t)) in newobj.Args.Zip(posible.Params.Skip(1)))
                        {
                            types.Add(InferExpressionType(arg, ref fctx, ref ctx, t));
                        }
                        if (types.SequenceEqual(posible.Params.Skip(1).Select(x => x.type)))
                        {
                            fn = posible;
                            break;
                        }
                    }
                }
            }
            if (fn is not null)
            {
                if (prev != ctx.Errors.Count)
                {
                    var x = ctx.Errors.Count - prev;
                    while (x > 0)
                    {
                        ctx.Errors.RemoveAt(ctx.Errors.Count - 1);
                        x--;
                    }
                }
                fn.WasUsed = true;
                var args = new List<Source>();
                var ptr = fctx.NewTemp(new PtrTypeInfo(dest.Type));
                instructions.Add(new Instruction(Operation.Ref, dest, null, ptr));
                args.Add(ptr);
                foreach (var (arg, (_, argtype)) in newobj.Args.Zip(fn.Params.Skip(1)))
                    args.Add(CompileExpression(arg, ref fctx, ref ctx, instructions, argtype));
                instructions.Add(new FnCallInstruction(fn, args, null));
            }
            else
            {
                ctx.Errors.Add(new Error($"Type {type} has not such constructor", newobj.File, newobj.Pos));
            }
        }
        else
        {
            ctx.Errors.Add(new Error($"Type {type} has no constructors", newobj.File, newobj.Pos));
        }
        return dest;
    }

    private static Source CompileCast(CastExpression cast, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        var type = ctx.GetTypeInfo(cast.Type) ?? ctx.Void;
        var res = fctx.NewTemp(type);
        var val = CompileExpression(cast.Value, ref fctx, ref ctx, instructions, null);
        instructions.Add(new Instruction(Operation.Equals, val, null, res));
        return res;
    }

    private static Source CompileStr(string value, ref Context ctx)
    {
        if (ctx.StringConstants.TryGetValue(value, out var v))
        {
            return v;
        }
        else
        {
            var str = new Constant<string>($"str{ctx.StringConstants.Count}", new PtrTypeInfo(ctx.Char));
            ctx.StringConstants.Add(value, str);
            return str;
        }
    }

    private static Source CompileDeref(DereferenceExpression deref, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        var type = InferExpressionType(deref, ref fctx, ref ctx, null);
        var dest = fctx.NewTemp(type);
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
        var fn = ctx.Fns.FirstOrDefault(x => x.Name == fncall.Name && types.SequenceEqual(x.Params.Select(x => x.type)));
        var prev = ctx.Errors.Count;
        if (fn is null)
        {
            var posibles = ctx.Fns.Where(x => x.Name == fncall.Name && x.Params.Count == fncall.Args.Count).ToList();
            if (posibles.Count != 0)
            {
                foreach (var posible in posibles)
                {
                    types.Clear();
                    foreach (var (arg, type) in fncall.Args.Zip(posible.Params))
                    {
                        types.Add(InferExpressionType(arg, ref fctx, ref ctx, type.type));
                    }
                    if (types.SequenceEqual(posible.Params.Select(x => x.type)))
                    {
                        fn = posible;
                        break;
                    }
                }
            }
        }
        if (fn is not null)
        {
            if (prev != ctx.Errors.Count)
            {
                var x = ctx.Errors.Count - prev;
                while (x > 0)
                {
                    ctx.Errors.RemoveAt(ctx.Errors.Count - 1);
                    x--;
                }
            }
            fn.WasUsed = true;
            var res = fctx.NewTemp(fn.RetType);
            var args = new List<Source>();
            foreach (var (arg, type) in fncall.Args.Zip(fn.Params))
                args.Add(CompileExpression(arg, ref fctx, ref ctx, instrs, type.type));
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
            var dest = fctx.NewTemp(destType);
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
        switch(r.Expr)
        {
            case VariableExpression varr:
            {
                var type = InferExpressionType(varr, ref fctx, ref ctx, null);
                PtrTypeInfo destType = new PtrTypeInfo(type);
                var v = CompileExpression(varr, ref fctx, ref ctx, instrs, type);
                var res = fctx.NewTemp(destType);
                instrs.Add(new Instruction(Operation.Ref, v, null, res));
                return res;
            }
            case IndexExpression index:
                return CompileRefOf(index, ref fctx, ref ctx, instrs);
            case MemberAccessExpression member:
                return CompileRefOf(member, ref fctx, ref ctx, instrs);
        }
        throw new NotImplementedException();

    }
    private static Source CompileNeg(NegateExpression neg, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instrs, TypeInfo? target)
    {
        var type = InferExpressionType(neg.Expr, ref fctx, ref ctx, target);
        if (type.Equals(ctx.I64) || type.Equals(ctx.I32) || type.Equals(ctx.I16) || type.Equals(ctx.I8))
        {
            var res = CompileExpression(neg.Expr, ref fctx, ref ctx, instrs, type);
            var dest = fctx.NewTemp(type);
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
    private static Source CompileVar(VariableExpression varr, ref FunctionContext fctx, ref Context ctx, TypeInfo? target, List<InstructionBase> instrs)
    {
        if (fctx.Variables.FirstOrDefault(x => x.Name == varr.Name) is Variable v)
        {
            if (target is not null && !v.Type.Equals(target))
            {
                var res = fctx.NewTemp(new PtrTypeInfo(target));
                instrs.Add(new Instruction(Operation.Equals, v, null, res));
                return res;
            }
            return v;
        }
        else if (ctx.Globals.FirstOrDefault(x => x.Name == varr.Name) is Variable gvar)
        {
            if (target is not null && !gvar.Type.Equals(target))
            {
                var res = fctx.NewTemp(new PtrTypeInfo(target));
                instrs.Add(new Instruction(Operation.Equals, gvar, null, res));
                return res;
            }
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
        Source src1 = CompileExpression(bin.Left, ref fctx, ref ctx, instructions, leftType);
        Source src2 = CompileExpression(bin.Right, ref fctx, ref ctx, instructions, rightType);
        var type = InferExpressionType(bin, ref fctx, ref ctx, target);
        var res = fctx.NewTemp(type);
        var op = bin.Op switch
        {
            "+" => Operation.Add,
            "-" => Operation.Sub,
            "*" => Operation.Mul,
            "/" => Operation.Div,
            "%" => Operation.Mod,
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
            "&" => Operation.BINAND,
            "|" => Operation.BINOR,
            "^" => Operation.XOR,
            _ => throw new(bin.Op)
        };
        if (op is Operation.OR or Operation.AND)
        {
            var ifstart = fctx.NewLabel();
            var elsestart = fctx.NewLabel();
            var end = fctx.NewLabel();
            CompileLazyBoolean(bin, ifstart, elsestart, ref fctx, ref ctx, instructions);
            instructions.Add(ifstart);
            instructions.Add(new Instruction(Operation.Equals, new Constant<bool>(true, ctx.Bool), null, res));
            instructions.Add(elsestart);
        }
        else
        {
            instructions.Add(new Instruction(op, src1, src2, res));
        }
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
        else if (target.Equals(ctx.U64))
        {
            return ctx.U64;
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
        else if (target.Equals(ctx.Char))
        {
            if (i.Value > byte.MaxValue)
            {
                ctx.Errors.Add(new Error("Value out of range", i.File, i.Pos));
            }
            else return ctx.Char;
        }
        return ctx.I32;
    }
    private static bool IsNumberType(TypeInfo t)
        => t.Name is "u64" or "i64" or "u32" or "i32" or "u16" or "i16" or "u8" or "i8";
    private static bool CanImplicitlyConvertNumber(TypeInfo from, TypeInfo to)
    {
        if (from.Size > to.Size)
            return false;
        if (from.Equals(to))
            return true;
        if (from.Name[0] == to.Name[0])
            return true;
        return false;
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
            case CharExpression:
                return target?.Equals(ctx.U8) == true ? ctx.U8 : ctx.Char;
            case StringExpression:
                return new PtrTypeInfo(ctx.Char);
            case CastExpression cast:
                {
                    if (ctx.GetTypeInfo(cast.Type) is TypeInfo type)
                    {
                        return type;
                    }
                    else
                    {
                        ctx.Errors.Add(new Error($"Unknown type {cast.Type}", cast.File, cast.Type.Pos));
                        return ctx.Void;
                    }
                }
            case MemberAccessExpression member:
                {
                    var type = InferExpressionType(member.Expr, ref fctx, ref ctx, null);
                    if (type is CustomTypeInfo custom && custom.Fields.TryGetValue(member.MemberName, out var fld))
                    {
                        return fld.Type;
                    }
                    else if (type is PtrTypeInfo ptr && ptr.Underlaying is CustomTypeInfo cust && cust.Fields.TryGetValue(member.MemberName, out var f))
                    {
                        return f.Type;
                    }
                    else
                    {
                        ctx.Errors.Add(new Error($"Type {type} does not has member {member.MemberName}", member.File, member.Pos));
                        return ctx.Void;
                    }
                }
                break;
            case NewObjExpression newObj:
                {
                    var type = ctx.GetTypeInfo(newObj.Type);
                    if (type is null)
                    {
                        ctx.Errors.Add(new Error($"Unknown type {newObj.Type}", newObj.File, newObj.Type.Pos));
                        return ctx.Void;
                    }
                    return type;
                }
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
                    if (target is not null)
                    {
                        if (target.Equals(varr.Type))
                            return target;
                        if (IsNumberType(target) && IsNumberType(varr.Type) && CanImplicitlyConvertNumber(varr.Type, target))
                        {
                            return target;
                        }
                        else
                        {
                            ctx.Errors.Add(new Error($"Cannot implicitly convert {varr.Type} to {target}", v.File, v.Pos));
                            return target;
                        }
                    }
                    return varr.Type;
                }
                else if (ctx.Globals.FirstOrDefault(x => x.Name == v.Name) is Variable gvar)
                {
                    if (target is not null)
                    {
                        if (target.Equals(gvar.Type))
                            return target;
                        if (IsNumberType(target) && IsNumberType(gvar.Type) && CanImplicitlyConvertNumber(gvar.Type, target))
                        {
                            return target;
                        }
                        else
                        {
                            ctx.Errors.Add(new Error($"Cannot implicitly convert {gvar.Type} to {target}", v.File, v.Pos));
                            return target;
                        }
                    }
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
                    if (ctx.Fns.FirstOrDefault(x => x.Name == fncall.Name && types.SequenceEqual(x.Params.Select(x => x.type))) is FnInfo fn)
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
            var parameters = new List<(string name, TypeInfo type)>();
            foreach (var p in fn.Params)
            {
                if (ctx.GetTypeInfo(p.Type) is TypeInfo type)
                {
                    parameters.Add((p.Name, type));
                }
                else
                {
                    ctx.Errors.Add(new Error($"Undefined type {p.Type}", p.File, p.Pos));
                }
            }
            var retType = fn.RetType is not null ? ctx.GetTypeInfo(fn.RetType) : ctx.Void;
            if (retType is null)
            {
                ctx.Errors.Add(new Error($"Undefined type {fn.RetType}", fn.File, fn.RetType!.Pos));
                retType = ctx.Types["void"];
            }
            funcs.Add(new FnInfo(name, parameters, retType, fn.Body));
        }
        ctx.Fns.AddRange(funcs);
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
            new TypeInfo("char", 1),
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
        ctx.Char = ctx.Types["char"];
    }
}



