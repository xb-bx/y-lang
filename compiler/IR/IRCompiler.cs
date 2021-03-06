namespace YLang.IR;

public class IRCompiler
{
    private static string[] firstFourArgsWinx64 = new[] { "rcx", "rdx", "r8", "r9" };
    private bool nullCheck;
    private List<InstructionBase> instructions;
    private List<Variable> vars;
    private Dictionary<string, Constant<string>> consts;
    private FnInfo? fn;
    public IRCompiler(bool nullCheck, List<InstructionBase> instructions, List<Variable> vars, FnInfo? fn, Dictionary<string, Constant<string>> consts)
        => (this.nullCheck, this.instructions, this.vars, this.fn, this.consts) = (nullCheck, instructions, vars, fn, consts);
    private (Constant<string>, Constant<string>) NewErr(string file, Position pos)
    {
        Constant<string> str1const = null, str2const = null;
        var str1 = $"Null reference at ";
        if(consts.ContainsKey(str1))
        {
            str1const = consts[str1];
        }
        else 
        {
            str1const = new Constant<string>($"str{consts.Count}", null!);
            consts.Add(str1, str1const);
        }
        var str2 = $"{file}:{pos.Line}:{pos.Column}";

        if(consts.ContainsKey(str2))
        {
            str2const = consts[str2];
        }
        else 
        {
            str2const = new Constant<string>($"str{consts.Count}", null!);
            consts.Add(str2, str2const);
        }
        return (str1const, str2const);
    }
    int count = 0;
    private void CheckNull(Source src, List<string> lines, string file, Position pos)
    {
        if(!nullCheck)
            return;
        CompileSource(src, lines, "qword", "rax");
        var (err1, err2) = NewErr(file, pos);
        lines.Add("cmp rax, 1000");
        lines.Add($"jg .ok{count}");
        lines.Add($"mov rcx, {err1}");
        lines.Add($"mov rdx, {err2}");
        lines.Add("call __nre");
        lines.Add($".ok{count++}:");
    }
    public List<string> Compile()
    {
        var lines = new List<string>();
        int resoffset = 0;
        if (fn is not null)
        {
            int offset = 16;
            foreach (var (arg, i) in vars.Where(x => x.IsArg).Select((x, i) => (x, i)))
            {
                arg.Offset = offset;
                offset += (arg.Type.Size < 8 ? 8 : arg.Type.Size);
            }
            var lastarg = vars.Where(x => x.IsArg).LastOrDefault();
            resoffset = lastarg?.Offset + lastarg?.Type.Size ?? 16;
            offset = 0;
            foreach (var (v, i) in vars.Where(x => !x.IsArg).Select((x, i) => (x, i)))
            {
                offset = v.Type.Size < 8 ? offset - 8 : offset - v.Type.Size;
                v.Offset = offset;
            }
            foreach (var v in vars)
                Console.WriteLine($"{v} {v.Offset}");
            var localsSize = 0;
            foreach (var v in vars.Where(x => !x.IsArg))
            {
                localsSize += v.Type.Size < 8 ? 8 : v.Type.Size;
            }
            lines.Add($"{fn.NameInAsm}:");
            lines.Add("push rbp");
            lines.Add("mov rbp, rsp");
            if (localsSize > 0)
                lines.Add($"sub rsp, {localsSize + (localsSize % 16 == 0 ? 8 : 0)}");
            else
                lines.Add($"sub rsp, 8");
            if (offset < 0)
            {
                lines.Add("mov rax, rbp");
                lines.Add("sub rax, 8");
                lines.Add(".clear:");
                lines.Add("mov qword[rax], 0");
                lines.Add("sub rax, 8");
                lines.Add("cmp rax, rsp");
                lines.Add("jge .clear");
            }
        }
        foreach (var instr in instructions)
        {
            switch (instr)
            {
                case Label label:
                    lines.Add($".{label}:");
                    break;
                case InlineAsmInstruction asm:
                    lines.AddRange(asm.Asm);
                    break;
                case Jmp jmp:
                    {
                        switch (jmp.Type)
                        {
                            case JumpType.Jmp:
                                lines.Add($"jmp .{jmp.Target}");
                                break;
                            default:
                                if (jmp.Condition is Constant<bool> cond)
                                {
                                    if ((cond.Value == true && jmp.Type == JumpType.JmpTrue) || (cond.Value == false) && (jmp.Type == JumpType.JmpFalse))
                                        lines.Add($"jmp .{jmp.Target}");
                                }
                                else if (jmp.Condition is Variable v)
                                {
                                    lines.Add($"cmp byte[rbp + {v.Offset}], 0");
                                    lines.Add($"{(jmp.Type == JumpType.JmpTrue ? "jne" : "je")} .{jmp.Target}");
                                }
                                break;
                        }
                    }
                    break;
                case InterfaceCall icall:
                    {
                        CheckNull(icall.Args[0], lines, icall.File, icall.Pos);
                        if (icall.Method.RetType.Size > 8)
                        {
                            lines.Add($"sub rsp, {icall.Method.RetType.Size}");
                        }
                        foreach (var arg in icall.Args.Select(x => x).Reverse())
                            if (arg is Variable v)
                            {
                                if (v.Type.Size > 8)
                                {
                                    lines.Add($"lea rax, {v.ToAsm(v.Type.Size - 8)}");
                                    for (int i = 0; i < v.Type.Size; i += 8)
                                    {
                                        lines.Add($"push qword[rax]");
                                        lines.Add($"sub rax, 8");
                                    }

                                }
                                else
                                {
                                    lines.Add($"push qword{v.ToAsm()}");
                                }
                            }
                            else
                            {
                                lines.Add($"push {arg}");
                            }
                        var thisvar = icall.Args[0] as Variable;
                        lines.Add($"mov rax, qword{thisvar.ToAsm()}");
                        lines.Add($"mov rax, [rax]");
                        lines.Add($"mov rax, [rax + {icall.Interface.Number} * 8]");
                        lines.Add($"lea rax, [rax + {icall.Method.Number} * 8]");
                        lines.Add($"call qword[rax]");
                        if (icall.Dest is not null)
                        {
                            if (icall.Dest.Type.Size > 8)
                            {
                                if (icall.Args.Count > 0)
                                    lines.Add($"add rsp, {icall.Args.Select(x => x.Type.Size < 8 ? 8 : x.Type.Size).Sum()}");

                                for (int i = 0; i < icall.Dest.Type.Size; i += 8)
                                {
                                    lines.Add("pop rax");
                                    lines.Add($"mov qword{icall.Dest.ToAsm(i)}, rax");
                                }
                            }
                            else
                            {
                                lines.Add($"mov qword{icall.Dest.ToAsm()}, rax");
                                if (icall.Args.Count > 0)
                                    lines.Add($"add rsp, {icall.Args.Select(x => x.Type.Size < 8 ? 8 : x.Type.Size).Sum()}");
                            }
                        }
                        else
                        {
                            if (icall.Args.Count > 0)
                                lines.Add($"add rsp, {icall.Args.Select(x => x.Type.Size < 8 ? 8 : x.Type.Size).Sum()}");
                            if (icall.Method.RetType.Size > 8)
                                lines.Add($"add rsp, {icall.Method.RetType.Size}");
                        }
                    }
                    break;
                case FnCallInstruction fncall:
                    {
                        if (fncall.Fn.CallingConvention == CallingConvention.Windows64)
                        {
                            var adding = fncall.Args.Count > 4 && fncall.Args.Count % 2 != 0 ? 8 : 0;
                            if (adding > 0) lines.Add("push 0");
                            if (fncall.Args.Count > 4)
                            {
                                foreach (var arg in fncall.Args.Skip(4).Reverse())
                                {
                                    if (arg is Variable v)
                                        lines.Add($"push qword{v.ToAsm()}");
                                    else
                                        lines.Add($"push {arg}");
                                }
                            }
                            lines.Add($"sub rsp, 32");
                            foreach (var (arg, i) in fncall.Args.Take(4).Select((x, i) => (x, i)))
                            {
                                lines.Add($"mov {firstFourArgsWinx64[i]}, {(arg is Variable v ? v.ToAsm() : arg)}");
                            }
                            if (fncall.Fn.IsExtern)
                                lines.Add($"call [{fncall.Fn.NameInAsm}]");
                            else
                                lines.Add($"call {fncall.Fn.NameInAsm}");
                            lines.Add($"add rsp, {(fncall.Args.Count < 4 ? 32 : 32 + (fncall.Args.Count - 4) * 8) + adding}");
                            if (fncall.Dest is not null)
                            {
                                lines.Add($"mov qword{fncall.Dest.ToAsm()}, rax");
                            }
                        }
                        else
                        {
                            if (fncall.Fn.RetType.Size > 8)
                            {
                                lines.Add($"sub rsp, {fncall.Fn.RetType.Size}");
                            }
                            foreach (var arg in fncall.Args.Select(x => x).Reverse())
                                if (arg is Variable v)
                                {
                                    if (v.Type.Size > 8)
                                    {
                                        lines.Add($"lea rax, {v.ToAsm(v.Type.Size - 8)}");
                                        for (int i = 0; i < v.Type.Size; i += 8)
                                        {
                                            lines.Add($"push qword[rax]");
                                            lines.Add($"sub rax, 8");
                                        }

                                    }
                                    else
                                    {
                                        lines.Add($"push qword{v.ToAsm()}");
                                    }
                                }
                                else
                                {
                                    lines.Add($"push {arg}");
                                }
                            if (fncall.Fn.IsExtern)
                                lines.Add($"call [{fncall.Fn.NameInAsm}]");
                            else
                                lines.Add($"call {fncall.Fn.NameInAsm}");
                            if (fncall.Dest is not null)
                            {
                                if (fncall.Dest.Type.Size > 8)
                                {
                                    if (fncall.Args.Count > 0)
                                        lines.Add($"add rsp, {fncall.Args.Select(x => x.Type.Size < 8 ? 8 : x.Type.Size).Sum()}");

                                    for (int i = 0; i < fncall.Dest.Type.Size; i += 8)
                                    {
                                        lines.Add("pop rax");
                                        lines.Add($"mov qword{fncall.Dest.ToAsm(i)}, rax");
                                    }
                                }
                                else
                                {
                                    lines.Add($"mov qword{fncall.Dest.ToAsm()}, rax");
                                    if (fncall.Args.Count > 0)
                                        lines.Add($"add rsp, {fncall.Args.Select(x => x.Type.Size < 8 ? 8 : x.Type.Size).Sum()}");
                                }
                            }
                            else
                            {
                                if (fncall.Args.Count > 0)
                                    lines.Add($"add rsp, {fncall.Args.Select(x => x.Type.Size < 8 ? 8 : x.Type.Size).Sum()}");
                                if (fncall.Fn.RetType.Size > 8)
                                    lines.Add($"add rsp, {fncall.Fn.RetType.Size}");
                            }
                        }
                    }
                    break;

                case Instruction inst: CompileInstr(inst, lines, resoffset); break;
            }
        }
        if (fn?.RetType.Name == "void")
        {
            lines.Add("leave");
            lines.Add("ret");
        }
        return lines;
    }

