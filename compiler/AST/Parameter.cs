namespace YLang.AST;

public class Parameter : ASTNode 
{
    public string Name { get; private set; }
    public TypeExpression Type { get; private set; }
    public Parameter(string name, TypeExpression type)
        => (Name, Type, File) = (name, type, type.File);
    public override string ToString()
        => $"{Name}: {Type}";
} 

