namespace YLang.IR;
public class Instruction : InstructionBase
{
    public Operation Op { get; private set; }
    public Source? First { get; private set; }
    public Source? Second { get; private set; }
    public Variable Destination { get; private set; }
    public Instruction(Operation op, Source? src1, Source? src2, Variable dest, string file, Position pos)
        => (Op, First, Second, Destination, File, Pos) = (op, src1, src2, dest, file, pos);
    public override string ToString()
    {
        return $"{Destination} = {First} {Op} {Second};";
    }
}

public class FnRefInstruction : InstructionBase 
{
    public FnInfo Fn { get; private set; }
    public Variable Destination { get; private set; }
    public FnRefInstruction(FnInfo fn, Variable dest,  string file, Position pos)
        => (Fn, Destination, File, Pos) = (fn, dest, file, pos);
    public override string ToString()
        => $"{Destination} = &{Fn}";
}

