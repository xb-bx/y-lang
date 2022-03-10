namespace YLang.IR;
public abstract class InstructionBase
{
}
public class Instruction : InstructionBase
{
    public Operation Op { get; private set; }
    public Source? First { get; private set; }
    public Source? Second { get; private set; }
    public Variable Destination { get; private set; }
    public Instruction(Operation op, Source? src1, Source? src2, Variable dest)
        => (Op, First, Second, Destination) = (op, src1, src2, dest);
    public override string ToString()
    {
        return $"{Destination} = {First} {Op} {Second};";
    }
}
public abstract class Source
{
}
public class Constant<T> : Source
    where T : notnull
{
    public T Value { get; private set; }
    public Constant(T value)
        => Value = value;
    public override string ToString()
        => Value!.ToString()!;
}
public class InlineAsmInstruction : InstructionBase
{
    public List<string> Asm { get; private set; }
    public InlineAsmInstruction(List<string> asm)
        => Asm = asm;
}
public enum Operation
{
    Add,
    Sub,
    Div,
    Mul,
    Equals,
    GT,
    LT,
    GTEQ,
    LTEQ,
    EQEQ,
    Not,
    Neg,
    Shr,
    Shl,
    Ref,
    Deref,
    SetRef,
    Index,
    SetIndex,
    Ret,
    OR,
    AND,
}
public class Variable : Source
{
    public string Name { get; private set; }
    public TypeInfo Type { get; private set; }
    public int Offset { get; set; }
    public bool IsArg { get; private set; }
    public Variable(string name, TypeInfo type, bool isArg = false)
        => (Name, Type, IsArg) = (name, type, isArg);
    public override string ToString()
        => $"({(IsArg ? "arg " : " ")}{Name}: {Type})";
}
public class FnCallInstruction : InstructionBase
{
    public FnInfo Fn { get; private set; }
    public List<Source> Args { get; private set; }
    public Variable Dest { get; private set; }
    public FnCallInstruction(FnInfo fn, List<Source> args, Variable dest)
        => (Fn, Args, Dest) = (fn, args, dest);
    public override string ToString()
        => $"{Dest} = call ({Fn})({string.Join(", ", Args)})";
}

public class Label : InstructionBase
{
    public int Index { get; private set; }
    public Label(int index)
        => Index = index;
    public override string ToString()
        => $"L{Index}";
}
public enum JumpType
{
    JmpTrue,
    JmpFalse,
    Jmp,
}
public class Jmp : InstructionBase
{
    public Label Target { get; private set; }
    public Source? Condition { get; private set; }
    public JumpType Type { get; private set; }
    public Jmp(Label tgt, Source? cond, JumpType type)
        => (Target, Condition, Type) = (tgt, cond, type);
    public override string ToString()
        => $"{(Type == JumpType.Jmp ? "jmp" : Type == JumpType.JmpTrue ? "jmp if" : "jmp if not")} {Condition} to {Target}";
}
public class IRCompiler
{
    public static List<string> Compile(List<InstructionBase> instructions, List<Variable> vars, FnInfo fn)
    {
        foreach (var (arg, i) in vars.Where(x => x.IsArg).Select((x, i) => (x, i)))
        {
            arg.Offset = (arg.Type.Size < 8 ? 8 : arg.Type.Size) * i + 16;
        }
        foreach (var (v, i) in vars.Where(x => !x.IsArg).Select((x, i) => (x, i)))
        {
            v.Offset = -((v.Type.Size < 8 ? 8 : v.Type.Size) * i) - 8;
        }
        foreach (var v in vars)
            Console.WriteLine($"{v} {v.Offset}");
        var localsSize = 0;
        foreach (var v in vars.Where(x => !x.IsArg))
        {
            localsSize += v.Type.Size < 8 ? 8 : v.Type.Size;
        }
        var lines = new List<string>();
        lines.Add($"{fn.NameInAsm}:");
        lines.Add("push rbp");
        lines.Add("mov rbp, rsp");
        lines.Add($"sub rsp, {localsSize}");
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
                case FnCallInstruction fncall:
                    {
                        foreach (var arg in fncall.Args.Select(x => x).Reverse())
                            if (arg is Variable v)
                            {
                                lines.Add($"push qword[rbp + {v.Offset}]");
                            }
                            else
                            {
                                lines.Add($"push {arg}");
                            }
                        lines.Add($"call {fncall.Fn.NameInAsm}");
                        lines.Add($"mov qword[rbp + {fncall.Dest.Offset}], rax");
                        foreach (var _ in fncall.Args)
                            lines.Add("pop rax");
                    }
                    break;
                case Instruction inst: CompileInstr(inst, lines, vars, fn); break;
            }
        }
        if(fn.RetType.Name == "void")
        {
            lines.Add("mov rsp, rbp");
            lines.Add("pop rbp");
            lines.Add("ret");
        }
        return lines;
    }

    private static void CompileInstr(Instruction instr, List<string> lines, List<Variable> vars, FnInfo fn)
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
                    };
                    CompileSource(instr.First!, lines, size, reg);
                    lines.Add($"mov {size}[rbp + {instr.Destination.Offset}], {reg}"); ;

                }
                break;
            case Operation.Add:
            case Operation.Sub:
            case Operation.Mul:
            case Operation.Div:
            case Operation.Shl:
            case Operation.Shr:
                {
                    var (size, reg1, reg2) = instr.Destination.Type.Size switch
                    {
                        1 => ("byte", "al", "bl"),
                        2 => ("word", "ax", "bx"),
                        4 => ("dword", "eax", "ebx"),
                        8 => ("qword", "rax", "rbx"),
                    };
                    CompileSource(instr.First, lines, size, reg1);
                    CompileSource(instr.Second, lines, size, reg2);
                    var op = instr.Op switch
                    {
                        Operation.Add => "add",
                        Operation.Sub => "sub",
                        Operation.Div => "idiv",
                        Operation.Mul => "imul",
                        Operation.Shl => "shl",
                        Operation.Shr => "shr",
                        _ => "fuck",
                    };
                    lines.Add($"{op} {reg1}, {reg2}");
                    lines.Add($"mov {size}[rbp + {instr.Destination.Offset}], {reg1}");
                }
                break;
            case Operation.LT:
            case Operation.LTEQ:
            case Operation.GT:
            case Operation.GTEQ:
            case Operation.EQEQ:
                {
                    var (size, reg1, reg2) = instr.Destination.Type.Size switch
                    {
                        1 => ("byte", "al", "bl"),
                        2 => ("word", "ax", "bx"),
                        4 => ("dword", "eax", "ebx"),
                        8 => ("qword", "rax", "rbx"),
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
                        _ => "fuck",
                    };
                    lines.Add($"cmp {reg1}, {reg2}");
                    lines.Add($"set{op} bl");
                    lines.Add($"mov byte[rbp + {instr.Destination.Offset}], bl");
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
                        };
                        CompileSource(s, lines, size, reg);
                        lines.Add("mov rsp, rbp");
                        lines.Add("pop rbp");
                        lines.Add("ret");
                    }
                }
                break;
        }
    }
    private static void CompileSource(Source src, List<string> lines, string size, string reg)
    {
        if (src is Variable v)
        {
            lines.Add($"mov {reg}, {size}[rbp + {v.Offset}]");
        }
        else
        {
            lines.Add($"mov {reg}, {src}");
        }
    }
}


