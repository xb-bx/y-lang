namespace YLang.AST;

public class ConstructorDefinitionStatement : Statement 
{
    public List<Parameter> Params { get; private set; }
    public Statement Body { get; private set; }
    public ConstructorDefinitionStatement(List<Parameter> @params, Statement body)
        => (Params, Body) = (@params, body);
}

