namespace YLang.IR;

public class Constant<T> : Source
    where T : notnull
{
    public T Value { get; private set; }
    public override TypeInfo Type { get; }
    public Constant(T value, TypeInfo type)
        => (Value, Type) = (value, type);
    public override string ToString()
        => Value!.ToString()!;
}


