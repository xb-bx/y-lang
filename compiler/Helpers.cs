namespace YLang;

public static class Helpers 
{
    public static bool Try<T>(Func<T> fn, out T res)
    {
        res = default;
        try 
        {
            res = fn();
            return true;
        }
        catch 
        {
            return false;
        }
    }
}

