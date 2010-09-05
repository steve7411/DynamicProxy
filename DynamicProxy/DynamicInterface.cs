using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace DynamicProxy
{
    public static class DynamicInterface
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

        private static void BuildConstructor(TypeBuilder typeBuilder, Dictionary<string, FieldBuilder> dynamicProperties, Dictionary<string, FieldBuilder> dynamicMethods, FieldBuilder dynamicIndexField = null)
        {
            const MethodAttributes methodAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            ConstructorBuilder constructor = typeBuilder.DefineConstructor(methodAttributes, CallingConventions.Standard, typeof(object).ToArrayItem<Type>());

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

            InitializeDynamicProperties(gen, dynamicProperties, OpCodes.Ldloc_0);

            InitializeDynamicMethods(gen, dynamicMethods, OpCodes.Ldloc_0);

            InitializeDynamicIndex(gen, dynamicIndexField);

            gen.Emit(OpCodes.Ret);
        }

        private static void InitializeDynamicIndex(ILGenerator gen, FieldBuilder dynamicIndexField)
        {
            if (dynamicIndexField == null)
                return;

            ConstructorInfo dynamicIndexCtor = typeof(DynamicIndex).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, typeof(object).ToArrayItem<Type>(), null);

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Newobj, dynamicIndexCtor);
            gen.Emit(OpCodes.Stfld, dynamicIndexField);
        }


        private static void InitializeDynamicMethods(ILGenerator gen, Dictionary<string, FieldBuilder> dynamicMethods, OpCode loadInstanceTypeOpCode)
        {
            ConstructorInfo dynamicMethodCtor = typeof(DynamicMethod).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(MethodInfo), typeof(object) }, null);
            MethodInfo typeGetMethod = typeof(Type).GetMethod("GetMethod", BindingFlags.Instance | BindingFlags.Public, null, typeof(string).ToArrayItem<Type>(), null);

            InitializeFields(dynamicMethods, gen, typeGetMethod, dynamicMethodCtor, loadInstanceTypeOpCode);
        }

        private static void InitializeDynamicProperties(ILGenerator gen, Dictionary<string, FieldBuilder> dynamicProperties, OpCode loadInstanceTypeOpCode)
        {
            MethodInfo typeGetProperty = typeof(Type).GetMethod("GetProperty", BindingFlags.Instance | BindingFlags.Public, null, typeof(string).ToArrayItem<Type>(), null);
            ConstructorInfo dynamicPropertyCtor = typeof(DynamicProperty).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(PropertyInfo), typeof(object) }, null);

            InitializeFields(dynamicProperties, gen, typeGetProperty, dynamicPropertyCtor, loadInstanceTypeOpCode);
        }

        private static void InitializeFields(Dictionary<string, FieldBuilder> dynamicAccessors, ILGenerator gen, MethodInfo typeGetMemberMethod, ConstructorInfo dynamicAccessorCtor, OpCode loadInstanceTypeOpCode)
        {
            foreach (var propertyName in dynamicAccessors.Keys)
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(loadInstanceTypeOpCode);
                gen.Emit(OpCodes.Ldstr, propertyName);
                gen.Emit(OpCodes.Callvirt, typeGetMemberMethod);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Newobj, dynamicAccessorCtor);
                gen.Emit(OpCodes.Stfld, dynamicAccessors[propertyName]);
            }
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

        private static MethodBuilder BuildIndexerSetter(TypeBuilder typeBuilder, PropertyInfo propertyInfo, FieldBuilder dynamicIndexField)
        {
            var indexParameters = propertyInfo.GetIndexParameters();
            MethodBuilder method = typeBuilder.DefineMethod(propertyInfo.GetSetMethod().Name, PropertyAccessorMethodAttributes);

            MethodInfo dynamicIndexSet = typeof(DynamicIndex).GetMethod("Set", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(object[]), typeof(object) }, null);

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

            MethodInfo dynamicIndexGet = typeof(DynamicIndex).GetMethod("Get", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(object[]) }, null);

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
                ilGenerator.Emit(OpCodes.Castclass, returnType);

                if (returnType.IsValueType)
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

        private static MethodBuilder BuildGetter(TypeBuilder typeBuilder, PropertyInfo propertyInfo, FieldBuilder dynamicPropertyField)
        {
            var indexParameterTypes = propertyInfo.GetIndexParameters().Select(pi => pi.ParameterType).ToArray();
            MethodBuilder method = typeBuilder.DefineMethod(propertyInfo.GetGetMethod().Name, PropertyAccessorMethodAttributes, propertyInfo.PropertyType, indexParameterTypes);

            MethodInfo dynamicPropertyGet = typeof(DynamicProperty).GetMethod("Get", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

            method.SetReturnType(propertyInfo.PropertyType);

            ILGenerator gen = method.GetILGenerator();

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, dynamicPropertyField);
            gen.Emit(OpCodes.Callvirt, dynamicPropertyGet);
            gen.Emit(OpCodes.Castclass, propertyInfo.PropertyType);

            if (propertyInfo.PropertyType.IsValueType)
            {
                gen.Emit(OpCodes.Unbox_Any, propertyInfo.PropertyType);
            }
            gen.Emit(OpCodes.Ret);

            return method;
        }

        private static MethodBuilder BuildSetter(TypeBuilder typeBuilder, PropertyInfo propertyInfo, FieldBuilder dynamicPropertyField)
        {
            MethodBuilder method = typeBuilder.DefineMethod(propertyInfo.GetSetMethod().Name, PropertyAccessorMethodAttributes);

            MethodInfo dynamicPropertySet = typeof(DynamicProperty).GetMethod("Set", BindingFlags.Instance | BindingFlags.Public, null, typeof(object).ToArrayItem<Type>(), null);

            method.SetReturnType(typeof(void));

            method.SetParameters(propertyInfo.PropertyType);

            method.DefineParameter(1, ParameterAttributes.None, "value");
            ILGenerator gen = method.GetILGenerator();

            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Ldfld, dynamicPropertyField);
            gen.Emit(OpCodes.Ldarg_1);

            if (propertyInfo.PropertyType.IsValueType)
                gen.Emit(OpCodes.Box, propertyInfo.PropertyType);

            gen.Emit(OpCodes.Callvirt, dynamicPropertySet);
            gen.Emit(OpCodes.Ret);

            return method;
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

                Dictionary<string, FieldBuilder> dynamicProperties = GetDynamicProperties(interfaceToImplement, typeBuilder);

                Dictionary<string, FieldBuilder> dynamicMethods = GetDynamicMethods(interfaceToImplement, typeBuilder);

                if (interfaceToImplement.GetProperties().Any(pi => pi.GetIndexParameters().Count() > 0))
                {
                    var dynamicIndexField = BuildPrivateReadonlyField(typeBuilder, "_indexer", typeof(DynamicIndex));
                    BuildDynamicIndexers(interfaceToImplement, typeBuilder, dynamicIndexField);
                    BuildConstructor(typeBuilder, dynamicProperties, dynamicMethods, dynamicIndexField);
                }
                else
                {
                    BuildConstructor(typeBuilder, dynamicProperties, dynamicMethods);
                }
            }
            return typeBuilder;
        }

        private static void BuildDynamicIndexers(Type interfaceToImplement, TypeBuilder typeBuilder, FieldBuilder dynamicIndexField)
        {
            foreach (var indexedProperty in interfaceToImplement.GetProperties().Where(pi => pi.GetIndexParameters().Count() > 0))
            {
                BuildProperty(typeBuilder, indexedProperty, dynamicIndexField);
            }
        }

        private static Dictionary<string, FieldBuilder> GetDynamicMethods(Type interfaceToImplement, TypeBuilder typeBuilder)
        {
            var dynamicMethods = new Dictionary<string, FieldBuilder>();
            foreach (var method in interfaceToImplement.GetMethods().Where(mi => !mi.IsSpecialName))
            {
                var methodName = GetMethodName(method);
                FieldBuilder dynamicMethod = BuildPrivateReadonlyField(typeBuilder, "_" + methodName, typeof(DynamicMethod));
                dynamicMethods.Add(method.Name, dynamicMethod);
                BuildMethod(typeBuilder, method, dynamicMethod);
            }
            return dynamicMethods;
        }

        private static Dictionary<string, FieldBuilder> GetDynamicProperties(Type interfaceToImplement, TypeBuilder typeBuilder)
        {
            var dynamicProperties = new Dictionary<string, FieldBuilder>();
            foreach (var property in interfaceToImplement.GetProperties().Where(pi => pi.GetIndexParameters().Count() == 0))
            {
                FieldBuilder dynamicProperty = BuildPrivateReadonlyField(typeBuilder, "_" + property.Name, typeof(DynamicProperty));
                dynamicProperties.Add(property.Name, dynamicProperty);
                BuildProperty(typeBuilder, property, dynamicProperty);
            }
            return dynamicProperties;
        }

        private static string GetMethodName(MethodInfo method)
        {
            var fullName = method.GetParameters().Aggregate(method.Name, (pname, pi) => pname + pi.ParameterType.Name);
            return fullName;
        }

        public static T CreateDynamicInterface<T>(object instance)
        {
            Type proxyType = GetTypeBuilder(typeof(T)).CreateType();

            return (T)proxyType.GetConstructor(typeof(object).ToArrayItem<Type>()).Invoke(instance.ToArrayItem<object>());
        }

        public static object CreateDynamicInterface(Type interfaceToImplement, object instance)
        {
            Type proxyType = GetTypeBuilder(interfaceToImplement).CreateType();

            return proxyType.GetConstructor(typeof(object).ToArrayItem<Type>()).Invoke(instance.ToArrayItem<object>());
        }
    }
}