    private void CompileInstr(Instruction instr, List<string> lines, int resoffset)
    {
        switch (instr.Op)
        {
            case Operation.Equals:
                {
                    var (size, reg) = instr.Destination.Type.Size switch
                    {
                        1 => ("byte", "al"),
                        2 => ("word", "ax"),
                        4 => ("dword", "eax"),
                        8 => ("qword", "rax"),
                        _ => (null, null),
                    };
                    if (size is not null)
                    {
                        if (instr.First is Constant<long> c)
                        {
                            lines.Add($"mov {size}{instr.Destination.ToAsm()}, {c.Value}");
                        }
                        else
                        {
                            CompileSource(instr.First!, lines, size, reg);
                            lines.Add($"mov {size}{instr.Destination.ToAsm()}, {reg}"); ;
                        }
                    }
                    else
                    {
                        var fst = instr.First as Variable;
                        for (int i = 0; i < fst?.Type.Size; i += 8)
                        {
                            lines.Add($"mov rax, qword{fst.ToAsm(i)}");
                            lines.Add($"mov qword{instr.Destination.ToAsm(i)}, rax");
                        }
                    }
                }
                break;
            case var op when op is Operation.Add or Operation.Sub &&
                ((instr.First?.Type is PtrTypeInfo
                || instr.Second?.Type is PtrTypeInfo)
                && !(instr.First?.Type is PtrTypeInfo && instr.Second?.Type is PtrTypeInfo)):
                {
                    Source ptr = null!;
                    Source alignment = null!;
                    PtrTypeInfo ptrType = null!;
                    if (instr.First.Type is PtrTypeInfo p)
                    {
                        ptrType = p;
                        ptr = instr.First!;
                        alignment = instr.Second!;
                    }
                    else if (instr.Second.Type is PtrTypeInfo pt)
                    {
                        ptrType = pt;
                        ptr = instr.Second!;
                        alignment = instr.First!;
                    }
                    CompileSource(ptr, lines, "qword", "rax");
                    var (size, reg) = alignment.Type.Size switch
                    {
                        1 => ("byte", "bl"),
                        2 => ("word", "bx"),
                        4 => ("dword", "ebx"),
                        8 => ("qword", "rbx"),
                    };
                    CompileSource(alignment, lines, size, reg);
                    var opp = op is Operation.Add ? "add" : "sub";
                    var log = (int)Math.Log2(ptrType.Underlaying.Size);
                    if (log > 0)
                        lines.Add($"shl {reg}, {log}");
                    lines.Add($"{opp} rax, rbx");
                    lines.Add($"mov qword{instr.Destination.ToAsm()}, rax");
                }
                break;
            case Operation.Mod:
            case Operation.Div:
                {
                    if (instr.Destination.Type.Size == 1)
                    {
                        lines.Add("xor ax, ax");
                        CompileSource(instr.First, lines, "byte", "al");
                        CompileSource(instr.Second, lines, "byte", "bl");
                        lines.Add($"{(IsUnsignedNumberType(instr.Destination.Type) ? "" : "i")}div bl");
                        lines.Add($"mov byte{instr.Destination.ToAsm()}, {(instr.Op is Operation.Mod ? "ah" : "al")}");
                    }
                    else
                    {
                        var (size, reg1, reg2, remainder) = instr.Destination.Type.Size switch
                        {
                            2 => ("word", "ax", "bx", "dx"),
                            4 => ("dword", "eax", "ebx", "edx"),
                            8 => ("qword", "rax", "rbx", "rdx"),
                            _ => default,
                        };
                        CompileSource(instr.First, lines, size, reg1);
                        CompileSource(instr.Second, lines, size, reg2);
                        lines.Add("mov edx, 0");
                        lines.Add($"{(!IsUnsignedNumberType(instr.Destination.Type) ? "i" : "")}div {reg2}");
                        lines.Add($"mov {size}{instr.Destination.ToAsm()}, {(instr.Op is Operation.Mod ? remainder : reg1)}");
                    }
                }
                break;
            case Operation.Mul:
                {
                    var (size, reg1, reg2) = instr.Destination.Type.Size switch
                    {
                        1 => ("byte", "al", "bl"),
                        2 => ("word", "ax", "bx"),
                        4 => ("dword", "eax", "ebx"),
                        _ => ("qword", "rax", "rbx"),
                    };
                    CompileSource(instr.First, lines, size, reg1);
                    CompileSource(instr.Second, lines, size, reg2);
                    lines.Add($"{(!IsUnsignedNumberType(instr.Destination.Type) ? "i" : "")}mul {reg2}");
                    lines.Add($"mov {size}{instr.Destination.ToAsm()}, {reg1}");
                }
                break;
            case Operation.Add:
            case Operation.Sub:
            case Operation.Shl:
            case Operation.Shr:
            case Operation.BINAND:
            case Operation.BINOR:
            case Operation.XOR:
                {
                    bool isShift = instr.Op is Operation.Shl || instr.Op is Operation.Shr;
                    var (size, reg1, reg2) = instr.Destination.Type.Size switch
                    {
                        1 => ("byte", "al", "cl"),
                        2 => ("word", "ax", isShift ? "cl" : "cx"),
                        4 => ("dword", "eax", isShift ? "cl" : "ecx"),
                        _ => ("qword", "rax", isShift ? "cl" : "rcx"),
                    };
                    CompileSource(instr.First, lines, size, reg1);
                    var isSecconst = instr.Second is Constant<long>;
                    if (!isSecconst) CompileSource(instr.Second, lines, isShift ? "byte" : size, reg2);
                    var op = instr.Op switch
                    {
                        Operation.Add => "add",
                        Operation.Sub => "sub",
                        Operation.Shl => "shl",
                        Operation.Shr => "shr",
                        Operation.BINAND => "and",
                        Operation.BINOR => "or",
                        Operation.XOR => "xor",
                        _ => "fuck",
                    };
                    lines.Add($"{op} {reg1}, {(isSecconst ? instr.Second : reg2)}");
                    lines.Add($"mov {size}{instr.Destination.ToAsm()}, {reg1}");
                }
                break;
            case Operation.LT:
            case Operation.LTEQ:
            case Operation.GT:
            case Operation.GTEQ:
            case Operation.EQEQ:
            case Operation.NEQ:
                {
                    var (size, reg1, reg2) = instr.First.Type.Size switch
                    {
                        1 => ("byte", "al", "bl"),
                        2 => ("word", "ax", "bx"),
                        4 => ("dword", "eax", "ebx"),
                        _ => ("qword", "rax", "rbx"),
                    };
                    CompileSource(instr.First, lines, size, reg1);
                    CompileSource(instr.Second, lines, size, reg2);
                    var op = instr.Op switch
                    {
                        Operation.LT => "l",
                        Operation.GT => "g",
                        Operation.LTEQ => "le",
                        Operation.GTEQ => "ge",
                        Operation.EQEQ => "e",
                        Operation.NEQ => "ne",
                        _ => "fuck",
                    };
                    lines.Add($"cmp {reg1}, {reg2}");
                    lines.Add($"set{op} bl");
                    lines.Add($"mov byte{instr.Destination.ToAsm()}, bl");
                }
                break;
            case Operation.Ret:
                {
                    if (instr.First is Source s)
                    {
                        var (size, reg) = fn.RetType.Size switch
                        {
                            1 => ("byte", "al"),
                            2 => ("word", "ax"),
                            4 => ("dword", "eax"),
                            8 => ("qword", "rax"),
                            _ => (null, null)
                        };
                        if (size is not null)
                        {
                            CompileSource(s, lines, size, reg);
                        }
                        else
                        {
                            var fst = instr.First as Variable;
                            for (int i = 0; i < fn.RetType.Size; i += 8)
                            {
                                lines.Add($"mov rax, qword{fst.ToAsm(i)}");
                                lines.Add($"mov qword[rbp + {resoffset + i}], rax");
                            }
                        }
                    }
                    lines.Add("leave");
                    lines.Add("ret");
                }
                break;
            case Operation.Ref:
                {
                    if (instr.First is Variable v)
                    {
                        lines.Add($"lea rax, {v.ToAsm()}");
                        //lines.Add($"sub rax, {v.Type.Size}");
                        lines.Add($"mov qword{instr.Destination.ToAsm()}, rax");
                    }
                }
                break;
            case Operation.SetRef:
                {
                    CheckNull(instr.Destination, lines, instr.File, instr.Pos); 
                    var undersize = (instr.Destination.Type as PtrTypeInfo)?.Underlaying.Size;
                    var (size, reg) = undersize switch
                    {
                        1 => ("byte", "al"),
                        2 => ("word", "ax"),
                        4 => ("dword", "eax"),
                        8 => ("qword", "rax"),
                        _ => (null, null),
                    };
                    if (size is not null)
                    {
                        CompileSource(instr.First, lines, size, reg);
                        lines.Add($"mov rbx, qword{instr.Destination.ToAsm()}");
                        lines.Add($"mov {size}[rbx], {reg}");
                    }
                    else
                    {
                        var fst = instr.First as Variable;
                        lines.Add($"mov rbx, qword{instr.Destination.ToAsm()}");
                        for (int i = 0; i < undersize; i += 8)
                        {
                            lines.Add($"mov rax, qword{fst.ToAsm(i)}");
                            lines.Add($"mov qword[rbx + {i}], rax");
                        }
                    }
                }
                break;
            case Operation.Deref:
                {
                    CheckNull(instr.First, lines, instr.File, instr.Pos); 
                    var (size, reg) = instr.Destination.Type.Size switch
                    {
                        1 => ("byte", "al"),
                        2 => ("word", "ax"),
                        4 => ("dword", "eax"),
                        8 => ("qword", "rax"),
                        _ => (null, null),
                    };
                    if (size is not null)
                    {
                        var s = instr.First as Variable;
                        lines.Add($"mov rbx, qword{s.ToAsm()}");
                        lines.Add($"mov {reg}, {size}[rbx]");
                        lines.Add($"mov {size}{instr.Destination.ToAsm()}, {reg}");
                    }
                    else
                    {
                        var fst = instr.First as Variable;
                        lines.Add($"mov rax, qword{fst.ToAsm()}");
                        for (int i = 0; i < instr.Destination.Type.Size; i += 8)
                        {
                            lines.Add($"mov rbx, qword[rax + {i}]");
                            lines.Add($"mov qword{instr.Destination.ToAsm(i)}, rbx");
                        }
                    }
                }
                break;
            case Operation.Inc:
            case Operation.Dec:
                {
                    var size = instr.Destination.Type.Size switch
                    {
                        1 => "byte",
                        2 => "word",
                        4 => "dword",
                        _ => "qword",
                    };
                    lines.Add($"{(instr.Op is Operation.Inc ? "inc" : "dec")} {size}{instr.Destination.ToAsm()}");
                }
                break;
            case Operation.Neg:
                {
                    var (size, reg) = instr.Destination.Type.Size switch
                    {
                        1 => ("byte", "al"),
                        2 => ("word", "ax"),
                        4 => ("dword", "eax"),
                        _ => ("qword", "rax"),
                    };
                    if (instr.First is Variable fst && instr.Destination.Name == fst.Name)
                    {
                        lines.Add($"neg {size}{fst.ToAsm()}");
                    }
                    else
                    {
                        CompileSource(instr.First, lines, size, reg);
                        lines.Add($"neg {reg}");
                        lines.Add($"mov {size}{instr.Destination.ToAsm()}, {reg}");
                    }
                }
                break;
            case Operation.Index:
                {
                    CheckNull(instr.First, lines, instr.File, instr.Pos);
                    var underlayingSize = (instr.First.Type as PtrTypeInfo)?.Underlaying.Size;
                    var (size, reg) = underlayingSize switch
                    {
                        1 => ("byte", "cl"),
                        2 => ("word", "cx"),
                        4 => ("dword", "ecx"),
                        8 => ("qword", "rcx"),
                        _ => (null, null),
                    };
                    if (size is not null)
                    {
                        CompileSource(instr.First, lines, "qword", "rax");
                        CompileSource(instr.Second, lines, "qword", "rbx");

                        lines.Add($"mov {reg}, {size}[rax + rbx * {underlayingSize}]");
                        lines.Add($"mov {size}{instr.Destination.ToAsm()}, {reg}");
                    }
                    else
                    {
                        CompileSource(instr.First, lines, "qword", "rax");
                        CompileSource(instr.Second, lines, "qword", "rbx");
                        lines.Add($"shl rbx, {(int)Math.Log2(underlayingSize.Value)}");
                        lines.Add($"lea rax, [rax + rbx]");
                        for (int i = 0; i < underlayingSize; i += 8)
                        {
                            lines.Add($"mov rbx, qword[rax]");
                            lines.Add($"mov qword{instr.Destination.ToAsm(i)}, rbx");
                            lines.Add($"add rax, 8");
                        }
                    }
                }
                break;
        }
    }
    private static void CompileSource(Source src, List<string> lines, string size, string reg)
    {
        if (src is Variable v)
        {
            lines.Add($"mov {reg}, {size}{v.ToAsm()}");
        }
        else
        {
            if (src is Constant<long> c)
            {
                if (reg.StartsWith("r") && c.Value < uint.MaxValue)
                    if (c.Value == 0)
                        lines.Add($"xor e{reg[1..]}, e{reg[1..]}");
                    else
                        lines.Add($"mov e{reg[1..]}, {c.Value}");
                else if (c.Value == 0)
                    lines.Add($"xor {reg}, {reg}");
                else
                    lines.Add($"mov {reg}, {src}");

            }
            else
                lines.Add($"mov {reg}, {src}");
        }
    }
    private static bool IsUnsignedNumberType(TypeInfo type)
        => type.Name is "u8" or "u16" or "u32" or "u64";
}


