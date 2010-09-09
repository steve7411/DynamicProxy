using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CSharp.RuntimeBinder;

namespace DynamicProxy
{
    public class DynamicProperty
    {
        private readonly PropertyInfo _property;

        private readonly DynamicMethod _getterMethod;
        private readonly DynamicMethod _setterMethod;

        public DynamicProperty(PropertyInfo property, object instance)
        {
            if (property == null)
                throw new ArgumentNullException("property");

            _property = property;

            if (property.CanRead)
            {
                _getterMethod = new DynamicMethod(property.GetGetMethod(true), instance);
            }

            if (property.CanWrite)
            {
                _setterMethod = new DynamicMethod(property.GetSetMethod(true), instance);
            }
        }

        public Expression GetGetterExpression()
        {
            if (!_property.CanRead)
                throw new RuntimeBinderException(String.Format("The property \"{0}\" is not readable.", _property.Name));

            return _getterMethod.GetMethodExpression(new Expression[0]);
        }

        public Expression GetSetterExpression(Expression value)
        {
            if (!_property.CanWrite)
                throw new RuntimeBinderException(String.Format("The property \"{0}\" is not writable.", _property.Name));

            return _setterMethod.GetMethodExpression(value.ToArrayItem<Expression>());
        }

        public object Get()
        {
            if (!_property.CanRead)
                throw new RuntimeBinderException(String.Format("The property \"{0}\" is not readable.", _property.Name));
            return _getterMethod.Invoke();
        }

        public void Set(object value)
        {
            if (!_property.CanWrite)
                throw new RuntimeBinderException(String.Format("The property \"{0}\" is not writable.", _property.Name));
            _setterMethod.Invoke(value);
        }
    }
}