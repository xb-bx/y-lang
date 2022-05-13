using YLang.IR;
using YLang.AST;

namespace YLang;
public class FnInfo
{
    public string Name { get; private set; }
    public string NameInAsm { get; private set; }
    public bool WasUsed { get; set; }
    public List<(string name, TypeInfo type)> Params { get; private set; }
    public List<InstructionBase>? Compiled { get; set; }
    public TypeInfo RetType { get; private set; }
    public Statement? Body { get; private set; }
    public CallingConvention CallingConvention { get; private set; }
    public FnInfo(string name, List<(string name, TypeInfo type)> @params, TypeInfo retType, Statement? body, CallingConvention cconv = default)
    {
        (Name, Params, RetType, Body, CallingConvention) = (name, @params, retType, body, cconv);
        NameInAsm = Name + string.Join('_', @params.Select(x => x.type)).Replace("*", "ptr");
    }
    public override string ToString()
        => $"{Name}({string.Join(", ", Params.Select(x => x.type))}): {RetType}";
}



