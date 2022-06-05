namespace YLang.IR;

public class Jmp : InstructionBase
{
    public Label Target { get; private set; }
    public Source? Condition { get; private set; }
    public JumpType Type { get; private set; }
    public Jmp(Label tgt, Source? cond, JumpType type, string file, Position pos)
        => (Target, Condition, Type, File, Pos) = (tgt, cond, type, file, pos);
    public override string ToString()
        => $"{(Type == JumpType.Jmp ? "jmp" : Type == JumpType.JmpTrue ? "jmp if" : "jmp if not")} {Condition} to {Target}";
}


