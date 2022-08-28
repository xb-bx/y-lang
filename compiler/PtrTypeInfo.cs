namespace YLang;

public class PtrTypeInfo : TypeInfo
{
    public TypeInfo Underlaying { get; private set; }
    public PtrTypeInfo(TypeInfo underlaying)
        => (Underlaying, Size, Name) = (underlaying, 8, $"*{underlaying.Name}");

    public override bool Equals(object? obj)
    {
        return obj is PtrTypeInfo info &&
               EqualityComparer<TypeInfo>.Default.Equals(Underlaying, info.Underlaying);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Name, Size, Underlaying);
    }
    public override string ToString()
        => $"*{Underlaying}";
}

public class FnPtrTypeInfo : TypeInfo 
{
    public List<TypeInfo> Arguments { get; private set; }
    public TypeInfo ReturnType { get; private set; }
    public FnPtrTypeInfo(List<TypeInfo> args, TypeInfo retType) =>
        (Arguments, ReturnType, Size, Name) = (args, retType, 8, "fnptr");
}


