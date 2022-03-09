namespace YLang;

public sealed class PtrType : TypeExpression 
{
    private string name;
    public int PtrDepth { get; private set; } 
    public override string Name => name;
    public TypeExpression UnderlayingType { get; private set; }
    public PtrType(TypeExpression underlaying, int ptrDepth, Position pos) 
        => (UnderlayingType, name, PtrDepth, Pos, File) = (underlaying, $"{new string('*', ptrDepth)}{underlaying}", ptrDepth, pos, underlaying.File);
    public override string ToString()
        => Name;
}

