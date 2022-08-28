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
public sealed class FnPtrType : TypeExpression 
{
    public List<TypeExpression> Arguments { get; private set; }
    public TypeExpression ReturnType { get; private set; }
    private string name;
    public override string Name => name;

    public FnPtrType(List<TypeExpression> args, TypeExpression retType, Position pos)
    {
        (Arguments, ReturnType, Pos, File) = (args, retType, pos, retType.File);
        name = ToString();
    }
    
    public override string ToString()
        => $"fnptr";
}

