using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CSharp.RuntimeBinder;

namespace DynamicProxy
{
    public class DynamicProxy : IDynamicMetaObjectProvider, INotifyPropertyChanged
    {
        private readonly object _instance;
        private readonly Type _instanceType;

        public DynamicProxy(object instance)
        {
            if (instance == null)
                throw new ArgumentNullException("instance");

            _instanceType = instance.GetType();
            _instance = instance;
        }

        public DynamicProxy(string assemblyFile, string typeName, params object[] args)
        {
            Assembly assembly = Assembly.LoadFrom(assemblyFile);
            _instanceType = assembly.GetType(typeName);
            if (_instanceType == null)
                throw new ArgumentException(String.Format("The type {0} could not be loaded from {1}.", typeName, assemblyFile), "typeName");

            _instance = Activator.CreateInstance(_instanceType, args);
        }

        public DynamicMetaObject GetMetaObject(Expression parameter)
        {
            var dynamicObject = _instance as IDynamicMetaObjectProvider;
            return dynamicObject != null ? dynamicObject.GetMetaObject(parameter) : new DynamicProxyMetaObject(parameter, this);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ToString()
        {
            return _instance.ToString();
        }

        public override bool Equals(object obj)
        {
            return _instance.Equals(obj);
        }

        public override int GetHashCode()
        {
            return _instance.GetHashCode();
        }

        public new Type GetType()
        {
            return _instanceType;
        }

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        private class DynamicProxyMetaObject : DynamicMetaObject
        {
            private readonly DynamicProxy _proxy;

            private DynamicIndex _index;

            public DynamicProxyMetaObject(Expression expression, DynamicProxy proxy)
                : base(expression, BindingRestrictions.Empty, proxy)
            {
                _proxy = proxy;
            }

            private DynamicIndex Index
            {
                get { return _index ?? (_index = new DynamicIndex(_proxy._instance)); }
            }

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
            {
                PropertyInfo property = _proxy._instanceType.GetProperty(binder.Name);
                if (property == null)
                    return null;

                return new DynamicMetaObject(new DynamicProperty(property, _proxy._instance).GetGetterExpression(), BindingRestrictions.GetTypeRestriction(Expression, LimitType));
            }

            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
            {
                PropertyInfo property = _proxy._instanceType.GetProperty(binder.Name);
                if (property == null)
                    return null;

                _proxy.NotifyPropertyChanged(binder.Name);
                return new DynamicMetaObject(new DynamicProperty(property, _proxy._instance).GetSetterExpression(value.Expression), BindingRestrictions.GetTypeRestriction(Expression, LimitType));
            }

            public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
            {
                MethodInfo method = _proxy._instanceType.GetMethod(binder.Name, args.Select(d => d.RuntimeType ?? d.LimitType.MakeByRefType()).ToArray());
                if (method == null)
                    return null;

                return new DynamicMetaObject(new DynamicMethod(method, _proxy._instance).GetMethodExpression(args.Select(arg => arg.Expression)), BindingRestrictions.GetTypeRestriction(Expression, LimitType));
            }

            public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
            {
                return new DynamicMetaObject(Index.GetGetIndexedItemExpression(indexes.Select(i => i.Expression)), BindingRestrictions.GetTypeRestriction(Expression, LimitType));
            }

            public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
            {
                return new DynamicMetaObject(Index.GetSetIndexedItemExpression(indexes.Select(i => i.Expression), value.Expression), BindingRestrictions.GetTypeRestriction(Expression, LimitType));
            }

            public override DynamicMetaObject BindConvert(ConvertBinder binder)
            {
                if (binder.Type.IsAssignableFrom(_proxy._instanceType))
                    return new DynamicMetaObject(Expression.Constant(_proxy._instance), BindingRestrictions.GetTypeRestriction(Expression, LimitType));
                if (FullfillsInterface(binder.Type))
                {
                    return new DynamicMetaObject(Expression.Constant(DynamicInterface.CreateDynamicInterface(binder.Type, _proxy._instance)), BindingRestrictions.GetTypeRestriction(Expression, LimitType));
                }

                throw new RuntimeBinderException(String.Format("Cannot convert type {0} to {1}.", _proxy._instanceType.FullName, binder.Type.FullName));
            }

            private bool FullfillsInterface(Type interfaceType)
            {
                if (!interfaceType.IsInterface)
                {
                    return false;
                }

                return FullfillsInterfaceMethodDefinitions(interfaceType)
                       && FullfillsInterfacePropertyAndIndexerDefinitions(interfaceType)
                       && FullfillsInterfaceEventDefinitions(interfaceType);
            }

            private bool FullfillsInterfaceEventDefinitions(Type interfaceType)
            {
                const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var interfaceEventAccessors = interfaceType.GetEvents(bindingFlags).SelectMany(e => new[] { e.GetAddMethod(true), e.GetRemoveMethod(true) }).Select(m => m.GetMethodNameWithTypes());
                var instanceEventAccessors = _proxy._instanceType.GetEvents(bindingFlags).SelectMany(e => new[] { e.GetAddMethod(true), e.GetRemoveMethod(true) }).Select(m => m.GetMethodNameWithTypes());

                var eventIntersection = interfaceEventAccessors.Intersect(instanceEventAccessors);

                return eventIntersection.Count() == interfaceEventAccessors.Count();
            }

            private bool FullfillsInterfacePropertyAndIndexerDefinitions(Type interfaceType)
            {
                const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var interfacePropertyAccessors = interfaceType.GetProperties(bindingFlags).SelectMany(p => p.GetAccessors(true)).Select(m => m.GetMethodNameWithTypes());
                var instancePropertyAccessors = _proxy._instanceType.GetProperties(bindingFlags).SelectMany(p => p.GetAccessors(true)).Select(m => m.GetMethodNameWithTypes());

                var propertyIntersection = interfacePropertyAccessors.Intersect(instancePropertyAccessors);

                return propertyIntersection.Count() == interfacePropertyAccessors.Count();
            }

            private bool FullfillsInterfaceMethodDefinitions(Type interfaceType)
            {
                const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var interfaceMethods = interfaceType.GetMethods(bindingFlags).Where(m => !m.IsSpecialName).Select(m => m.GetMethodNameWithTypes());
                var instanceMethods = _proxy._instanceType.GetMethods(bindingFlags).Where(m => !m.IsSpecialName).Select(m => m.GetMethodNameWithTypes());
                var methodIntersect = interfaceMethods.Intersect(instanceMethods);

                return interfaceMethods.Count() == methodIntersect.Count();
            }
        }

    }
}