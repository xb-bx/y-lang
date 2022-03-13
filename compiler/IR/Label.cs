namespace YLang.IR;

public class Label : InstructionBase
{
    public int Index { get; private set; }
    public Label(int index)
        => Index = index;
    public override string ToString()
        => $"L{Index}";
}


