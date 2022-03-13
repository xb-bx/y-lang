namespace YLang.IR;
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


