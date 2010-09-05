using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DynamicProxy
{
    public class DynamicIndex
    {
        private readonly ConstantExpression _instance;
        private readonly Type _instanceType;

        private readonly Dictionary<PropertyInfo, Func<object[], object>> _getIndexedItemMethods = new Dictionary<PropertyInfo, Func<object[], object>>();
        private readonly Dictionary<PropertyInfo, Action<object[], object>> _setIndexedItemMethods = new Dictionary<PropertyInfo, Action<object[], object>>();

        public DynamicIndex(object instance)
        {
            _instanceType = instance.GetType();
            _instance = Expression.Constant(instance, _instanceType);
        }

        private Expression GetGetIndexedItemExpression(IEnumerable<Expression> indexes, PropertyInfo itemProperty)
        {
            Expression accessor;
            if (typeof(Array).IsAssignableFrom(_instanceType))
            {
                accessor = Expression.ArrayIndex(_instance, indexes);
            }
            else
            {
                itemProperty = itemProperty ?? GetItemProperty(indexes);
                IndexExpression indexer = Expression.MakeIndex(_instance, itemProperty, indexes);
                accessor = Expression.Convert(indexer, typeof(object));
            }

            return accessor;
        }

        public Expression GetGetIndexedItemExpression(IEnumerable<Expression> indexes)
        {
            return GetGetIndexedItemExpression(indexes, null);
        }

        private UnaryExpression GetSetIndexedItemExpression(IEnumerable<Expression> indexes, Expression value, PropertyInfo itemProperty)
        {
            UnaryExpression accessor;
            if (typeof(Array).IsAssignableFrom(_instanceType))
            {
                IndexExpression arrayAccessor = Expression.ArrayAccess(_instance, indexes);
                accessor = Expression.Convert(Expression.Assign(arrayAccessor, Expression.Convert(value, _instanceType.GetElementType())), typeof(object));
            }
            else
            {
                itemProperty = itemProperty ?? GetItemProperty(indexes);
                accessor = Expression.Convert(Expression.Assign(Expression.MakeIndex(_instance, itemProperty, indexes), Expression.Convert(value, itemProperty.PropertyType)), typeof(object));
            }

            return accessor;
        }

        private PropertyInfo GetItemProperty(IEnumerable<Expression> indexes)
        {
            return _instanceType.GetProperty("Item", indexes.Select(i => i.Type).ToArray());
        }

        public UnaryExpression GetSetIndexedItemExpression(IEnumerable<Expression> indexes, Expression value)
        {
            return GetSetIndexedItemExpression(indexes, value, null);
        }

        private Func<object[], object> GetGetIndexedItemMethod(IEnumerable<Type> indexTypes)
        {
            PropertyInfo itemProperty = _instanceType.GetProperty("Item", indexTypes.ToArray());
            if (_getIndexedItemMethods.ContainsKey(itemProperty))
            {
                return _getIndexedItemMethods[itemProperty];
            }

            ParameterExpression indexes = Expression.Parameter(typeof(object[]), "indexes");
            Expression indexer = GetGetIndexedItemExpression(GetIndexArgumentParametersFromArray(indexes, indexTypes), itemProperty);
            Expression<Func<object[], object>> lambda = Expression.Lambda<Func<object[], object>>(indexer, indexes);
            return lambda.Compile();
        }

        private Action<object[], object> GetSetIndexedItemMethod(IEnumerable<Type> indexTypes)
        {
            PropertyInfo itemProperty = _instanceType.GetProperty("Item", indexTypes.ToArray());
            if (_setIndexedItemMethods.ContainsKey(itemProperty))
            {
                return _setIndexedItemMethods[itemProperty];
            }

            ParameterExpression indexes = Expression.Parameter(typeof(object[]), "indexes");
            ParameterExpression value = Expression.Parameter(typeof(object), "value");

            UnaryExpression indexAssigner = GetSetIndexedItemExpression(GetIndexArgumentParametersFromArray(indexes, indexTypes), value, itemProperty);
            Expression<Action<object[], object>> lambda = Expression.Lambda<Action<object[], object>>(indexAssigner, indexes, value);
            return lambda.Compile();
        }

        private static IEnumerable<UnaryExpression> GetIndexArgumentParametersFromArray(ParameterExpression args, IEnumerable<Type> indexTypes)
        {
            return indexTypes.Select((type, index) => Expression.Convert(Expression.ArrayIndex(args, Expression.Constant(index)), type));
        }

        public object Get(params object[] indexes)
        {
            return GetGetIndexedItemMethod(GetElementTypes(indexes))(indexes);
        }

        public void Set(object[] indexes, object value)
        {
            GetSetIndexedItemMethod(GetElementTypes(indexes))(indexes, value);
        }

        private static IEnumerable<Type> GetElementTypes(IEnumerable<object> indexes)
        {
            return indexes.Select(i => i.GetType());
        }
    }
}