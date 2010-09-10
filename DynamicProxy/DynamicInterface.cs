using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Microsoft.CSharp.RuntimeBinder;

namespace DynamicProxy
{
    static class DynamicInterface
    {
        private const MethodAttributes PropertyAccessorMethodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final;
        private static AssemblyBuilder _asmBuilder;
        private static ModuleBuilder _modBuilder;
        static readonly Dictionary<Type, TypeBuilder> TypeBuilders = new Dictionary<Type, TypeBuilder>();

        static DynamicInterface()
        {
            GenerateAssemblyAndModule();
        }

        public static void SaveAssembly()
        {
            _asmBuilder.Save("DynamicInterfaces.dll");
        }

        private static void GenerateAssemblyAndModule()
        {
            if (_asmBuilder == null)
            {
                var assemblyName = new AssemblyName { Name = "DynamicInterfaces" };
                _asmBuilder = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);

                _modBuilder = _asmBuilder.DefineDynamicModule(_asmBuilder.GetName().Name, _asmBuilder.GetName().Name + ".dll");
            }
        }

        private static TypeBuilder CreateType(ModuleBuilder modBuilder, string typeName, Type[] interfaces)
        {
            TypeBuilder typeBuilder = modBuilder.DefineType(typeName,
                                                            TypeAttributes.Public |
                                                            TypeAttributes.Class |
                                                            TypeAttributes.AutoClass |
                                                            TypeAttributes.AnsiClass |
                                                            TypeAttributes.BeforeFieldInit |
                                                            TypeAttributes.AutoLayout,
                                                            typeof(object),
                                                            interfaces);

            return typeBuilder;
        }

        private static void BuildConstructor(TypeBuilder typeBuilder, Dictionary<MemberInfo, FieldBuilder> dynamicMembers, FieldBuilder dynamicIndexField = null)
        {
            const MethodAttributes constructorAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            ConstructorBuilder constructor = typeBuilder.DefineConstructor(constructorAttributes, CallingConventions.Standard, typeof(object).ToArrayItem<Type>());

            MethodInfo objectGetType = typeof(object).GetMethod("GetType", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

            ConstructorInfo objectCtor = typeof(object).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

            constructor.DefineParameter(1, ParameterAttributes.None, "instance");
            ILGenerator gen = constructor.GetILGenerator();

            gen.DeclareLocal(typeof(Type));

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, objectCtor);

            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Callvirt, objectGetType);
            gen.Emit(OpCodes.Stloc_0);

            InitializeDynamicMembers(gen, dynamicMembers, OpCodes.Ldloc_0);

            InitializeDynamicIndex(gen, dynamicIndexField);

