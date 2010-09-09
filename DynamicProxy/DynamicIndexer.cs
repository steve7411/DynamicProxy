using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DynamicProxy
{
    public class DynamicIndexer
    {
        private readonly object _instance;
        private readonly Type _instanceType;

        private readonly Dictionary<PropertyInfo, DynamicProperty> _dynamicProperties = new Dictionary<PropertyInfo, DynamicProperty>();

        public DynamicIndexer(object instance)
        {
            _instanceType = instance.GetType();
            _instance = instance;
        }

        private Expression GetGetIndexedItemExpression(IEnumerable<Expression> indexes, PropertyInfo itemProperty)
        {
            Expression accessor;
            if (typeof(Array).IsAssignableFrom(_instanceType))
            {
                accessor = Expression.ArrayIndex(Expression.Constant(_instance, _instanceType), indexes);
            }
            else
            {
                itemProperty = itemProperty ?? GetItemProperty(indexes);
                accessor = GetDynamicProperty(itemProperty).GetGetterExpression(indexes.ToArray());
            }

            return accessor;
        }

        public Expression GetGetIndexedItemExpression(IEnumerable<Expression> indexes)
        {
            return GetGetIndexedItemExpression(indexes, null);
        }

        private Expression GetSetIndexedItemExpression(IEnumerable<Expression> indexes, Expression value, PropertyInfo itemProperty)
        {
            Expression accessor;
            if (typeof(Array).IsAssignableFrom(_instanceType))
            {
                IndexExpression arrayAccessor = Expression.ArrayAccess(Expression.Constant(_instance, _instanceType), indexes);
                accessor = Expression.Convert(Expression.Assign(arrayAccessor, Expression.Convert(value, _instanceType.GetElementType())), typeof(object));
            }
            else
            {
                itemProperty = itemProperty ?? GetItemProperty(indexes);
                accessor = GetDynamicProperty(itemProperty).GetSetterExpression(value, indexes.ToArray());
            }

            return accessor;
        }

        private PropertyInfo GetItemProperty(IEnumerable<Expression> indexes)
        {
            return _instanceType.GetProperty("Item", indexes.Select(i => i.Type).ToArray());
        }

        public Expression GetSetIndexedItemExpression(IEnumerable<Expression> indexes, Expression value)
        {
            return GetSetIndexedItemExpression(indexes, value, null);
        }

        private DynamicProperty GetDynamicProperty(IEnumerable<Type> indexTypes)
        {
            PropertyInfo itemProperty = _instanceType.GetProperty("Item", indexTypes.ToArray());
            return GetDynamicProperty(itemProperty);
        }

        private DynamicProperty GetDynamicProperty(PropertyInfo itemProperty)
        {
            if (_dynamicProperties.ContainsKey(itemProperty))
            {
                return _dynamicProperties[itemProperty];
            }

            var dynamicProperty = new DynamicProperty(itemProperty, _instance);
            _dynamicProperties.Add(itemProperty, dynamicProperty);
            return dynamicProperty;
        }

        public object Get(params object[] indexes)
        {
            return GetDynamicProperty(GetElementTypes(indexes)).Get(indexes);
        }

        public void Set(object[] indexes, object value)
        {
            GetDynamicProperty(GetElementTypes(indexes)).Set(indexes, value);
        }

        private static IEnumerable<Type> GetElementTypes(IEnumerable<object> indexes)
        {
            return indexes.Select(i => i.GetType());
        }
    }
}