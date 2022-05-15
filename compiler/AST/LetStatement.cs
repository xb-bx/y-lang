namespace YLang.AST;

public class LetStatement : Statement
{
    public string Name { get; private set; }
    public TypeExpression? Type { get; private set; }
    public Expression Value { get; private set; }
    public LetStatement(string name, TypeExpression? type, Expression value, Position pos)
        => (Name, Type, Value, Pos, File) = (name, type, value, pos, value.File);
    public override string ToString()
        => $"let {Name}: {Type} = {Value};";

}
public class DllImportStatement : Statement
{
    public string Dll { get; private set; }
    public List<ExternFunctionDefinition> Imports { get; private set; }
    public CallingConvention CallingConvention { get; private set; }
    public DllImportStatement(string dll, List<ExternFunctionDefinition> imports, string file, Position pos, CallingConvention cconv = CallingConvention.Windows64)
        => (Dll, Imports, File, Pos, CallingConvention) = (dll, imports, file, pos, cconv);

}

