namespace YLang;

public class TypeInfo
{
    public string Name { get; protected set; }
    public int Size { get; protected set; }
    public TypeInfo(string name, int size)
        => (Name, Size) = (name, size);
    protected TypeInfo()
        => Name = null!;
    public override string ToString()
        => Name;
    public override bool Equals(object? obj)
        => obj is TypeInfo type and not PtrTypeInfo ? type.Name == Name && type.Size == Size : false;

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Size);
    }
}
public class CustomTypeInfo : TypeInfo
{
    public Dictionary<string, FieldInfo> Fields {get; private set; }
    public List<FnInfo> Constructors { get; private set;}
    public CustomTypeInfo(string name, Dictionary<string, FieldInfo> fields, List<FnInfo> ctors)
    {
        (Name, Fields, Constructors) = (name, fields, ctors);
        var last = fields.LastOrDefault().Value;
        Size = last?.Offset + last?.Type.Size ?? 0;
        if(Size % 8 != 0)
            Size += 8 - (Size % 8);
    }
}

public class FieldInfo 
{
    public int Offset { get; private set; }
    public TypeInfo Type { get; private set; }
    public FieldInfo(int offset, TypeInfo type)
        => (Offset, Type) = (offset, type);
}


