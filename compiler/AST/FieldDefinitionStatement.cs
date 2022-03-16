namespace YLang.AST;

public class FieldDefinitionStatement : Statement 
{
    public string Name { get; private set; }
    public TypeExpression Type { get; private set; }
    public FieldDefinitionStatement(string name, TypeExpression type, Position pos)
        => (Name, Type, Pos, File) = (name, type, pos, type.File);

}
