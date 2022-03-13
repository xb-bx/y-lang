namespace YLang.IR;

public class FnCallInstruction : InstructionBase
{
    public FnInfo Fn { get; private set; }
    public List<Source> Args { get; private set; }
    public Variable? Dest { get; set; }
    public FnCallInstruction(FnInfo fn, List<Source> args, Variable dest)
        => (Fn, Args, Dest) = (fn, args, dest);
    public override string ToString()
        => $"{Dest} = call ({Fn})({string.Join(", ", Args)})";
}


