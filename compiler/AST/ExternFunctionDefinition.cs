using System.Text;

namespace YLang.AST;
public class ExternFunctionDefinition : Statement
{
    public string Name { get; private set; }
    public string? ImportName { get; private set; }
    public TypeExpression? ReturnType { get; private set; }
    public List<Parameter> Parameters { get; private set; }
    public ExternFunctionDefinition(string name, List<Parameter> parameters, string file, Position pos, TypeExpression? returntype = null, string? importName = null)
        => (Name, ImportName, File, Pos, ReturnType, Parameters) = (name, importName, file, pos, returntype, parameters);
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("extern fn ")
            .Append(Name)
            .Append('(').Append(string.Join(", ", Parameters)).Append(')');
        if(ReturnType is not null)
        {
            builder.Append(": ").Append(ReturnType);
        }
        if(ImportName is not null)
        {
            builder.Append(" from ").Append(ImportName);
        }
        return builder.ToString();
    }
}
