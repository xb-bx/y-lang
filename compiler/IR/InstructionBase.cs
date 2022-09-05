namespace YLang.IR;

public abstract class InstructionBase
{
    public string File { get; protected set; }
    public Position Pos { get; protected set; }
}
public class StackallocInstruction : InstructionBase 
{
    public Variable Destination { get; private set; }
    public int Size { get; private set; }
    public TypeInfo Type { get; private set; }
    public StackallocInstruction(Variable dest, int size, TypeInfo type, string file, Position pos)
        => (Destination, Size, Type, File, Pos) = (dest, size, type, file, pos);

}


