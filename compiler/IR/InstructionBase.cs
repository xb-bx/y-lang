namespace YLang.IR;

public abstract class InstructionBase
{
    public string File { get; protected set; }
    public Position Pos { get; protected set; }
}


