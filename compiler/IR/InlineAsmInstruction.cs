namespace YLang.IR;

public class InlineAsmInstruction : InstructionBase
{
    public List<string> Asm { get; private set; }
    public InlineAsmInstruction(List<string> asm, string file, Position pos)
        => (Asm, File, Pos) = (asm, file, pos);
}


