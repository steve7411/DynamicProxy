using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CSharp.RuntimeBinder;

namespace DynamicProxy
{
    public class DynamicProperty
    {
        private readonly ConstantExpression _instance;
        private readonly PropertyInfo _property;

        private Func<object> _getterMethod;
        private Action<object> _setterMethod;

        private Func<object> GetterMethod
        {
            get
            {
                return _getterMethod ?? (_getterMethod = GetGetterMethod());
            }
        }

        private Action<object> SetterMethod
        {
            get
            {
                return _setterMethod ?? (_setterMethod = GetSetterMethod());
            }
        }

        public DynamicProperty(PropertyInfo property, object instance)
        {
            if (property == null)
                throw new ArgumentNullException("property");
            
            _property = property;
            _instance = Expression.Constant(instance);
        }

        public UnaryExpression GetGetterExpression()
        {
            return Expression.Convert(Expression.Property(_instance, _property), typeof(object));
        }

        public UnaryExpression GetSetterExpression(Expression value)
        {
            MemberExpression propertyExpression = Expression.Property(_instance, _property);
            return Expression.Convert(Expression.Assign(propertyExpression, Expression.Convert(value, _property.PropertyType)), typeof(object));
        }

        private Func<object> GetGetterMethod()
        {
            return Expression.Lambda<Func<object>>(GetGetterExpression(), null).Compile();
        }

        private Action<object> GetSetterMethod()
        {
            MemberExpression propertyExpression = Expression.Property(_instance, _property);
            ParameterExpression valueExpression = Expression.Parameter(typeof(object), "value");
            BinaryExpression assignment = Expression.Assign(propertyExpression, Expression.Convert(valueExpression, _property.PropertyType));
            Expression<Action<object>> lambda = Expression.Lambda<Action<object>>(assignment, valueExpression);
            return lambda.Compile();
        }

        public object Get()
        {
            return GetterMethod();
        }

        public void Set(object value)
        {
            if (!_property.CanWrite)
                throw new RuntimeBinderException(String.Format("The property \"{0}\" is not writable.", _property.Name));
            SetterMethod(value);
        }
    }
}