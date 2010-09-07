using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DynamicProxy
{
    public class DynamicMethod
    {
        private readonly ConstantExpression _instance;
        private readonly MethodInfo _methodInfo;

        private Func<object[], object> _method;
        public Func<object[], object> Method
        {
            get { return _method ?? (_method = GetMethod()); }
        }

        public DynamicMethod(MethodInfo method, object instance)
        {
            if (method == null)
                throw new ArgumentNullException("method");

            _methodInfo = method;
            _instance = method.IsStatic ? null : Expression.Constant(instance);
        }

        public Expression GetMethodExpression(IEnumerable<Expression> args)
        {
            MethodCallExpression methodCallExpression = Expression.Call(_instance, _methodInfo, args);
            Expression caller = _methodInfo.ReturnType == typeof(void)
                                    ? (Expression)Expression.Block(methodCallExpression, Expression.Default(typeof(object)))
                                    : Expression.Convert(methodCallExpression, typeof(object));
            return caller;
        }

        private Func<object[], object> GetMethod()
        {
            var args = Expression.Parameter(typeof(object[]), "args");
            var parameterExpressions = GetParameterExpressionsFromArray(args);
            return Expression.Lambda<Func<object[], object>>(GetMethodExpression(parameterExpressions), args).Compile();
        }

        private IEnumerable<UnaryExpression> GetParameterExpressionsFromArray(ParameterExpression args)
        {
            return _methodInfo.GetParameters().Select((p, index) => Expression.Convert(Expression.ArrayIndex(args, Expression.Constant(index)), p.ParameterType));
        }

        public object Invoke(params object[] args)
        {
            return Method(args);
        }
    }
}