namespace YLang;

public enum CallingConvention 
{
    Y = 0,
    Windows64,
}



public static class Exts 
{
    public static bool IsSequenceEquals<T>(this IEnumerable<T> self, IEnumerable<T> other)
    {
        var first = self.ToList();
        var second = other.ToList();
        return first.Count == second.Count && first.SequenceEqual(second);
    }
}
