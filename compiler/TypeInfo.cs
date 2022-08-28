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
    public Dictionary<string, FieldInfo> Fields {get; set; }
    public List<FnInfo> Constructors { get; set;}
    public List<FnInfo> Methods { get; set; }
    public List<InterfaceInfo> Interfaces { get; set; }
    public CustomTypeInfo(string name, Dictionary<string, FieldInfo> fields, List<FnInfo> ctors, List<FnInfo> methods, List<InterfaceInfo> interfaces)
    {
        (Name, Fields, Constructors, Methods, Interfaces) = (name, fields, ctors, methods, interfaces);
        RecomputeSize();
    }
    public void RecomputeSize()
    {
        var last = Fields.LastOrDefault().Value;
        Size = last?.Offset + last?.Type.Size ?? 0;
        if(Size % 8 != 0)
            Size += 8 - (Size % 8);
    }
}
public class EnumInfo : TypeInfo 
{ 
    public Dictionary<string, int> Values { get; set; }
    public EnumInfo(string name, Dictionary<string, int> values)
        => (Name, Values, Size) = (name, values, 4);
}
public class FieldInfo 
{
    public int Offset { get; set; }
    public TypeInfo Type { get; private set; }
    public FieldInfo(int offset, TypeInfo type)
        => (Offset, Type) = (offset, type);
}

public class InterfaceInfo : TypeInfo 
{
    public List<InterfaceMethod> Methods { get; private set; }
    public int Number { get; private set; }
    public InterfaceInfo(string name, List<InterfaceMethod> methods, int num)
        => (Name, Methods, Number, Size) = (name, methods, num, 0);
}

public class InterfaceMethod
{
    public string Name { get; private set; }
    public List<(string name, TypeInfo type)> Params { get; private set; }
    public int Number { get; private set; } 
    public TypeInfo RetType { get; private set; }
    public InterfaceMethod(string name, List<(string name, TypeInfo type)> @params, TypeInfo retType, int num)
        => (Name, Params, RetType, Number) = (name, @params, retType, num);
}