            gen.Emit(OpCodes.Ret);
        }

        private static void InitializeDynamicMembers(ILGenerator gen, Dictionary<MemberInfo, FieldBuilder> dynamicMembers, OpCode loadInstanceTypeOpCode)
        {
            foreach (var kvp in dynamicMembers)
            {
                if (kvp.Key is MethodInfo)
                    InitializeDynamicMethod(gen, kvp, loadInstanceTypeOpCode);
                else if (kvp.Key is EventInfo)
                    InitializeDynamicEvent(gen, kvp, loadInstanceTypeOpCode);
                else
                    InitializeDynamicProperty(gen, kvp, loadInstanceTypeOpCode);
            }
        }

        private static void InitializeDynamicEvent(ILGenerator gen, KeyValuePair<MemberInfo, FieldBuilder> dynamicEvent, OpCode loadInstanceTypeOpCode)
        {
            ConstructorInfo dynamicEventdCtor = typeof(DynamicEvent).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(EventInfo), typeof(object) }, null);
            MethodInfo typeGetEvent = typeof(Type).GetMethod("GetEvent", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string), typeof(BindingFlags) }, null);

            InitializeField(dynamicEvent, gen, typeGetEvent, dynamicEventdCtor, loadInstanceTypeOpCode);
        }

        private static void InitializeDynamicIndex(ILGenerator gen, FieldBuilder dynamicIndexField)
        {
            if (dynamicIndexField == null)
                return;

            ConstructorInfo dynamicIndexCtor = typeof(DynamicIndexer).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, typeof(object).ToArrayItem<Type>(), null);

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Newobj, dynamicIndexCtor);
            gen.Emit(OpCodes.Stfld, dynamicIndexField);
        }


        private static void InitializeDynamicMethod(ILGenerator gen, KeyValuePair<MemberInfo, FieldBuilder> dynamicMethod, OpCode loadInstanceTypeOpCode)
        {
            ConstructorInfo dynamicMethodCtor = typeof(DynamicMethod).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(MethodInfo), typeof(object) }, null);
            MethodInfo typeGetMethod = typeof(Type).GetMethod("GetMethod", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string), typeof(BindingFlags) }, null);

            InitializeField(dynamicMethod, gen, typeGetMethod, dynamicMethodCtor, loadInstanceTypeOpCode);
        }

        private static void InitializeDynamicProperty(ILGenerator gen, KeyValuePair<MemberInfo, FieldBuilder> dynamicProperty, OpCode loadInstanceTypeOpCode)
        {
            MethodInfo typeGetProperty = typeof(Type).GetMethod("GetProperty", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string), typeof(BindingFlags) }, null);
            ConstructorInfo dynamicPropertyCtor = typeof(DynamicProperty).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(PropertyInfo), typeof(object) }, null);

            InitializeField(dynamicProperty, gen, typeGetProperty, dynamicPropertyCtor, loadInstanceTypeOpCode);
        }

        private static void InitializeField(KeyValuePair<MemberInfo, FieldBuilder> dynamicAccessor, ILGenerator gen, MethodInfo typeGetMemberMethod, ConstructorInfo dynamicAccessorCtor, OpCode loadInstanceTypeOpCode)
        {
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(loadInstanceTypeOpCode);
            gen.Emit(OpCodes.Ldstr, dynamicAccessor.Key.Name);
            gen.Emit(OpCodes.Ldc_I4_S, (int)(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            gen.Emit(OpCodes.Callvirt, typeGetMemberMethod);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Newobj, dynamicAccessorCtor);
            gen.Emit(OpCodes.Stfld, dynamicAccessor.Value);
        }

        private static FieldBuilder BuildPrivateReadonlyField(TypeBuilder typeBuilder, string name, Type fieldType)
        {
            FieldBuilder field = typeBuilder.DefineField(name, fieldType, FieldAttributes.Private | FieldAttributes.InitOnly);
            return field;
        }

        private static void BuildProperty(TypeBuilder typeBuilder, PropertyInfo propertyInfo, FieldBuilder dynamicAccessorField)
        {
            var parameterTypes = propertyInfo.GetIndexParameters().Select(pi => pi.ParameterType).ToArray();
            var property = typeBuilder.DefineProperty(propertyInfo.Name, propertyInfo.Attributes, propertyInfo.PropertyType, parameterTypes);

            if (propertyInfo.CanRead)
            {
                var methodBuilder = parameterTypes.Length == 0
                                        ? BuildGetter(typeBuilder, propertyInfo, dynamicAccessorField)
                                        : BuildIndexerGetter(typeBuilder, propertyInfo, dynamicAccessorField);

                property.SetGetMethod(methodBuilder);
            }

            if (propertyInfo.CanWrite)
            {
                var methodBuilder = parameterTypes.Length == 0
                                        ? BuildSetter(typeBuilder, propertyInfo, dynamicAccessorField)
                                        : BuildIndexerSetter(typeBuilder, propertyInfo, dynamicAccessorField);

                property.SetSetMethod(methodBuilder);
            }
        }

        private static MethodBuilder BuildGetter(TypeBuilder typeBuilder, PropertyInfo propertyInfo, FieldBuilder dynamicPropertyField)
        {
            var indexParameterTypes = propertyInfo.GetIndexParameters().Select(pi => pi.ParameterType).ToArray();
            MethodBuilder method = typeBuilder.DefineMethod(propertyInfo.GetGetMethod().Name, PropertyAccessorMethodAttributes, propertyInfo.PropertyType, indexParameterTypes);

            MethodInfo dynamicPropertyGet = typeof(DynamicProperty).GetMethod("Get", BindingFlags.Instance | BindingFlags.Public, null, typeof(object[]).ToArrayItem<Type>(), null);

            method.SetReturnType(propertyInfo.PropertyType);

            ILGenerator gen = method.GetILGenerator();

            EmitPopulateObjectArrayArgumentAndCallMethod(propertyInfo.PropertyType, propertyInfo.GetIndexParameters(), gen, dynamicPropertyField, dynamicPropertyGet);

            return method;
        }

        private static MethodBuilder BuildSetter(TypeBuilder typeBuilder, PropertyInfo propertyInfo, FieldBuilder dynamicPropertyField)
        {
            MethodBuilder method = typeBuilder.DefineMethod(propertyInfo.GetSetMethod().Name, PropertyAccessorMethodAttributes);

            MethodInfo dynamicPropertySet = typeof(DynamicProperty).GetMethod("Set", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(object[]), typeof(object) }, null);

            method.SetReturnType(typeof(void));

            method.SetParameters(propertyInfo.PropertyType);

            method.DefineParameter(1, ParameterAttributes.None, "value");
            ILGenerator gen = method.GetILGenerator();

            EmitPopulateObjectArrayArgumentAndCallMethod(propertyInfo.PropertyType, propertyInfo.GetIndexParameters(), gen, dynamicPropertyField, dynamicPropertySet, true);

            return method;
        }

        private static MethodBuilder BuildIndexerSetter(TypeBuilder typeBuilder, PropertyInfo propertyInfo, FieldBuilder dynamicIndexField)
        {
            var indexParameters = propertyInfo.GetIndexParameters();
            MethodBuilder method = typeBuilder.DefineMethod(propertyInfo.GetSetMethod().Name, PropertyAccessorMethodAttributes);

            MethodInfo dynamicIndexSet = typeof(DynamicIndexer).GetMethod("Set", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(object[]), typeof(object) }, null);

            method.SetReturnType(typeof(void));

            var indexParameterTypes = indexParameters.Select(pi => pi.ParameterType).ToList();
            indexParameterTypes.Add(propertyInfo.PropertyType);
            method.SetParameters(indexParameterTypes.ToArray());

            for (int i = 0; i < indexParameters.Length; i++)
            {
                var indexParameter = indexParameters[i];
                method.DefineParameter(i + 1, indexParameter.Attributes, indexParameter.Name);
            }

            method.DefineParameter(indexParameters.Length + 1, ParameterAttributes.None, "value");

            ILGenerator gen = method.GetILGenerator();

            EmitPopulateObjectArrayArgumentAndCallMethod(propertyInfo.PropertyType, indexParameters, gen, dynamicIndexField, dynamicIndexSet, true);

            return method;
        }

        private static MethodBuilder BuildIndexerGetter(TypeBuilder typeBuilder, PropertyInfo propertyInfo, FieldBuilder dynamicIndexField)
        {
            var indexParameters = propertyInfo.GetIndexParameters();
            var indexParameterTypes = indexParameters.Select(pi => pi.ParameterType).ToArray();
            MethodBuilder method = typeBuilder.DefineMethod(propertyInfo.GetGetMethod().Name, PropertyAccessorMethodAttributes, propertyInfo.PropertyType, indexParameterTypes);

            MethodInfo dynamicIndexGet = typeof(DynamicIndexer).GetMethod("Get", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(object[]) }, null);

            for (int i = 0; i < indexParameters.Length; i++)
            {
                var indexParameter = indexParameters[i];
                method.DefineParameter(i + 1, indexParameter.Attributes, indexParameter.Name);
            }

            ILGenerator gen = method.GetILGenerator();

            EmitPopulateObjectArrayArgumentAndCallMethod(propertyInfo.PropertyType, indexParameters, gen, dynamicIndexField, dynamicIndexGet);

            return method;

        }

        private static void EmitPopulateObjectArrayArgumentAndCallMethod(Type returnType, IList<ParameterInfo> parameters, ILGenerator ilGenerator, FieldInfo dynamicAccessorField, MethodInfo dynamicAccessorMethod, bool appendValueParameter = false)
        {
            ilGenerator.DeclareLocal(typeof(object[]));

            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldfld, dynamicAccessorField);
            ilGenerator.Emit(OpCodes.Ldc_I4, parameters.Count());
            ilGenerator.Emit(OpCodes.Newarr, typeof(object));
            ilGenerator.Emit(OpCodes.Stloc_0);

            PopulateLocalObjectArray(ilGenerator, parameters, OpCodes.Ldloc_0);

            ilGenerator.Emit(OpCodes.Ldloc_0);

            if (appendValueParameter)
            {
                ilGenerator.Emit(OpCodes.Ldarg, parameters.Count + 1);
                if (returnType.IsValueType)
                {
                    ilGenerator.Emit(OpCodes.Box, returnType);
                }
                ilGenerator.Emit(OpCodes.Callvirt, dynamicAccessorMethod);
            }
            else
            {
                ilGenerator.Emit(OpCodes.Callvirt, dynamicAccessorMethod);
                //ilGenerator.Emit(OpCodes.Castclass, returnType);

                if (returnType == typeof(void))
                    ilGenerator.Emit(OpCodes.Pop);
                else if (returnType.IsValueType)
                    ilGenerator.Emit(OpCodes.Unbox_Any, returnType);
            }
            ilGenerator.Emit(OpCodes.Ret);
        }

        private static void PopulateLocalObjectArray(ILGenerator ilGenerator, IList<ParameterInfo> parameters, OpCode loadArrayOpCode)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                ilGenerator.Emit(loadArrayOpCode);
                ilGenerator.Emit(OpCodes.Ldc_I4, i);
                ilGenerator.Emit(OpCodes.Ldarg, i + 1);
                if (parameters[i].ParameterType.IsValueType)
                    ilGenerator.Emit(OpCodes.Box, parameters[i].ParameterType);
                ilGenerator.Emit(OpCodes.Stelem_Ref);
            }
        }

        private static void BuildEvent(TypeBuilder typeBuilder, EventInfo eventInfo, FieldBuilder dynamicEventField)
        {
            EventBuilder eventBuilder = typeBuilder.DefineEvent(eventInfo.Name, EventAttributes.None, eventInfo.EventHandlerType);

            eventBuilder.SetAddOnMethod(DefineEventAdder(typeBuilder, eventInfo, dynamicEventField));
            eventBuilder.SetRemoveOnMethod(DefineEventRemover(typeBuilder, eventInfo, dynamicEventField));
        }

        private static MethodBuilder DefineEventAdder(TypeBuilder typeBuilder, EventInfo eventInfo, FieldBuilder dynamicEventField)
        {
            MethodBuilder addMethod = typeBuilder.DefineMethod(eventInfo.GetAddMethod(true).Name, PropertyAccessorMethodAttributes, typeof(void), eventInfo.EventHandlerType.ToArrayItem<Type>());
            MethodInfo dynamicEventAdd = typeof(DynamicEvent).GetMethod("Add", BindingFlags.Instance | BindingFlags.Public, null, typeof(object).ToArrayItem<Type>(), null);

            addMethod.DefineParameter(1, ParameterAttributes.None, "value");

            ILGenerator gen = addMethod.GetILGenerator();

            BuildCallVoidWithOneParameter(gen, dynamicEventField, dynamicEventAdd);

            return addMethod;
        }

        private static MethodBuilder DefineEventRemover(TypeBuilder typeBuilder, EventInfo eventInfo, FieldBuilder dynamicEventField)
        {
            MethodBuilder removeMethod = typeBuilder.DefineMethod(eventInfo.GetRemoveMethod(true).Name, PropertyAccessorMethodAttributes, typeof(void), eventInfo.EventHandlerType.ToArrayItem<Type>());
            MethodInfo dynamicEventRemove = typeof(DynamicEvent).GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public, null, typeof(object).ToArrayItem<Type>(), null);

            removeMethod.DefineParameter(1, ParameterAttributes.None, "value");

            ILGenerator gen = removeMethod.GetILGenerator();

            BuildCallVoidWithOneParameter(gen, dynamicEventField, dynamicEventRemove);

            return removeMethod;
        }

        private static void BuildCallVoidWithOneParameter(ILGenerator gen, FieldBuilder dynamicEventField, MethodInfo dynamicEventAdd)
        {
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, dynamicEventField);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Castclass, typeof(object));
            gen.Emit(OpCodes.Callvirt, dynamicEventAdd);
            gen.Emit(OpCodes.Ret);
        }


        private static void BuildMethod(TypeBuilder typeBuilder, MethodInfo methodToWrap, FieldInfo dynamicMethodField)
        {
            MethodBuilder method = typeBuilder.DefineMethod(methodToWrap.Name, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual);
            MethodInfo dynamicMethodInvoke = typeof(DynamicMethod).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public, null, typeof(object[]).ToArrayItem<Type>(), null);

            method.SetReturnType(methodToWrap.ReturnType);

            var parameters = methodToWrap.GetParameters();

            method.SetParameters(parameters.Select(p => p.ParameterType).ToArray());

            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                method.DefineParameter(i + 1, parameter.Attributes, parameter.Name);
            }

            ILGenerator gen = method.GetILGenerator();

            EmitPopulateObjectArrayArgumentAndCallMethod(method.ReturnType, parameters, gen, dynamicMethodField, dynamicMethodInvoke);
        }

        private static TypeBuilder GetTypeBuilder(Type interfaceToImplement)
        {
            var typeBuilder = TypeBuilders.ContainsKey(interfaceToImplement)
                                  ? TypeBuilders[interfaceToImplement]
                                  : null;

            if (typeBuilder == null)
            {
                typeBuilder = CreateType(_modBuilder, "DynamicProxy.DynamicInterface_" + interfaceToImplement.Name, interfaceToImplement.ToArrayItem<Type>());
                TypeBuilders.Add(interfaceToImplement, typeBuilder);

                var dynamicMembersContainer = new Dictionary<MemberInfo, FieldBuilder>();

                GetDynamicProperties(interfaceToImplement, typeBuilder, dynamicMembersContainer);
                GetDynamicMethods(interfaceToImplement, typeBuilder, dynamicMembersContainer);
                BuildDynamicEvents(interfaceToImplement, typeBuilder, dynamicMembersContainer);

                if (interfaceToImplement.GetProperties().Any(pi => pi.GetIndexParameters().Count() > 0))
                {
                    var dynamicIndexField = BuildPrivateReadonlyField(typeBuilder, "_indexer", typeof(DynamicIndexer));
                    BuildDynamicIndexers(interfaceToImplement, typeBuilder, dynamicIndexField);
                    BuildConstructor(typeBuilder, dynamicMembersContainer, dynamicIndexField);
                }
                else
                {
                    BuildConstructor(typeBuilder, dynamicMembersContainer);
                }
            }
            return typeBuilder;
        }

        private static void BuildDynamicEvents(Type interfaceToImplement, TypeBuilder typeBuilder, Dictionary<MemberInfo, FieldBuilder> dynamicMembersContainer)
        {
            foreach (var eventInfo in interfaceToImplement.GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                FieldBuilder dynamicEvent = BuildPrivateReadonlyField(typeBuilder, "_" + eventInfo.Name, typeof(DynamicEvent));
                dynamicMembersContainer.Add(eventInfo, dynamicEvent);
                BuildEvent(typeBuilder, eventInfo, dynamicEvent);
            }
        }

        private static void BuildDynamicIndexers(Type interfaceToImplement, TypeBuilder typeBuilder, FieldBuilder dynamicIndexField)
        {
            foreach (var indexedProperty in interfaceToImplement.GetProperties().Where(pi => pi.GetIndexParameters().Count() > 0))
            {
                BuildProperty(typeBuilder, indexedProperty, dynamicIndexField);
            }
        }

        private static void GetDynamicMethods(Type interfaceToImplement, TypeBuilder typeBuilder, Dictionary<MemberInfo, FieldBuilder> dynamicMethods)
        {
            foreach (var method in interfaceToImplement.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(mi => !mi.IsSpecialName))
            {
                FieldBuilder dynamicMethod = BuildPrivateReadonlyField(typeBuilder, "_" + method.GetMethodNameWithTypes(), typeof(DynamicMethod));
                dynamicMethods.Add(method, dynamicMethod);
                BuildMethod(typeBuilder, method, dynamicMethod);
            }
        }

        private static void GetDynamicProperties(Type interfaceToImplement, TypeBuilder typeBuilder, Dictionary<MemberInfo, FieldBuilder> dynamicProperties)
        {
            foreach (var property in interfaceToImplement.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(pi => pi.GetIndexParameters().Count() == 0))
            {
                FieldBuilder dynamicProperty = BuildPrivateReadonlyField(typeBuilder, "_" + property.Name, typeof(DynamicProperty));
                dynamicProperties.Add(property, dynamicProperty);
                BuildProperty(typeBuilder, property, dynamicProperty);
            }
        }

        public static T CreateDynamicInterface<T>(object instance)
        {
            return (T)CreateDynamicInterface(typeof(T), instance);
        }

        public static object CreateDynamicInterface(Type interfaceToImplement, object instance)
        {
            Type proxyType = GetTypeBuilder(interfaceToImplement).CreateType();

            var instanceType = instance.GetType();
            if (FullfillsInterfaceDefinitions(interfaceToImplement, instanceType))
            {
                return proxyType.GetConstructor(typeof(object).ToArrayItem<Type>()).Invoke(instance.ToArrayItem<object>());
            }

            throw new RuntimeBinderException(String.Format("Cannot convert type {0} to {1}.", instanceType.FullName, interfaceToImplement.FullName));
        }

        private static bool FullfillsInterfaceDefinitions(Type interfaceType, Type instanceType)
        {
            if (!interfaceType.IsInterface)
            {
                return false;
            }

            return FulfillsInterfaceMethodDefinitions(interfaceType, instanceType)
                   && FulfillsInterfacePropertyAndIndexerDefinitions(interfaceType, instanceType)
                   && FulfillsInterfaceEventDefinitions(interfaceType, instanceType);
        }

        private static bool FulfillsInterfaceEventDefinitions(Type interfaceType, Type instanceType)
        {
            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var interfaceEventAccessors = interfaceType.GetEvents(bindingFlags).SelectMany(e => new[] { e.GetAddMethod(true), e.GetRemoveMethod(true) }).Select(m => m.GetMethodNameWithTypes());
            var instanceEventAccessors = instanceType.GetEvents(bindingFlags).SelectMany(e => new[] { e.GetAddMethod(true), e.GetRemoveMethod(true) }).Select(m => m.GetMethodNameWithTypes());

            var eventIntersection = interfaceEventAccessors.Intersect(instanceEventAccessors);

            return eventIntersection.Count() == interfaceEventAccessors.Count();
        }

        private static bool FulfillsInterfacePropertyAndIndexerDefinitions(IReflect interfaceType, IReflect instanceType)
        {
            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var interfacePropertyAccessors = interfaceType.GetProperties(bindingFlags).SelectMany(p => p.GetAccessors(true)).Select(m => m.GetMethodNameWithTypes());
            var instancePropertyAccessors = instanceType.GetProperties(bindingFlags).SelectMany(p => p.GetAccessors(true)).Select(m => m.GetMethodNameWithTypes());

            var propertyIntersection = interfacePropertyAccessors.Intersect(instancePropertyAccessors);

            return propertyIntersection.Count() == interfacePropertyAccessors.Count();
        }

        private static bool FulfillsInterfaceMethodDefinitions(IReflect interfaceType, IReflect instanceType)
        {
            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var interfaceMethods = interfaceType.GetMethods(bindingFlags).Where(m => !m.IsSpecialName).Select(m => m.GetMethodNameWithTypes());
            var instanceMethods = instanceType.GetMethods(bindingFlags).Where(m => !m.IsSpecialName).Select(m => m.GetMethodNameWithTypes());
            var methodIntersect = interfaceMethods.Intersect(instanceMethods);

            return interfaceMethods.Count() == methodIntersect.Count();
        }
    }
}