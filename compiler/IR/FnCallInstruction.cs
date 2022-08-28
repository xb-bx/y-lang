namespace YLang.IR;

public class FnCallInstruction : InstructionBase
{
    public FnInfo Fn { get; private set; }
    public List<Source> Args { get; private set; }
    public Variable? Dest { get; set; }
    public FnCallInstruction(FnInfo fn, List<Source> args, Variable dest, string file, Position pos)
        => (Fn, Args, Dest, File, Pos) = (fn, args, dest, file, pos);
    public override string ToString()
        => $"{Dest} = call ({Fn})({string.Join(", ", Args)})";
}
public class FnRefCall : InstructionBase 
{
    public Variable Fn { get; private set; }
    public FnPtrTypeInfo Type { get; private set; }
    public List<Source> Args { get; private set; }
    public Variable? Dest { get; set; }
    public FnRefCall(Variable fn, FnPtrTypeInfo type, List<Source> args, Variable dest, string file, Position pos)
        => (Fn, Type, Args, Dest, File, Pos) = (fn, type, args, dest, file, pos);
    public override string ToString() 
        => $"{Dest} = {Fn}({string.Join(", ", Args)});";
}
public class InterfaceCall : InstructionBase 
{
    public InterfaceInfo Interface { get; private set; }
    public InterfaceMethod Method { get; private set; }
    public List<Source> Args { get; private set; }
    public Variable? Dest { get; set; }
    public InterfaceCall(InterfaceInfo interf, InterfaceMethod method, List<Source> args, Variable? dest, string file, Position pos)
        => (Interface, Method, Args, Dest, File, Pos) = (interf, method, args, dest, file, pos);
    public override string ToString()
        => $"{Dest} = call {Interface.Name}.{Method.Name}({string.Join(",", Args)})";
}
