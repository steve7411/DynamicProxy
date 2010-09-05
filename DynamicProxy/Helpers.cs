using System.Linq;
using System.Reflection;

namespace DynamicProxy
{
    static class Helpers
    {
        internal static T[] ToArrayItem<T>(this object item)
        {
            return new[] { (T)item };
        }

        public static string GetMethodNameWithTypes(this MethodInfo method)
        {
            var fullName = method.GetParameters().Aggregate(method.Name, (pname, pi) => pname + pi.ParameterType.Name);
            return fullName;
        }
    }
}
