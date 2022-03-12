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



