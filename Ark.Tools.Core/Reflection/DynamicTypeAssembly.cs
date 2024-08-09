// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

namespace Ark.Tools.Core.Reflection
{
    /// <summary>
    /// Generates new Types with dynamically added properties.
    /// </summary>
    public class DynamicTypeAssembly
    {
        private readonly AssemblyBuilder _assemblyBuilder;
        private readonly ModuleBuilder _moduleBuilder;


        /// <summary>
        /// Constructor.
        /// </summary>
        public DynamicTypeAssembly()
        {
            var uniqueIdentifier = Guid.NewGuid().ToString();
            var assemblyName = new AssemblyName(uniqueIdentifier);

            _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect);
            _moduleBuilder = _assemblyBuilder.DefineDynamicModule(uniqueIdentifier);
        }


        /// <summary>
        /// Creates a new Type based on the specified parent Type and attaches dynamic properties.
        /// </summary>
        /// <param name="parentType">The parent Type to base the new Type on</param>
        /// <param name="dynamicProperties">The collection of dynamic properties to attach to the new Type</param>
        /// <returns>An extended Type with dynamic properties added to it</returns>
        public Type CreateNewTypeWithDynamicProperties(Type parentType, IEnumerable<(string name, Type type)> dynamicProperties)
        {
            var typeBuilder = _moduleBuilder.DefineType(parentType.Name + Guid.NewGuid().ToString(), TypeAttributes.Public);
            typeBuilder.SetParent(parentType);

            var constructors = parentType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            // Loop through each constructor
            foreach (var constructor in constructors)
            {
                // Get all of the parameters from the constructor
                var parameters = constructor.GetParameters();

                // Get all of the types from parameters
                var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

                // Build the new constructor on the new type we are creating
                var newConstructor = typeBuilder.DefineConstructor(MethodAttributes.Public, constructor.CallingConvention, parameterTypes);

                // Loop through each parameter in the constructor
                for (var i = 0; i < parameters.Length; ++i)
                {
                    var parameter = parameters[i];
                    // Define the parameter
                    var parameterBuilder = newConstructor.DefineParameter(i + 1, parameter.Attributes, parameter.Name);
                }

                // Get the IL generator from the new constructor we defined earlier
                var emitter = newConstructor.GetILGenerator();
                emitter.Emit(OpCodes.Nop);

                // Load `this` and call base constructor with arguments
                emitter.Emit(OpCodes.Ldarg_0);
                for (var i = 1; i <= parameters.Length; ++i)
                {
                    emitter.Emit(OpCodes.Ldarg, i);
                }
                emitter.Emit(OpCodes.Call, constructor);

                emitter.Emit(OpCodes.Ret);
            }

            foreach (var (name, type) in dynamicProperties)
                _addDynamicPropertyToType(typeBuilder, name, type);

            return typeBuilder.CreateType() ?? throw new InvalidOperationException("CreateType() retuned null");
        }

        private static void _addDynamicPropertyToType(TypeBuilder typeBuilder, string propertyName, Type propertyType)
        {
            string fieldName = $"_{propertyName}";

            FieldBuilder fieldBuilder = typeBuilder.DefineField(fieldName, propertyType, FieldAttributes.Private);

            // The property set and get methods require a special set of attributes.
            MethodAttributes getSetAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

            // Define the 'get' accessor method.
            MethodBuilder getMethodBuilder = typeBuilder.DefineMethod($"get_{propertyName}", getSetAttributes, propertyType, Type.EmptyTypes);
            ILGenerator propertyGetGenerator = getMethodBuilder.GetILGenerator();
            propertyGetGenerator.Emit(OpCodes.Ldarg_0);
            propertyGetGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            propertyGetGenerator.Emit(OpCodes.Ret);

            // Define the 'set' accessor method.
            MethodBuilder setMethodBuilder = typeBuilder.DefineMethod($"set_{propertyName}", getSetAttributes, null, new Type[] { propertyType });
            ILGenerator propertySetGenerator = setMethodBuilder.GetILGenerator();
            propertySetGenerator.Emit(OpCodes.Ldarg_0);
            propertySetGenerator.Emit(OpCodes.Ldarg_1);
            propertySetGenerator.Emit(OpCodes.Stfld, fieldBuilder);
            propertySetGenerator.Emit(OpCodes.Ret);

            // Lastly, we must map the two methods created above to a PropertyBuilder and their corresponding behaviors, 'get' and 'set' respectively.
            PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
            propertyBuilder.SetGetMethod(getMethodBuilder);
            propertyBuilder.SetSetMethod(setMethodBuilder);
        }
    }
}
