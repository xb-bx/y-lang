using System.Text;
using YLang.AST;

namespace YLang;

public class Formater 
{
    
    public static string Format(List<Statement> statements, int indentMult = 4)
    {
        var sb = new StringBuilder();
        foreach (var statement in statements)
            Format(statement, sb, 0, indentMult);
        return sb.ToString();
    }
    public static void Format(Statement stat, StringBuilder sb, int indent, int indentMult = 4)
    {
        switch (stat)
        {
            case LetStatement let:
                sb.Append(' ', indent * indentMult).AppendLine($"let {let.Name}: {let.Type} = {let.Value};");
                break;
            case AssignStatement assign:
                sb.Append(' ', indent * indentMult).AppendLine($"{assign.Expr} = {assign.Value};");
                break;
            case CallStatement call:
                sb.Append(' ', indent * indentMult).AppendLine($"{call.Call};");
                break;
            case RetStatement ret:
                sb.Append(' ', indent * indentMult).AppendLine($"ret {ret.Value};");
                break;
            case IfElseStatement ifelse:
                sb.Append(' ', indent * indentMult).Append("if ").AppendLine(ifelse.Condition.ToString());
                Format(ifelse.Body, sb, indent + 1);
                if (ifelse.Else is not null)
                {
                    sb.Append(' ', indent * indentMult).AppendLine("else");
                    Format(ifelse.Else, sb, indent + 1);
                }
                break;
            case BlockStatement block:
                sb.Append(' ', (indent - 1) * indentMult).AppendLine("{");
                foreach (var st in block.Statements)
                    Format(st, sb, indent);
                sb.Append(' ', (indent - 1) * indentMult).AppendLine("}");
                break;
            case WhileStatement wh:
                sb.Append(' ', indent * indentMult).Append("while ").AppendLine(wh.Cond.ToString());
                Format(wh.Body, sb, indent + 1, indentMult);
                break;
            case FnDefinitionStatement fn:
                sb.Append(' ', indent * indentMult).AppendLine($"fn {fn.Name}({string.Join(", ", fn.Params)}): {fn.RetType}");
                Format(fn.Body, sb, indent + 1, indentMult);
                break;
            case InlineAsmStatement asm:
                {
                    sb.Append(' ', indent * indentMult).AppendLine("asm");
                    var body = asm.Body;
                    sb.Append(' ', indent * indentMult).AppendLine("{");
                    if (body.Count > 0)
                    {
                        int prevLine = body[0].Pos.Line;
                        indent++;
                        sb.Append(' ', indent * indentMult);
                        foreach (var token in body)
                        {
                            if (prevLine != token.Pos.Line)
                            {
                                sb.AppendLine().Append(' ', indent * indentMult);
                                prevLine = token.Pos.Line;
                            }
                            sb.Append(token.Value).Append(' ');
                        }
                        sb.AppendLine();
                        indent--;
                    }
                    sb.Append(' ', indent * indentMult).AppendLine("}");
                }
                break;
        }
    }
}



