using YLang.IR;
using YLang.AST;

namespace YLang;

public class FnInfo
{
    public string Name { get; private set; }
    public string NameInAsm { get; private set; }
    public List<TypeInfo> Params { get; private set; }
    public List<InstructionBase>? Compiled { get; set; }
    public TypeInfo RetType { get; private set; }
    public FnDefinitionStatement FnDef { get; private set; }
    public FnInfo(string name, List<TypeInfo> @params, TypeInfo retType, FnDefinitionStatement fndef)
    {
        (Name, Params, RetType, FnDef) = (name, @params, retType, fndef);
        NameInAsm = Name + string.Join('_', @params).Replace("*", "");
    }
    public override string ToString()
        => $"{Name}({string.Join(", ", Params)}): {RetType}";
}



