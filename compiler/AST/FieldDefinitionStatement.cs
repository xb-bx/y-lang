namespace YLang.AST;
public abstract class FieldDefinitionStatementBase : Statement 
{

}
public class UnionDefinitionStatement : FieldDefinitionStatementBase
{
    public List<FieldDefinitionStatement> Fields;
    public UnionDefinitionStatement(List<FieldDefinitionStatement> fields, string file, Position pos)
        => (Fields, Pos, File) = (fields, pos, file);
}
public class FieldDefinitionStatement : FieldDefinitionStatementBase 
{
    public string Name { get; private set; }
    public TypeExpression Type { get; private set; }
    public FieldDefinitionStatement(string name, TypeExpression type, Position pos)
        => (Name, Type, Pos, File) = (name, type, pos, type.File);

}
