namespace YLang.IR;

public class Variable : Source
{
    public string Name { get; private set; }
    public override TypeInfo Type { get; }
    public int Offset { get; set; }
    public bool IsArg { get; private set; }
    public bool IsGlobal { get; set; }
    public Variable(string name, TypeInfo type, bool isArg = false)
        => (Name, Type, IsArg) = (name, type, isArg);
    public override string ToString()
        => $"({(IsArg ? "arg " : " ")}{Name}: {Type})";
    public string ToAddress()
        => IsGlobal ? Name : $"rbp + {Offset}";
}


