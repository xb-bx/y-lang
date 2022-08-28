using System.Text;
using YLang.IR;
using YLang.AST;

namespace YLang;

public static class Compiler
{
    private struct Context
    {
        public Dictionary<string, TypeInfo> Types = null!;
        public List<FnInfo> Fns = new();
        public List<Error> Errors = new();
        public List<Variable> Globals = new();
        public List<InterfaceInfo> Interfaces = null!;
        public Dictionary<string, Constant<string>> StringConstants = new();
        public Context() { }
        public Constant<string> NewStr(string val)
        {
            if (StringConstants.TryGetValue(val, out var str))
            {
                return str;
            }
            else
            {
                var s = new Constant<string>($"str{StringConstants.Count}", new PtrTypeInfo(Char));
                StringConstants.Add(val, s);
                return s;
            }
        }
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
            else if(typeexpr is FnPtrType fnptr) 
            {
                TypeInfo CheckType(Context ctx, TypeExpression type, TypeInfo? typeinfo) 
                {
                    if(typeinfo is null) ctx.Errors.Add(new Error($"Unknown type {type}", type.File, type.Pos));
                    return typeinfo ?? ctx.Void;
                }
                var ct = this;
                var args = fnptr.Arguments.Select(x => CheckType(ct, x, ct.GetTypeInfo(x))).ToList();
                var ret = CheckType(this, fnptr.ReturnType, GetTypeInfo(fnptr.ReturnType));
                return new FnPtrTypeInfo(args, ret);
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
        Console.WriteLine($"Need to opt: {settings.Optimize}");
        if (settings.DumpIR is not null && !settings.DumpIR.Exists)
        {
            settings.DumpIR.Create();
        }
        var ctx = new Context();
        AddDefaultTypes(ref ctx);
        var fns = statements.OfType<FnDefinitionStatement>().ToList();
        var globals = statements.OfType<LetStatement>().ToList();
        var structs = statements.OfType<StructDefinitionStatement>().ToList();
        var imports = statements.OfType<DllImportStatement>().ToList();
        var enums = statements.OfType<EnumDeclarationStatement>().ToList();
        var interfaces = statements.OfType<InterfaceDefinitionStatement>().ToList();
        AddEmptyInterfaces(ref ctx, interfaces);
        AddEnums(ref ctx, enums);
        AddStructs(structs, ref ctx);
        AddInterfaces(ref ctx, interfaces);
        CheckStructsInterfaces(ref ctx, structs);
        var globalctx = new FunctionContext();
        globalctx.NewVar("__globals_start", ctx.Void);
        var instrs = new List<InstructionBase>();
        foreach (var global in globals)
        {
            if (IsConstant(global.Value))
            {
                CompileStatement(ref globalctx, ref ctx, global, instrs);
            }
            else
            {
                ctx.Errors.Add(new Error("Global variables can be initialized only with constant values", global.Value.File, global.Value.Pos));
            }
        }

        globalctx.NewVar("__globals_end", ctx.Void);
        globalctx.Variables.ForEach(x => x.IsGlobal = true);
        ctx.Globals.AddRange(globalctx.Variables);

        foreach (var import in imports)
        {
            foreach (var fn in import.Imports)
            {

                var name = fn.Name;
                var parameters = new List<(string name, TypeInfo type)>();
                foreach (var p in fn.Parameters)
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
                var retType = fn.ReturnType is not null ? ctx.GetTypeInfo(fn.ReturnType) : ctx.Void;
                if (retType is null)
                {
                    ctx.Errors.Add(new Error($"Undefined type {fn.ReturnType}", fn.File, fn.ReturnType!.Pos));
                    retType = ctx.Types["void"];
                }
                ctx.Fns.Add(new FnInfo(name, parameters, retType, null, import.CallingConvention, true));
            }
        }
        if (fns.Count == 0)
        {
            return new();
        }
        AddFunctions(ref ctx, fns);
        var res = new List<string>();
        List<(List<Variable>, FnInfo)> compiledfns = new();
        var main = ctx.Fns.FirstOrDefault(x => x.Name == "main" && x.Params.Count == 0);
        if (main is null)
        {
            ctx.Errors.Add(new Error("No entry point", ctx.Fns.FirstOrDefault()?.Body?.File ?? "<source>", new Position()));
            return ctx.Errors;
        }
        var chartype = ctx.Char;
        if (settings.NullChecks && ctx.Fns.FirstOrDefault(x => x.Name == "writestr" && x.Params.Count == 1 && x.Params[0].type.Equals(new PtrTypeInfo(chartype))) is FnInfo writestrfn)
        {
            writestrfn.WasUsed = true;
        }
        main.WasUsed = true;
        bool smthWasCompiled = true;
        while (smthWasCompiled)
        {
            smthWasCompiled = false;
            foreach (var fn in ctx.Fns.Where(x => x.WasUsed && x.Compiled is null && x.Body is not null))
            {
                smthWasCompiled = true;
                Console.WriteLine($"Compiling {fn}");
                var f = CompileFn(ref ctx, fn);
                //Console.WriteLine(string.Join('\n', f.Info.Compiled));
                compiledfns.Add((f.Variables, f.Info));
            }
        }
        foreach (var (vars, fn) in compiledfns)
        {
            if (!fn.WasUsed && fn.Name != "main")
                continue;
            var fctx = new FunctionContext();
            fctx.Variables = vars;
            fctx.Info = fn;
            Console.WriteLine(fn.Name);
            if (settings.Optimize && fn.Compiled is not null)
                Optimize(fn.Compiled, vars);
            if (settings.DumpIR is not null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Variables: ");
                foreach (var variable in vars)
                    sb.AppendLine(variable.ToString());
                sb.AppendLine("-------- BODY ---------");
                foreach (var instr in fn.Compiled)
                    sb.AppendLine(instr.ToString());
                File.WriteAllText(Path.Combine(settings.DumpIR.FullName, $"{fn.NameInAsm}.ir"), sb.ToString());
            }
            res.AddRange(new IRCompiler(settings.NullChecks, fn.Compiled, vars, fn, ctx.StringConstants).Compile());
        }
        var globalsinit = new IRCompiler(settings.NullChecks, instrs, new(), null!, ctx.StringConstants).Compile();
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
                .AppendLine(string.Join('\n', globalsinit))
                .AppendLine("sub rsp, 8")
                .AppendLine("call main")
                .AppendLine("__exitprog:")
                .AppendLine("invoke ExitProcess, 0");
            if (settings.NullChecks)
                result
                    .AppendLine("__nre:")
                    .AppendLine("push rdx")
                    .AppendLine("push rcx")
                    .AppendLine("call writestrptrchar")
                    .AppendLine("add rsp, 8")
                    .AppendLine("call writestrptrchar")
                    .AppendLine("add rsp, 8")
                    .AppendLine("jmp __exitprog");
            result.AppendLine(string.Join('\n', res));
            if (ctx.Globals.Count > 0 || ctx.StringConstants.Count > 0)
            {
                result.AppendLine("section '.data' data readable writable");
                GenerateTypeInfos(ref ctx, result);
                foreach (var global in ctx.Globals)
                    if (!global.Name.StartsWith('@'))
                        result.AppendLine($"{global.Name} dq {(global.Type.Size > 8 ? string.Join(", ", Enumerable.Repeat("0", global.Type.Size / 8)) : "0")}");
                foreach (var (s, str) in ctx.StringConstants)
                    result.AppendLine($"{str.Value} db {string.Join(',', s.Select(x => (byte)x).Append((byte)0))}");
                GenerateVTables(ref ctx, result);
            }
            result
                .AppendLine("section '.idata' import data readable writeable")
                .Append("library kernel32,'kernel32.dll', user32, 'user32.dll'");
            if (imports.Count > 0)
            {
                result.Append(", ");
                foreach (var (import, i) in imports.Select((x, i) => (x, i)))
                {
                    result.Append("dll").Append(i).Append(',').Append("'").Append(import.Dll).Append("'");
                    if (i < imports.Count - 1)
                        result.Append(',');
                }
                result.AppendLine();
                foreach (var (import, i) in imports.Select((x, i) => (x, i)).Where(x => x.x.Imports.Count > 0))
                {
                    result.Append("import ").Append("dll").Append(i);
                    foreach (var fn in import.Imports)
                    {
                        var name = fn.Name + string.Join('_', fn.Parameters.Select(x => x.Type)).Replace("*", "ptr");
                        result.Append(", ").Append(name).Append(", '").Append(fn.ImportName ?? fn.Name).Append("'");
                    }
                    if (i < imports.Count - 1)
                        result.AppendLine();
                }
                result.AppendLine();
            }
            else
            {
                result.AppendLine();
            }
            result.AppendLine("import kernel32, ExitProcess, 'ExitProcess'");
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
                GenerateTypeInfos(ref ctx, result);
                foreach (var global in ctx.Globals)
                    result.AppendLine($"{global.Name} dq {(global.Type.Size > 8 ? string.Join(", ", Enumerable.Repeat("0", global.Type.Size / 8)) : "0")}");
                foreach (var (s, str) in ctx.StringConstants)
                    result.AppendLine($"{str.Value} db {string.Join(',', s.Select(x => (byte)x).Append((byte)0))}");
            }
        }
        File.WriteAllText(output, result.ToString());
        return ctx.Errors;
    }

    private static void GenerateVTables(ref Context ctx, StringBuilder result)
    {
        foreach (var struc in ctx.Types.Values.OfType<CustomTypeInfo>().Where(x => x.Interfaces.Count > 0))
        {
            result.Append($"@vtable_{struc.Name} dq ");
            int i = 0;
            foreach (var interfac in struc.Interfaces)
            {
                while (i < interfac.Number - 1)
                {
                    result.Append("0,");
                    i++;
                }
                result.Append($"vtable_{struc.Name}_{interfac.Name}");
                i = interfac.Number;
                if (interfac.Number < struc.Interfaces.Last().Number)
                    result.Append(',');
                else result.AppendLine();
            }
            foreach (var interfac in struc.Interfaces)
            {
                if (interfac.Methods.Count > 0)
                    result.Append($"vtable_{struc.Name}_{interfac.Name} dq ");
                foreach (var method in interfac.Methods)
                {
                    var fn = struc.Methods.FirstOrDefault(x => x.Name.EndsWith("." + method.Name) && method.Params.Skip(1).Select(y => y.type).IsSequenceEquals(x.Params.Skip(1).Select(p => p.type)));
                    result.Append($"{fn?.NameInAsm}");
                    if (method.Number < interfac.Methods.Count - 1)
                        result.Append(", ");
                    else result.AppendLine();
                }
            }
        }
    }
    private static void AddEmptyInterfaces(ref Context ctx, List<InterfaceDefinitionStatement> interfaces)
    {
        var interfaceslist = new List<InterfaceInfo>();
        ctx.Interfaces = interfaceslist;
        foreach (var interf in interfaces)
        {
            var interfType = new InterfaceInfo(interf.Name, new(), interfaceslist.Count);
            interfaceslist.Add(interfType);
            if (ctx.Types.ContainsKey(interfType.Name))
            {
                ctx.Errors.Add(new Error($"Type {interfType.Name} already defined", interf.File, interf.Pos));
                continue;
            }
            ctx.Types.Add(interfType.Name, interfType);
        }
    }
    private static void AddInterfaces(ref Context ctx, List<InterfaceDefinitionStatement> interfaces)
    {
        foreach (var interf in interfaces)
        {
            InterfaceInfo interfType = (ctx.Types[interf.Name] as InterfaceInfo)!;
            var methods = interfType.Methods;
            foreach (var fn in interf.Functions)
            {
                var @params = new List<(string name, TypeInfo type)>();
                @params.Add(("this", new PtrTypeInfo(interfType)));
                foreach (var param in fn.Params)
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
                var name = fn.Name;
                TypeInfo returnType = fn.RetType is null ? ctx.Void : ctx.GetTypeInfo(fn.RetType)!;
                if (returnType is null)
                {
                    ctx.Errors.Add(new Error($"Unknown type {fn.RetType}", fn.RetType.File, fn.RetType.Pos));
                    returnType = ctx.Void;
                }
                var info = new InterfaceMethod(name, @params, returnType, methods.Count);
                methods.Add(info);
            }
        }
    }
    private static void AddEnums(ref Context context, List<EnumDeclarationStatement> enums)
    {
        foreach (var enumDecl in enums)
        {
            if (context.Types.ContainsKey(enumDecl.Name))
            {
                context.Errors.Add(new Error($"Type {enumDecl.Name} already declared", enumDecl.File, enumDecl.Pos));
                continue;
            }
            context.Types.Add(enumDecl.Name, new EnumInfo(enumDecl.Name, enumDecl.Values));
        }
    }
    private static void Optimize(List<InstructionBase> instrs, List<Variable> vars, int optimization = 8)
    {
        Console.WriteLine("OPTIMIZE");
        for (int opc = 0; opc < optimization; opc++)
            for (int ind = 0; ind < instrs.Count; ind++)
            {
                var instr = instrs[ind];
                var next = ind + 1 < instrs.Count ? instrs[ind + 1] : null;
                if (instr is Instruction i)
                {
                    if (i.Op is Operation.Neg && i.First is Constant<long> c)
                    {
                        instrs[ind] = new Instruction(Operation.Equals, new Constant<long>(-c.Value, c.Type), null, i.Destination, i.File, i.Pos);
                    }
                    else if (i.Op is Operation.Add or Operation.Sub && i.Second is Constant<long> cc && cc.Value == 0 && i.Destination.Type.Size == i.First.Type.Size)
                    {
                        instrs[ind] = new Instruction(Operation.Equals, i.First, null, i.Destination, i.File, i.Pos);
                    }
                    else if (i.Op is Operation.Add or Operation.Sub && i.Second is Constant<long> ccc && ccc.Value == 1
                            && next is Instruction nexti && i.First is Variable ivar
                            && nexti.First is Variable nextv
                            && nexti.Op is Operation.Equals && nexti.Destination == ivar
                            && nextv == i.Destination)
                    {
                        instrs.RemoveAt(ind);
                        var op = i.Op is Operation.Add ? Operation.Inc : Operation.Dec;
                        instrs[ind] = new Instruction(op, null, null, nexti.Destination, next.File, next.Pos);
                    }
                    else if (CheckProducesConstant(i) && next is Instruction nextiv && UsesVar(nextiv, i.Destination))
                    {
                        instrs.RemoveAt(ind);
                        var leftc = i.First as Constant<long>;
                        var rightc = i.Second as Constant<long>;
                        var left = leftc!.Value;
                        var right = rightc!.Value;
                        var val = i.Op switch
                        {
                            Operation.Add => left + right,
                            Operation.Sub => left - right,
                            Operation.Div => left / right,
                            Operation.Mul => left * right,
                        };
                        var fst = nextiv.First == i.Destination ? new Constant<long>(val, i.Destination.Type) : nextiv.First;
                        var snd = nextiv.Second == i.Destination ? new Constant<long>(val, i.Destination.Type) : nextiv.Second;
                        instrs[ind] = new Instruction(nextiv.Op, fst, snd, nextiv.Destination, nextiv.File, nextiv.Pos);
                    }
                    else if (false && CheckIsConst(i) && next is Instruction nextii && UsesVar(nextii, i.Destination))
                    {
                        int index = i.Destination.IsTemp ? ind : ind + 1;
                        if (i.Destination.IsTemp) instrs.RemoveAt(ind);
                        var fst = nextii.First == i.Destination ? i.First : nextii.First;
                        var snd = nextii.Second == i.Destination ? i.First : nextii.Second;
                        instrs[index] = new Instruction(nextii.Op, fst, snd, nextii.Destination, nextii.File, nextii.Pos);
                    }
                    else if(i.Op is Operation.Equals && i.First!.Type is PtrTypeInfo && i.Destination.Type is PtrTypeInfo)
                    {
                        if(ind + 1 < instrs.Count && instrs[ind + 1] is Instruction secnd) 
                        {
                            if(secnd.Op is Operation.Deref && secnd.First == i.Destination) 
                            {
                                instrs.RemoveAt(ind); 
                                instrs.RemoveAt(ind); 
                                var newinstr = new Instruction(Operation.Deref, i.First, null, secnd.Destination, i.File, i.Pos);
                                instrs.Insert(ind, newinstr);
                            }
                        }
                    }
                }
            }
        var used = vars.ToDictionary(x => x, x => false);
        foreach (var instr in instrs)
        {
        }
    }
    private static bool CheckIsConst(Instruction i)
    {
        return i.First is Constant<long> && i.Op is Operation.Equals;
    }
    private static bool UsesVar(Instruction i, Variable v)
    {
        return i.First == v || i.Second == v;
    }
    private static bool UsesVar(InstructionBase instr, Variable v)
    {
        switch (instr)
        {
            case Instruction i: return i.First == v || i.Second == v || i.Destination == v; break;
            case InterfaceCall icall: return icall.Dest == v || icall.Args.Any(x => x == v); break;
            case FnCallInstruction call: return call.Dest == v || call.Args.Any(x => x == v); break;
            case Jmp jmp: return jmp.Condition == v; break;
            default: throw new();
        }
    }
    private static bool CheckProducesConstant(Instruction i)
    {
        return i.First is Constant<long> && i.Second is Constant<long> && i.Op is Operation.Add or Operation.Mul or Operation.Sub or Operation.Div;
    }
    private static void GenerateTypeInfos(ref Context ctx, StringBuilder sb)
    {
        var generated = new HashSet<TypeInfo>();
        foreach (var type in ctx.Types.Values)
        {
            GenerateTypeInfo(ref ctx, type, sb, generated);
        }
    }
    private static void GenerateTypeInfo(ref Context ctx, TypeInfo type, StringBuilder sb, HashSet<TypeInfo> generated)
    {
        if (type is CustomTypeInfo custom && !generated.Contains(type))
        {
            generated.Add(type);
            foreach (var field in custom.Fields.Values)
            {
                GenerateTypeInfo(ref ctx, field.Type, sb, generated);
            }
            sb.AppendLine($"__fields_of_{type.Name}:");
            foreach (var (fldname, field) in custom.Fields)
            {
                var namestr = ctx.NewStr(fldname);
                sb.AppendLine($"__field_{type.Name}_{fldname} dq {namestr}, __type_{field.Type.Name.Replace("*", "ptr")}, {field.Offset}");
            }
            var name = ctx.NewStr(type.Name);
            sb.AppendLine($"__type_{type.Name} dq {name}, {type.Size}, __fields_of_{type.Name}, {custom.Fields.Count}, 0, {generated.Count}");
        }
        else if (type is PtrTypeInfo ptr && !generated.Contains(type))
        {
            var name = ctx.NewStr("ptr");
            sb.AppendLine($"__type_{type.Name.Replace("*", "ptr")} dq {name}, {type.Size}, 0, 0, __type_{ptr.Underlaying.Name.Replace("*", "ptr")}");
            generated.Add(type);
            GenerateTypeInfo(ref ctx, ptr.Underlaying, sb, generated);
        }
        else if (type is not PtrTypeInfo && !generated.Contains(type))
        {
            var name = ctx.NewStr(type.Name);
            sb.AppendLine($"__type_{type.Name} dq {name}, {type.Size}, 0, 0, 0, {generated.Count}");
            generated.Add(type);
        }
    }
    private static void AddStructs(List<StructDefinitionStatement> structs, ref Context ctx)
    {
        var structTypes = structs.Select(x => new CustomTypeInfo(x.Name, new(), new(), new(), new())).ToList();
        foreach (var t in structTypes)
        {
            ctx.Types.Add(t.Name, t);
        }
        foreach (var (structt, struc) in structs.Zip(structTypes))
        {
            var interfaces = new List<InterfaceInfo>(struc.Interfaces.Count);
            foreach (var interf in structt.Interfaces)
            {
                if (ctx.GetTypeInfo(interf) is InterfaceInfo interfaceInfo)
                {
                    interfaces.Add(interfaceInfo);
                }
                else
                {
                    ctx.Errors.Add(new Error($"Cannot find interface {interf}", interf.File, interf.Pos));
                    continue;
                }
            }
            interfaces = interfaces.OrderBy(x => x.Number).ToList();
            var fields = new Dictionary<string, FieldInfo>();
            int offset = 0;
            if (interfaces.Count > 0)
            {
                fields.Add("@vtable", new FieldInfo(0, ctx.U64));
                offset = 8;
            }
            foreach (var sfield in structt.Fields)
            {
                if (sfield is FieldDefinitionStatement field)
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
                else if (sfield is UnionDefinitionStatement union)
                {
                    int max = 0;
                    foreach (var fld in union.Fields)
                    {
                        var type = ctx.GetTypeInfo(fld.Type);
                        if (type is null)
                        {
                            ctx.Errors.Add(new Error($"Unknown type {fld.Type}", fld.File, fld.Type.Pos));
                        }
                        else
                        {
                            fields.Add(fld.Name, new FieldInfo(offset, type));
                            max = type.Size > max ? type.Size : max;
                        }
                    }
                    offset += max;
                }
            }
            var ctors = new List<FnInfo>();
            var methods = new List<FnInfo>();
            struc.Constructors = ctors;
            struc.Methods = methods;
            struc.Fields = fields;
            struc.Interfaces = interfaces;
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
            foreach (var fn in structt.Methods)
            {
                var @params = new List<(string name, TypeInfo type)>();
                @params.Add(("this", new PtrTypeInfo(struc)));
                foreach (var param in fn.Params)
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
                var name = $"{structt.Name}.{fn.Name}";
                TypeInfo returnType = fn.RetType is null ? ctx.Void : ctx.GetTypeInfo(fn.RetType)!;
                if (returnType is null)
                {
                    ctx.Errors.Add(new Error($"Unknown type {fn.RetType}", fn.RetType.File, fn.RetType.Pos));
                    returnType = ctx.Void;
                }
                var info = new FnInfo(name, @params, returnType, fn.Body);
                ctx.Fns.Add(info);
                methods.Add(info);
            }
            if (interfaces.Count > 0)
                ctx.Globals.Add(new Variable($"@vtable_{struc.Name}", new PtrTypeInfo(new PtrTypeInfo(ctx.Void))) { IsGlobal = true });
            struc.RecomputeSize();
        }
        foreach(var type in ctx.Types.Select(x => x.Value).OfType<CustomTypeInfo>()) 
            OffsetFields(type);
    }
    private static void OffsetFields(CustomTypeInfo type) 
    {
        var offset = 0;
        foreach(var (_, fld) in type.Fields) 
        {
            if(fld.Type is CustomTypeInfo c) { OffsetFields(c); c.RecomputeSize(); }
            fld.Offset = offset;
            offset += fld.Type.Size; 
        }
        type.RecomputeSize();
    }
    private static void CheckStructsInterfaces(ref Context ctx, List<StructDefinitionStatement> structs)
    {
        Console.WriteLine($"COUNT IS S: {structs.Count}");
        foreach (var structt in structs)
        {
            Console.WriteLine($"ISTR: {structt.Name}");
            CustomTypeInfo struc = (ctx.Types[structt.Name] as CustomTypeInfo)!;
            var methods = struc.Methods;
            var interfaces = struc.Interfaces;
            foreach (var interf in interfaces)
            {
                Console.WriteLine(interf.Name);
                foreach (var method in interf.Methods)
                {
                    if (methods.FirstOrDefault(
                                x =>
                                    x.Name.EndsWith('.' + method.Name)
                                    && x.Params.Count == method.Params.Count
                                    && x.Params.Skip(1).Select(y => y.type).ToList().IsSequenceEquals(x.Params.Skip(1).Select(p => p.type).ToList())) is FnInfo fn)
                    {
                        Console.WriteLine($"{fn.Params.Skip(1).Count()} {method.Params.Skip(1).Count()}");

                        fn.WasUsed = true;
                    }
                    else
                    {
                        ctx.Errors.Add(new Error($"Struct {struc.Name} does not implement method {interf.Name}.{method.Name}", structt.File, structt.Pos));
                    }
                }
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
        public Variable NewTemp(TypeInfo type)
        {
            var v = NewVar($"t{TempCount++}", type);
            v.IsTemp = true;
            return v;
        }
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
        if (info.Name.EndsWith("._ctor"))
        {
            var cust = info.Params[0].type as PtrTypeInfo;
            if (cust?.Underlaying is CustomTypeInfo struc && struc.Interfaces.Count > 0)
            {
                var thisvar = fctx.Variables.First(x => x.Name == "this");
                var vtable = ctx.Globals.First(x => x.Name == $"@vtable_{struc.Name}");
                var vtableref = fctx.NewTemp(new PtrTypeInfo(vtable.Type));
                vtable.RefTaken = true;
                res.Add(new Instruction(Operation.Ref, vtable, null, vtableref, info.Body.File, info.Body.Pos));
                SetField(thisvar, struc, struc.Fields["@vtable"], vtableref, ref fctx, ref ctx, res, info.Body.File, info.Body.Pos);
            }
        }
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
                    if (letType is InterfaceInfo)
                    {
                        ctx.Errors.Add(new Error($"Cannot declare variable of interface type", let.Type.File, let.Type.Pos));
                        return;
                    }
                    var valueType = InferExpressionType(let.Value, ref fctx, ref ctx, letType);
                    if (letType is PtrTypeInfo ptr && ptr.Underlaying is InterfaceInfo interf && valueType is PtrTypeInfo vptr && vptr.Underlaying is CustomTypeInfo cust && cust.Interfaces.Contains(interf)) { }
                    else if (!letType.Equals(valueType))
                    {
                        ctx.Errors.Add(new Error($"Cannot assign value of type {valueType} to variable of type {letType}", let.File, let.Value.Pos));
                        return;
                    }
                    var res = CompileExpression(let.Value, ref fctx, ref ctx, instructions, valueType);
                    var varr = fctx.NewVar(let.Name, letType);
                    instructions.Add(new Instruction(Operation.Equals, res, null, varr, let.File, let.Pos));
                }
                else
                {
                    var valueType = InferExpressionType(let.Value, ref fctx, ref ctx, null);
                    var res = CompileExpression(let.Value, ref fctx, ref ctx, instructions, valueType);
                    var varr = fctx.NewVar(let.Name, valueType);
                    instructions.Add(new Instruction(Operation.Equals, res, null, varr, let.File, let.Pos));
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
                            instructions.Add(new Instruction(Operation.Equals, res, null, v, ass.File, ass.Pos));
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
                        instructions.Add(new Instruction(Operation.SetRef, res, null, destexpr, deref.File, deref.Pos));
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
                        instructions.Add(new Instruction(Operation.SetRef, value, null, refr, index.File, index.Pos));
                    }
                    else if (ass.Expr is MemberAccessExpression member)
                    {
                        var type = InferExpressionType(member, ref fctx, ref ctx, null);
                        var valueType = InferExpressionType(ass.Value, ref fctx, ref ctx, type);
                        if (!type.Equals(valueType))
                        {
                            ctx.Errors.Add(
                                new Error(
                                    $"Cannot store value of type {valueType} to variable of type {type}", ass.Value.File, ass.Value.Pos
                                )
                            );
                        }
                        var refr = CompileRefOf(member, ref fctx, ref ctx, instructions);
                        var value = CompileExpression(ass.Value, ref fctx, ref ctx, instructions, valueType);
                        instructions.Add(new Instruction(Operation.SetRef, value, null, refr, member.File, member.Pos));
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
                    instructions.Add(new InlineAsmInstruction(ls, asm.File, asm.Pos));
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
                            instructions.Add(new Instruction(Operation.Ret, null, null, null!, ret.File, ret.Pos));
                        }
                    }
                    else
                    {
                        var valueType = InferExpressionType(ret.Value, ref fctx, ref ctx, fctx.RetType);
                        if (valueType.Equals(fctx.RetType))
                        {
                            var res = CompileExpression(ret.Value, ref fctx, ref ctx, instructions, fctx.RetType);
                            instructions.Add(new Instruction(Operation.Ret, res, null, null!, ret.File, ret.Pos));
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
                        var jmp = new Jmp(elsestart, cond, JumpType.JmpFalse, ifElse.File, ifElse.Pos);
                        instructions.Add(jmp);
                    }
                    instructions.Add(ifstart);
                    CompileStatement(ref fctx, ref ctx, ifElse.Body, instructions);
                    var jmptoend = new Jmp(end, null, JumpType.Jmp, ifElse.File, ifElse.Pos);
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
                    var jmptocond = new Jmp(condition, null, JumpType.Jmp, wh.File, wh.Pos);
                    instructions.Add(jmptocond);
                    var loopbody = fctx.NewLabel();
                    instructions.Add(loopbody);
                    CompileStatement(ref fctx, ref ctx, wh.Body, instructions);
                    var condType = InferExpressionType(wh.Cond, ref fctx, ref ctx, ctx.Bool);
                    if (condType != ctx.Bool)
                        ctx.Errors.Add(new Error($"Condition must be boolean", wh.File, wh.Cond.Pos));
                    instructions.Add(condition);
                    var cond = CompileExpression(wh.Cond, ref fctx, ref ctx, instructions, ctx.Bool);
                    var jmpif = new Jmp(loopbody, cond, JumpType.JmpTrue, wh.File, wh.Pos);
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
                instructions.Add(new Jmp(elsestart, first, JumpType.JmpFalse, bin.File, bin.Pos));
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
                instructions.Add(new Jmp(elsestart, second, JumpType.JmpFalse, bin.File, bin.Pos));
            }
            instructions.Add(new Jmp(ifstart, null, JumpType.Jmp, bin.File, bin.Pos));
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
                instructions.Add(new Jmp(ifstart, first, JumpType.JmpTrue, bin.File, bin.Pos));
            }
            if (bin.Right is BinaryExpression bright && bright.Op is "&&" or "||")
            {
                CompileLazyBoolean(bright, ifstart, elsestart, ref fctx, ref ctx, instructions);
            }
            else
            {
                var second = CompileExpression(bin.Right, ref fctx, ref ctx, instructions, ctx.Bool);
                instructions.Add(new Jmp(ifstart, second, JumpType.JmpTrue, bin.File, bin.Pos));
            }
            instructions.Add(new Jmp(elsestart, null, JumpType.Jmp, bin.File, bin.Pos));
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
                    v.RefTaken = true;
                    instructions.Add(new Instruction(Operation.Ref, v, null, addr, expr.File, expr.Pos));
                    return addr;
                }
                else if(ctx.Fns.FirstOrDefault(x => x.Name == varr.Name) is FnInfo fn) 
                {
                    var addr = fctx.NewTemp(fn.MakeType());
                    fn.WasUsed = true;
                    instructions.Add(new FnRefInstruction(fn, addr, expr.File, expr.Pos));
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
                        instructions.Add(new Instruction(Operation.Equals, addr, null, temp, member.File, member.Pos));
                        var fieldRef = fctx.NewTemp(new PtrTypeInfo(field.Type));
                        instructions.Add(new Instruction(Operation.Add, temp, new Constant<long>(field.Offset, ctx.U64), fieldRef, member.File, member.Pos));
                        return fieldRef;
                    }
                    else if (type is PtrTypeInfo ptr && ptr.Underlaying is CustomTypeInfo cst && cst.Fields.TryGetValue(member.MemberName, out FieldInfo fld))
                    {
                        var addr = CompileExpression(member.Expr, ref fctx, ref ctx, instructions, null);
                        var temp = fctx.NewTemp(new PtrTypeInfo(ctx.U8));
                        instructions.Add(new Instruction(Operation.Equals, addr, null, temp, member.File, member.Pos));
                        var fieldRef = fctx.NewTemp(new PtrTypeInfo(fld.Type));
                        instructions.Add(new Instruction(Operation.Add, temp, new Constant<long>(fld.Offset, ctx.U64), fieldRef, member.File, member.Pos));
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
                        if (!indexType.Equals(ctx.U64))
                        {
                            ctx.Errors.Add(new Error($"Cannot index with type {indexType}", index.Indexes[0].File, index.Indexes[0].Pos));
                        }
                        var i = CompileExpression(index.Indexes[0], ref fctx, ref ctx, instructions, indexType);
                        var indexed = CompileExpression(index.Indexed, ref fctx, ref ctx, instructions, null);
                        var res = fctx.NewTemp(type);
                        instructions.Add(new Instruction(Operation.Add, indexed, i, res, index.File, index.Pos));
                        return res;
                    }
                    else
                    {
                        ctx.Errors.Add(new Error($"Cannot index type {type}", index.File, index.Pos));
                    }
                }
                break;
            case Expression exprs:
                {
                    var exprType = InferExpressionType(exprs, ref fctx, ref ctx, null);
                    var res = CompileExpression(exprs, ref fctx, ref ctx, instructions, exprType);
                    if (res is not Variable)
                    {
                        ctx.Errors.Add(new Error($"Cant get address of {expr}", expr.File, expr.Pos));
                    }
                    else
                    {
                        var tempref = fctx.NewTemp(new PtrTypeInfo(res.Type));
                        (res as Variable).RefTaken = true;
                        instructions.Add(new Instruction(Operation.Ref, res, null, tempref, exprs.File, exprs.Pos));
                        return tempref;
                    }

                }
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
            MethodCallExpression method => CompileMethodCall(method, ref fctx, ref ctx, instructions),
            BoxExpression box => CompileBoxExpression(box, ref fctx, ref ctx, instructions),
            TypeOfExpression typeofexpr => CompileTypeOf(typeofexpr, ref fctx, ref ctx, instructions),
        };
    }
    private static Source CompileTypeOf(TypeOfExpression typeOf, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        var type = ctx.GetTypeInfo(typeOf.Type);
        if (type is null)
        {
            ctx.Errors.Add(new Error($"Unknown type {typeOf.Type}", typeOf.File, typeOf.Type.Pos));
            return fctx.NewTemp(ctx.Types["TypeInfo"]);
        }
        return CompileTypeOf(type, ref fctx, ref ctx, instructions, typeOf.File, typeOf.Pos);
    }
    private static Source CompileTypeOf(TypeInfo type, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions, string file, Position pos)
    {
        if (type is PtrTypeInfo ptrType)
        {
            var underType = CompileTypeOf(ptrType.Underlaying, ref fctx, ref ctx, instructions, file, pos);
            var newObj = ctx.Fns.First(x => x.Name == "new_obj" && x.Params[0].type.Name == "u64");
            newObj.WasUsed = true;
            var typeInfo = ctx.Types["TypeInfo"] as CustomTypeInfo;
            var typeoftypeinfo = CompileTypeOf(typeInfo, ref fctx, ref ctx, instructions, file, pos);
            var obj = fctx.NewTemp(ctx.Types["Obj"]);
            instructions.Add(new FnCallInstruction(newObj, new List<Source>() { new Constant<long>(typeInfo.Size, ctx.U64), typeoftypeinfo }, obj, file, pos));
            var objref = fctx.NewTemp(new PtrTypeInfo(obj.Type));
            obj.RefTaken = true;
            instructions.Add(new Instruction(Operation.Ref, obj, null, objref, file, pos));
            var objptr = GetField(objref, obj.Type, (obj.Type as CustomTypeInfo).Fields["data"], ref fctx, ref ctx, instructions, file, pos);
            var typeinfovar = fctx.NewTemp(new PtrTypeInfo(typeInfo));
            instructions.Add(new Instruction(Operation.Equals, objptr, null, typeinfovar, file, pos));
            SetField(typeinfovar, typeInfo, typeInfo.Fields["name"], ctx.NewStr("ptr"), ref fctx, ref ctx, instructions, file, pos);
            SetField(typeinfovar, typeInfo, typeInfo.Fields["size"], new Constant<long>(8, ctx.U64), ref fctx, ref ctx, instructions, file, pos);
            SetField(typeinfovar, typeInfo, typeInfo.Fields["field_count"], new Constant<long>(0, ctx.U64), ref fctx, ref ctx, instructions, file, pos);
            SetField(typeinfovar, typeInfo, typeInfo.Fields["underlaying"], underType, ref fctx, ref ctx, instructions, file, pos);
            SetField(typeinfovar, typeInfo, typeInfo.Fields["id"], new Constant<long>(-1, ctx.I64), ref fctx, ref ctx, instructions, file, pos);
            return typeinfovar;
        }
        else
        {
            var typeInfo = new Variable($"__type_{type.Name}", ctx.Types["TypeInfo"]);
            typeInfo.IsGlobal = true;
            var res = fctx.NewTemp(new PtrTypeInfo(ctx.Types["TypeInfo"]));
            typeInfo.RefTaken = true;
            instructions.Add(new Instruction(Operation.Ref, typeInfo, null, res, file, pos));
            return res;
        }
    }
    private static void SetField(Variable objref, TypeInfo objtype, FieldInfo field, Source data, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions, string file, Position pos)
    {

        var temp = fctx.NewTemp(new PtrTypeInfo(ctx.U8));
        var tempr = fctx.NewTemp(new PtrTypeInfo(field.Type));
        instructions.Add(new Instruction(Operation.Equals, objref, null, temp, file, pos));
        instructions.Add(new Instruction(Operation.Add, temp, new Constant<long>(field.Offset, ctx.U64), tempr, file, pos));
        instructions.Add(new Instruction(Operation.SetRef, data, null, tempr, file, pos));
    }
    private static Variable GetField(Variable objref, TypeInfo objtype, FieldInfo field, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions, string file, Position pos)
    {

        var temp = fctx.NewTemp(new PtrTypeInfo(ctx.U8));
        var tempr = fctx.NewTemp(new PtrTypeInfo(field.Type));
        var res = fctx.NewTemp(field.Type);
        instructions.Add(new Instruction(Operation.Equals, objref, null, temp, file, pos));
        instructions.Add(new Instruction(Operation.Add, temp, new Constant<long>(field.Offset, ctx.U64), tempr, file, pos));
        instructions.Add(new Instruction(Operation.Deref, tempr, null, res, file, pos));
        return res;
    }
    private static Source CompileBoxExpression(BoxExpression box, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        var type = InferExpressionType(box.Expr, ref fctx, ref ctx, null);
        var expr = CompileExpression(box.Expr, ref fctx, ref ctx, instructions, type);
        var U64 = ctx.U64;
        var objtype = ctx.Types["Obj"] as CustomTypeInfo;
        var allocatefn = ctx.Fns.First(x => x.Name == "new_obj" && x.Params[0].type.Equals(U64));
        allocatefn.WasUsed = true;
        var obj = fctx.NewTemp(objtype);
        var typeofexpr = CompileTypeOf(type, ref fctx, ref ctx, instructions, box.File, box.Pos);
        instructions.Add(new FnCallInstruction(allocatefn, new List<Source>() { new Constant<long>(type.Size, ctx.U64), typeofexpr }, obj, box.File, box.Pos));
        var objref = fctx.NewTemp(new PtrTypeInfo(obj.Type));
        obj.RefTaken = true;
        instructions.Add(new Instruction(Operation.Ref, obj, null, objref, box.File, box.Pos));
        var objdata = GetField(objref, objtype, objtype.Fields["data"], ref fctx, ref ctx, instructions, box.File, box.Pos);
        var typedobjdata = fctx.NewTemp(new PtrTypeInfo(type));
        instructions.Add(new Instruction(Operation.Equals, objdata, null, typedobjdata, box.File, box.Pos));
        instructions.Add(new Instruction(Operation.SetRef, expr, null, typedobjdata, box.File, box.Pos));
        return obj;
    }
    private static Source CompileMethodCall(MethodCallExpression method, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        var type = InferExpressionType(method.Expr, ref fctx, ref ctx, null);
        if (type is CustomTypeInfo cust && TryGetFunction($"{cust.Name}.{method.Name}", method.Args, ref ctx, ref fctx, true) is FnInfo fn)
        {
            fn.WasUsed = true;
            var thisptr = CompileRefOf(method.Expr, ref fctx, ref ctx, instructions);
            var args = new List<Source>();
            args.Add(thisptr);
            foreach (var (arg, targetType) in method.Args.Zip(fn.Params.Skip(1).Select(x => x.type)))
            {
                args.Add(CompileExpression(arg, ref fctx, ref ctx, instructions, targetType));
            }
            var result = fctx.NewTemp(fn.RetType);
            instructions.Add(new FnCallInstruction(fn, args, result, method.File, method.Pos));
            return result;
        }
        else if (type is PtrTypeInfo iptr && iptr.Underlaying is InterfaceInfo interf)
        {
            if (TryGetInterfaceMethod(method.Name, interf, method.Args, ref ctx, ref fctx) is InterfaceMethod m)
            {
                var thisptr = CompileExpression(method.Expr, ref fctx, ref ctx, instructions, type);
                var args = new List<Source>();
                args.Add(thisptr);
                foreach (var (arg, targetType) in method.Args.Zip(m.Params.Skip(1).Select(x => x.type)))
                {
                    args.Add(CompileExpression(arg, ref fctx, ref ctx, instructions, targetType));
                }
                var result = fctx.NewTemp(m.RetType);
                instructions.Add(new InterfaceCall(interf, m, args, result, method.File, method.Pos));
                return result;
            }
            else
            {
                ctx.Errors.Add(new Error($"Interface {interf.Name} does not contains method {method.Name}", method.File, method.Pos));
                return fctx.NewTemp(ctx.Void);
            }
        }
        else if (type is PtrTypeInfo ptr
                && ptr.Underlaying is CustomTypeInfo custptr
                && TryGetFunction($"{custptr.Name}.{method.Name}", method.Args, ref ctx, ref fctx, true) is FnInfo fninfo)
        {
            fninfo.WasUsed = true;
            var thisptr = CompileExpression(method.Expr, ref fctx, ref ctx, instructions, type);
            var args = new List<Source>();
            args.Add(thisptr);
            foreach (var (arg, targetType) in method.Args.Zip(fninfo.Params.Skip(1).Select(x => x.type)))
            {
                args.Add(CompileExpression(arg, ref fctx, ref ctx, instructions, targetType));
            }
            var result = fctx.NewTemp(fninfo.RetType);
            instructions.Add(new FnCallInstruction(fninfo, args, result, method.File, method.Pos));
            return result;
        }
        else
        {
            ctx.Errors.Add(new Error($"Type {type} does not contains method {method.Name}", method.File, method.Pos));
            return new Constant<long>(0, ctx.I32);
        }
    }
    private static Source CompileMember(MemberAccessExpression member, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        if (member.Expr is VariableExpression vvar
                && fctx.Variables.FirstOrDefault(x => x.Name == vvar.Name) is null
                && ctx.Globals.FirstOrDefault(x => x.Name == vvar.Name) is null)
        {
            if (ctx.Types.TryGetValue(vvar.Name, out TypeInfo type) && type is EnumInfo enumInfo)
            {
                if (!enumInfo.Values.TryGetValue(member.MemberName, out int val))
                {
                    ctx.Errors.Add(new Error($"Enum {enumInfo.Name} does not contains value '{member.MemberName}'", member.File, member.Pos));
                }
                else
                {
                    return new Constant<long>(val, enumInfo);
                }
            }
            else
            {
                ctx.Errors.Add(new Error($"Unknown enum {vvar.Name}", vvar.File, vvar.Pos));
            }
            return new Constant<long>(0, ctx.I32);
        }
        else
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
                instructions.Add(new Instruction(Operation.Equals, src, null, temp, member.File, member.Pos));
                instructions.Add(new Instruction(Operation.Add, temp, new Constant<long>(field.Offset, ctx.U64), tempr, member.File, member.Pos));
                instructions.Add(new Instruction(Operation.Deref, tempr, null, res, member.File, member.Pos));
                return res;
            }
            else if (((memberexprtype as CustomTypeInfo)?.Fields.TryGetValue(member.MemberName, out field) == true))
            {

                var tempref = fctx.NewTemp(type);
                var res = fctx.NewTemp(type);
                var src = CompileExpression(member.Expr, ref fctx, ref ctx, instructions, null);
                (src as Variable).RefTaken = true;
                instructions.Add(new Instruction(Operation.Ref, src, null, temp, member.File, member.Pos));
                instructions.Add(new Instruction(Operation.Add, temp, new Constant<long>(field.Offset, ctx.U64), tempref, member.File, member.Pos));
                instructions.Add(new Instruction(Operation.Deref, tempref, null, res, member.File, member.Pos));
                return res;
            }
            return new Constant<long>(0, ctx.Void);
        }
    }

    private static Source CompileNew(NewObjExpression newobj, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        var type = InferExpressionType(newobj, ref fctx, ref ctx, null);
        var dest = fctx.NewTemp(type);
        if (type is CustomTypeInfo cust)
        {
            if (cust.Constructors.Count == 0 && newobj.Args.Count == 0)
            {
                if (cust.Interfaces.Count > 0)
                {
                    var destref = fctx.NewTemp(new PtrTypeInfo(cust));
                    dest.RefTaken = true;
                    instructions.Add(new Instruction(Operation.Ref, dest, null, destref, newobj.File, newobj.Pos));
                    var vtable = ctx.Globals.First(x => x.Name == $"@vtable_{cust.Name}");
                    var vtableref = fctx.NewTemp(new PtrTypeInfo(vtable.Type));
                    vtable.RefTaken = true;
                    instructions.Add(new Instruction(Operation.Ref, vtable, null, vtableref, newobj.File, newobj.Pos));
                    SetField(destref, cust, cust.Fields["@vtable"], vtableref, ref fctx, ref ctx, instructions, newobj.File, newobj.Pos);
                }
                return dest;
            }
            var types = new List<TypeInfo>();
            foreach (var arg in newobj.Args)
            {
                types.Add(InferExpressionType(arg, ref fctx, ref ctx, null));
            }
            var fn = cust.Constructors.FirstOrDefault(x => x.Params.Skip(1).Select(x => x.type).IsSequenceEquals(types));

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
                        if (types.IsSequenceEquals(posible.Params.Skip(1).Select(x => x.type)))
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
                dest.RefTaken = true;
                instructions.Add(new Instruction(Operation.Ref, dest, null, ptr, newobj.File, newobj.Pos));
                args.Add(ptr);
                foreach (var (arg, (_, argtype)) in newobj.Args.Zip(fn.Params.Skip(1)))
                    args.Add(CompileExpression(arg, ref fctx, ref ctx, instructions, argtype));
                instructions.Add(new FnCallInstruction(fn, args, null, newobj.File, newobj.Pos));
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
        instructions.Add(new Instruction(Operation.Equals, val, null, res, cast.File, cast.Pos));
        return res;
    }

    private static Source CompileStr(string value, ref Context ctx)
    {
        return ctx.NewStr(value);
    }

    private static Source CompileDeref(DereferenceExpression deref, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instructions)
    {
        var type = InferExpressionType(deref, ref fctx, ref ctx, null);
        var dest = fctx.NewTemp(type);
        var res = CompileExpression(deref.Expr, ref fctx, ref ctx, instructions, type);
        instructions.Add(new Instruction(Operation.Deref, res, null, dest, deref.File, deref.Pos));
        return dest;
    }

    private static Source CompileFnCall(FunctionCallExpression fncall, ref FunctionContext fctx, ref Context ctx, List<InstructionBase> instrs)
    {
        if (TryGetFunction(fncall.Name, fncall.Args, ref ctx, ref fctx) is FnInfo fn)
        {
            fn.WasUsed = true;
            var res = fctx.NewTemp(fn.RetType);
            var args = new List<Source>();
            foreach (var (arg, type) in fncall.Args.Zip(fn.Params))
                args.Add(CompileExpression(arg, ref fctx, ref ctx, instrs, type.type));
            instrs.Add(new FnCallInstruction(fn, args, res, fncall.File, fncall.Pos));
            return res;
        }
        else if(fctx.Variables.Any(x => x.Name == fncall.Name) || ctx.Globals.Any(x => x.Name == fncall.Name) ) 
        {
            var v = fctx.Variables.FirstOrDefault(x => x.Name == fncall.Name) ?? ctx.Globals.FirstOrDefault(x => x.Name == fncall.Name);
            if(v!.Type is FnPtrTypeInfo fntype) 
            {
                var args = new List<Source>();
                foreach(var (arg, type) in fncall.Args.Zip(fntype.Arguments)) 
                {
                    var argType = InferExpressionType(arg, ref fctx, ref ctx, type);
                    if(!argType.Equals(type))
                    {
                        ctx.Errors.Add(new Error($"Cannot convert value of type {argType} to {type}", arg.File, arg.Pos));
                    }
                    args.Add(CompileExpression(arg, ref fctx, ref ctx, instrs, argType));
                }
                if(fncall.Args.Count != fntype.Arguments.Count)
                    ctx.Errors.Add(new Error($"Cant invoke variable {v}", fncall.File, fncall.Pos));
                var dest = fntype.ReturnType.Equals(ctx.Void) ? null : fctx.NewTemp(fntype.ReturnType);
                instrs.Add(new FnRefCall(v, fntype, args, dest, fncall.File, fncall.Pos));
                return ((Source)dest) ?? new Constant<long>(0, ctx.I64);

            }
            else 
            {
                ctx.Errors.Add(new Error($"Cant invoke varialble of type {v.Type}", fncall.File, fncall.Pos));
                return new Constant<long>(0, ctx.I64);
            }
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
            instrs.Add(new Instruction(Operation.Index, source, expr, dest, index.File, index.Pos));
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
        switch (r.Expr)
        {
            case VariableExpression varr:
                {
                    if(InferExpressionType(r, ref fctx, ref ctx, null) is FnPtrTypeInfo fntype)
                    {
                        var addr = fctx.NewTemp(fntype);
                        string name = (r.Expr as VariableExpression)!.Name;
                        var fn = ctx.Fns.FirstOrDefault(x => x.Name == name);
                        fn!.WasUsed = true;
                        instrs.Add(new FnRefInstruction(fn!, addr, r.File, r.Pos));
                        return addr;
                    }
                    else 
                    {
                        var type = InferExpressionType(varr, ref fctx, ref ctx, null);
                        PtrTypeInfo destType = new PtrTypeInfo(type);
                        var v = CompileExpression(varr, ref fctx, ref ctx, instrs, type);
                        var res = fctx.NewTemp(destType);
                        (v as Variable).RefTaken = true;
                        instrs.Add(new Instruction(Operation.Ref, v, null, res, r.File, r.Pos));
                        return res;
                    }
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
            var negi = new Instruction(Operation.Neg, res, null, dest, neg.File, neg.Pos);
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
                var res = fctx.NewTemp((target));
                instrs.Add(new Instruction(Operation.Equals, v, null, res, varr.File, varr.Pos));
                return res;
            }
            return v;
        }
        else if (ctx.Globals.FirstOrDefault(x => x.Name == varr.Name) is Variable gvar)
        {
            if (target is not null && !gvar.Type.Equals(target))
            {
                var res = fctx.NewTemp(new PtrTypeInfo(target));
                instrs.Add(new Instruction(Operation.Equals, gvar, null, res, varr.File, varr.Pos));
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
            instructions.Add(new Instruction(Operation.Equals, new Constant<bool>(true, ctx.Bool), null, res, bin.File, bin.Pos));
            instructions.Add(elsestart);
        }
        else
        {
            instructions.Add(new Instruction(op, src1, src2, res, bin.File, bin.Pos));
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
        var type = FirstInferExpressionType(expr, ref fctx, ref ctx, target);
        if (target is InterfaceInfo interf && type is PtrTypeInfo ptr && ptr.Underlaying is CustomTypeInfo cust)
        {
            if (cust.Interfaces.Contains(interf))
            {
                return new PtrTypeInfo(interf);
            }
            else
            {
                ctx.Errors.Add(new Error($"Cannot convert {cust} to {interf}", expr.File, expr.Pos));
            }
        }
        return type;
    }
    private static TypeInfo FirstInferExpressionType(Expression expr, ref FunctionContext fctx, ref Context ctx, TypeInfo? target)
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
            case TypeOfExpression:
                return target?.Equals(new PtrTypeInfo(ctx.Void)) == true ? new PtrTypeInfo(ctx.Void) : new PtrTypeInfo(ctx.Types["TypeInfo"]);
            case StringExpression:
                return target?.Equals(new PtrTypeInfo(ctx.Void)) == true ? new PtrTypeInfo(ctx.Void) : new PtrTypeInfo(ctx.Char);
            case BoxExpression box:
                return ctx.Types["Obj"];
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
                    if (member.Expr is VariableExpression vvar
                            && fctx.Variables.FirstOrDefault(x => x.Name == vvar.Name) is null
                            && ctx.Globals.FirstOrDefault(x => x.Name == vvar.Name) is null)
                    {
                        if (ctx.Types.TryGetValue(vvar.Name, out TypeInfo type) && type is EnumInfo enumInfo)
                        {
                            if (!enumInfo.Values.TryGetValue(member.MemberName, out _))
                            {
                                ctx.Errors.Add(new Error($"Enum {enumInfo.Name} does not contains value '{member.MemberName}'", member.File, member.Pos));
                            }
                            if (target?.Equals(ctx.I32) is true)
                                return ctx.I32;
                            else
                                return enumInfo;
                        }
                        else
                        {
                            ctx.Errors.Add(new Error($"Unknown enum {vvar.Name}", vvar.File, vvar.Pos));
                            return ctx.I32;
                        }
                    }
                    else
                    {
                        var type = InferExpressionType(member.Expr, ref fctx, ref ctx, null);
                        if (type is CustomTypeInfo custom && custom.Fields.TryGetValue(member.MemberName, out var fld))
                        {
                            return fld.Type is PtrTypeInfo && target?.Equals(new PtrTypeInfo(ctx.Void)) == true ? new PtrTypeInfo(ctx.Void) : fld.Type;
                        }
                        else if (type is PtrTypeInfo ptr && ptr.Underlaying is CustomTypeInfo cust && cust.Fields.TryGetValue(member.MemberName, out var f))
                        {
                            return f.Type is PtrTypeInfo && target?.Equals(new PtrTypeInfo(ctx.Void)) == true ? new PtrTypeInfo(ctx.Void) : f.Type;
                        }
                        else
                        {
                            ctx.Errors.Add(new Error($"Type {type} does not has member {member.MemberName}", member.File, member.Pos));
                            return ctx.Void;
                        }
                    }
                }
            case MethodCallExpression method:
                {
                    var type = InferExpressionType(method.Expr, ref fctx, ref ctx, null);
                    CustomTypeInfo? cust = null;
                    if (
                        ((cust = type as CustomTypeInfo) is not null || (cust = (type as PtrTypeInfo)?.Underlaying as CustomTypeInfo) is not null)
                        && TryGetFunction($"{cust.Name}.{method.Name}", method.Args, ref ctx, ref fctx, true) is FnInfo fn)
                    {
                        return fn.RetType is PtrTypeInfo && target?.Equals(new PtrTypeInfo(ctx.Void)) == true ? new PtrTypeInfo(ctx.Void) : fn.RetType;
                    }
                    else if (type is PtrTypeInfo ptr && ptr.Underlaying is InterfaceInfo interf)
                    {
                        if (TryGetInterfaceMethod(method.Name, interf, method.Args, ref ctx, ref fctx) is InterfaceMethod m)
                        {
                            return m.RetType;
                        }
                        else
                        {
                            ctx.Errors.Add(new Error($"Interface {interf.Name} does not contains method {method.Name}", method.File, method.Pos));
                        }
                        return ctx.Void;
                    }
                    else
                    {
                        ctx.Errors.Add(new Error($"Type {type} does not contains method {method.Name}", method.File, method.Pos));
                        return ctx.Void;
                    }
                }
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
                        else if (varr.Type is PtrTypeInfo && target.Equals(new PtrTypeInfo(ctx.Void)))
                        {
                            return new PtrTypeInfo(ctx.Void);
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
                        else if (gvar.Type is PtrTypeInfo && target.Equals(new PtrTypeInfo(ctx.Void)))
                        {
                            return new PtrTypeInfo(ctx.Void);
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
                {
                if (r.Expr is VariableExpression v)
                {
                    if (fctx.Variables.Any(x => x.Name == v.Name) || ctx.Globals.Any(x => x.Name == v.Name))
                        return target?.Equals(new PtrTypeInfo(ctx.Void)) == true ? new PtrTypeInfo(ctx.Void) : InferExpressionType(r.Expr, ref fctx, ref ctx, null) switch
                        {
                            PtrTypeInfo ptr => new PtrTypeInfo(ptr),
                            TypeInfo t => new PtrTypeInfo(t),
                        };
                    else if(ctx.Fns.FirstOrDefault(x => x.Name == v.Name) is FnInfo fn) 
                    {   
                        return fn.MakeType();
                    }
                    else 
                    {
                        ctx.Errors.Add(new Error($"Undefined variable {v.Name}", v.File, v.Pos));
                        return target ?? ctx.Void;
                    }
                }
                return target?.Equals(new PtrTypeInfo(ctx.Void)) == true ? new PtrTypeInfo(ctx.Void) : InferExpressionType(r.Expr, ref fctx, ref ctx, null) switch
                {
                    PtrTypeInfo ptr => new PtrTypeInfo(ptr),
                    TypeInfo t => new PtrTypeInfo(t),
                };
                }
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
                        return ptr.Underlaying is PtrTypeInfo && target?.Equals(new PtrTypeInfo(ctx.Void)) == true ? new PtrTypeInfo(ctx.Void) : ptr.Underlaying;
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
                    if (TryGetFunction(fncall.Name, fncall.Args, ref ctx, ref fctx) is FnInfo fn)
                    {
                        return fn.RetType is PtrTypeInfo && target?.Equals(new PtrTypeInfo(ctx.Void)) == true ? new PtrTypeInfo(ctx.Void) : fn.RetType;
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
    private static InterfaceMethod? TryGetInterfaceMethod(string name, InterfaceInfo interf, List<Expression> args, ref Context ctx, ref FunctionContext fctx)
    {
        var types = new List<TypeInfo>();
        foreach (var arg in args)
        {
            types.Add(InferExpressionType(arg, ref fctx, ref ctx, null));
        }
        var fn = interf.Methods.FirstOrDefault(x => x.Name == name && types.IsSequenceEquals(x.Params.Skip(1).Select(x => x.type)));
        var prev = ctx.Errors.Count;
        if (fn is null)
        {
            var posibles = interf.Methods.Where(x => x.Name == name && x.Params.Count - 1 == args.Count).ToList();
            {
                foreach (var posible in posibles)
                {
                    types.Clear();
                    foreach (var (arg, type) in args.Zip(posible.Params.Skip(1)))
                    {
                        types.Add(InferExpressionType(arg, ref fctx, ref ctx, type.type));
                    }
                    if (types.IsSequenceEquals(posible.Params.Skip(1).Select(x => x.type)))
                    {
                        fn = posible;
                        break;
                    }
                }
            }
        }
        while (prev < ctx.Errors.Count)
        {
            ctx.Errors.RemoveAt(ctx.Errors.Count - 1);
        }
        return fn;
    }
    private static FnInfo? TryGetFunction(string name, List<Expression> args, ref Context ctx, ref FunctionContext fctx, bool isInstance = false)
    {
        var types = new List<TypeInfo>();
        foreach (var arg in args)
        {
            types.Add(InferExpressionType(arg, ref fctx, ref ctx, null));
        }
        var fn = ctx.Fns.FirstOrDefault(x => x.Name == name && types.IsSequenceEquals(x.Params.Skip(isInstance ? 1 : 0).Select(x => x.type)));
        var prev = ctx.Errors.Count;
        if (fn is null)
        {
            var posibles = ctx.Fns.Where(x => x.Name == name && x.Params.Count - (isInstance ? 1 : 0) == args.Count).ToList();
            if (posibles.Count != 0)
            {
                foreach (var posible in posibles)
                {
                    types.Clear();
                    foreach (var (arg, type) in args.Zip(posible.Params.Skip(isInstance ? 1 : 0)))
                    {
                        types.Add(InferExpressionType(arg, ref fctx, ref ctx, type.type));
                    }
                    if (types.IsSequenceEquals(posible.Params.Skip(isInstance ? 1 : 0).Select(x => x.type)))
                    {
                        fn = posible;
                        break;
                    }
                }
            }
        }
        while (prev < ctx.Errors.Count)
        {
            ctx.Errors.RemoveAt(ctx.Errors.Count - 1);
        }
        return fn;
    }

    private static TypeInfo InferNull(ref Context ctx, TypeInfo? target)
    {
        if (target is PtrTypeInfo or FnPtrTypeInfo)
            return target;
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



