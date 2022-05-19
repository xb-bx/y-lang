namespace YLang.AST;
public class StructDefinitionStatement : Statement 
{
    public string Name { get; private set; }
    public List<FieldDefinitionStatementBase> Fields { get; private set; }
    public List<ConstructorDefinitionStatement> Constructors { get; private set; }
    public List<FnDefinitionStatement> Methods { get; private set; }
    public StructDefinitionStatement(string name, List<FieldDefinitionStatementBase> fields, List<ConstructorDefinitionStatement> constructors, List<FnDefinitionStatement> methods, Position pos, string file)
        => (Name, Fields, Constructors, Methods, Pos, File) = (name, fields, constructors, methods, pos, file);
}
public class EnumDeclarationStatement : Statement 
{
    public string Name { get; private set; }
    public Dictionary<string, int> Values { get; private set; }
    public EnumDeclarationStatement(string name, Dictionary<string, int> values, string file, Position pos)
        => (Name, Values, File, Pos) = (name, values, file, pos);

}

