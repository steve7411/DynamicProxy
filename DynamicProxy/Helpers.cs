namespace DynamicProxy
{
    static class Helpers
    {
        internal static T[] ToArrayItem<T>(this object item)
        {
            return new[] { (T)item };
        }
    }
}
