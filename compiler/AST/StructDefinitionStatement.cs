namespace YLang.AST;
public class StructDefinitionStatement : Statement 
{
    public string Name { get; private set; }
    public List<FieldDefinitionStatement> Fields { get; private set; }
    public List<ConstructorDefinitionStatement> Constructors { get; private set; }
    public StructDefinitionStatement(string name, List<FieldDefinitionStatement> fields, List<ConstructorDefinitionStatement> constructors, Position pos, string file)
        => (Name, Fields, Constructors, Pos, File) = (name, fields, constructors, pos, file);
}

