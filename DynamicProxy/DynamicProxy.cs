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

            private DynamicIndexer _index;

            public DynamicProxyMetaObject(Expression expression, DynamicProxy proxy)
                : base(expression, BindingRestrictions.Empty, proxy)
            {
                _proxy = proxy;
            }

            private DynamicIndexer Index
            {
                get { return _index ?? (_index = new DynamicIndexer(_proxy._instance)); }
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
                
                return new DynamicMetaObject(Expression.Constant(DynamicInterface.CreateDynamicInterface(binder.Type, _proxy._instance)), BindingRestrictions.GetTypeRestriction(Expression, LimitType));
            }
        }

    }
}