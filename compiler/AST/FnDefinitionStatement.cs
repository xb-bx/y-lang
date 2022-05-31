namespace YLang.AST;

public class FnDefinitionStatement : Statement
{
    public string Name { get; private set; }
    public List<Parameter> Params { get; private set; }
    public TypeExpression? RetType { get; private set; }
    public Statement? Body { get; private set; }
    public FnDefinitionStatement(string name, List<Parameter> @params, TypeExpression? retType, Statement? body, Position pos, string file)
        => (Name, Params, RetType, Body, Pos, File) = (name, @params, retType, body, pos, file);
}

public class InterfaceDefinitionStatement : Statement 
{
    public string Name { get; private set; }
    public List<FnDefinitionStatement> Functions { get; private set; }
    public InterfaceDefinitionStatement(string name, List<FnDefinitionStatement> fns, string file, Position pos)
        => (Name, Functions, File, Pos) = (name, fns, file, pos);
}
