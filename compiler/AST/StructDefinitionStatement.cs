namespace YLang.AST;

public class StructDefinitionStatement : Statement 
{
    public string Name { get; private set; }
    public List<FieldDefinitionStatement> Fields { get; private set; }
    public StructDefinitionStatement(string name, List<FieldDefinitionStatement> fields, Position pos, string file)
        => (Name, Fields, Pos, File) = (name, fields, pos, file);
}

