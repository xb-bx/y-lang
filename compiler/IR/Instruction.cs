namespace YLang.IR; 
public abstract class InstructionBase 
{
}
public class Instruction : InstructionBase 
{
    public Operation Op { get; private set; }
    public Source? First{ get; private set; }
    public Source? Second{ get; private set; }
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
public enum Operation 
{
    Add,
    Sub, 
    Div, 
    Mul,
    Assign,
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
}
public class Variable : Source 
{
    public string Name { get; private set; }
    public TypeInfo Type { get; private set; }
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

