using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrecompiledRegex.Fody
{
    /// <summary>
    /// Implements a limited set of ILMerge-like capabilities needed to move the compiled regex types into the
    /// consuming assembly
    /// </summary>
    internal class TypeMerger
    {
        private readonly ModuleDefinition from, to;
        private readonly Dictionary<TypeDefinition, TypeDefinition> mappedTypes = new Dictionary<TypeDefinition, TypeDefinition>();
        private readonly Dictionary<FieldDefinition, FieldDefinition> mappedFields = new Dictionary<FieldDefinition, FieldDefinition>();
        private readonly Dictionary<MethodDefinition, MethodDefinition> mappedMethods = new Dictionary<MethodDefinition, MethodDefinition>();

        private TypeMerger(ModuleDefinition from, ModuleDefinition to)
        {
            this.from = from;
            this.to = to;
        }

        public static void MergeTypes(ModuleDefinition from, ModuleDefinition to)
        {
            var merger = new TypeMerger(from, to);
            merger.MapTypes();
            merger.PopulateMembers();
        }

        #region ---- Mappings ----
        private void MapTypes()
        {
            foreach (var type in this.from.Types)
            {
                if (type.BaseType == null)
                {
                    // avoid copying the weird <Module> type
                    // see http://stackoverflow.com/questions/35617201/what-is-the-module-type
                    continue;
                }

                this.MapType(type);
                this.to.Types.Add(this.mappedTypes[type]);
            }
        }

        private void MapType(TypeDefinition fromType)
        {
            if (fromType.HasEvents)
            {
                throw new NotSupportedException("Events");
            }
            if (fromType.HasProperties)
            {
                throw new NotSupportedException("Properties");
            }
            if (fromType.HasNestedTypes)
            {
                throw new NotSupportedException("NestedTypes");
            }

            var toType = new TypeDefinition(fromType.Namespace, fromType.Name, fromType.Attributes); 

            foreach (var nestedType in fromType.NestedTypes)
            {
                this.MapType(nestedType);
                toType.NestedTypes.Add(this.mappedTypes[nestedType]);
            }

            foreach (var field in fromType.Fields)
            {
                this.MapField(field);
                toType.Fields.Add(this.mappedFields[field]);
            }

            foreach (var method in fromType.Methods)
            {
                this.MapMethod(method);
                toType.Methods.Add(this.mappedMethods[method]);
            }
            
            this.mappedTypes.Add(fromType, toType);
        }

        private void MapField(FieldDefinition fromField)
        {
            this.mappedFields.Add(fromField, new FieldDefinition(fromField.Name, fromField.Attributes, fromField.FieldType));
        }

        private void MapMethod(MethodDefinition fromMethod)
        {
            this.mappedMethods.Add(fromMethod, new MethodDefinition(fromMethod.Name, fromMethod.Attributes, fromMethod.ReturnType));
        }
        #endregion

        private void PopulateMembers()
        {
            foreach (var kvp in this.mappedTypes)
            {
                this.PopulateType(kvp.Key, kvp.Value);
            }

            foreach (var kvp in this.mappedFields)
            {
                this.PopulateField(kvp.Key, kvp.Value);
            }

            foreach (var kvp in this.mappedMethods)
            {
                this.PopulateMethod(kvp.Key, kvp.Value);
            }
        }

        private void PopulateType(TypeDefinition fromType, TypeDefinition toType)
        {
            if (fromType.HasGenericParameters)
            {
                throw new NotSupportedException("Generics");
            }

            toType.BaseType = this.Resolve(fromType.BaseType);
            foreach (var @interface in fromType.Interfaces)
            {
                toType.Interfaces.Add(this.Resolve(@interface));
            }

            this.PopulateCustomAttributes(fromType.CustomAttributes, toType.CustomAttributes);
        }

        private void PopulateField(FieldDefinition fromField, FieldDefinition toField)
        {
            toField.FieldType = this.Resolve(fromField.FieldType);
        }

        private void PopulateMethod(MethodDefinition fromMethod, MethodDefinition toMethod)
        {
            if (fromMethod.HasPInvokeInfo)
            {
                throw new NotSupportedException("PInvoke");
            }
            if (fromMethod.HasGenericParameters)
            {
                throw new NotSupportedException("Generics");
            }
            if (fromMethod.HasSecurityDeclarations)
            {
                throw new NotSupportedException("SecurityDeclarations");
            }

            toMethod.ReturnType = this.Resolve(fromMethod.ReturnType);
            toMethod.CallingConvention = fromMethod.CallingConvention;
            foreach (var parameter in fromMethod.Parameters)
            {
                toMethod.Parameters.Add(this.CopyParameter(parameter));   
            }

            foreach (var @override in fromMethod.Overrides)
            {
                toMethod.Overrides.Add(this.Resolve(@override));
            }

            this.PopulateCustomAttributes(fromMethod.CustomAttributes, toMethod.CustomAttributes);

            if (fromMethod.HasBody)
            {
                toMethod.Body.InitLocals = fromMethod.Body.InitLocals;
                toMethod.Body.IteratorType = this.Resolve(fromMethod.Body.IteratorType)?.Resolve();
                foreach (var fromVariable in fromMethod.Body.Variables)
                {
                    toMethod.Body.Variables.Add(new VariableDefinition(fromVariable.Name, this.Resolve(fromVariable.VariableType)));
                }

                // copy instructions
                var fromInstructions = fromMethod.Body.Instructions;
                var toInstructions = toMethod.Body.Instructions;
                // first, fill up with empty instructions. We'll populate later
                for (var i = 0; i < fromInstructions.Count; ++i)
                {
                    toInstructions.Add(Instruction.Create(OpCodes.Nop));
                }

                for (var i = 0; i < fromInstructions.Count; ++i)
                {
                    var fromInstruction = fromInstructions[i];
                    var toInstruction = toInstructions[i];

                    toInstruction.OpCode = fromInstruction.OpCode;
                    var fromOperand = fromInstruction.Operand;

                    object toOperand;

                    // possible operand types determined from Instruction.Create() overloads
                    Instruction instructionOperand;
                    IEnumerable<Instruction> instructionsOperand;
                    MethodReference methodOperand;
                    FieldReference fieldOperand;
                    TypeReference typeOperand;
                    ParameterDefinition parameterOperand;
                    VariableDefinition variableOperand;
                    if (fromOperand == null)
                    {
                        toOperand = null;
                    }
                    else if ((instructionOperand = fromOperand as Instruction) != null)
                    {
                        toOperand = MapInstruction(instructionOperand, fromMethod.Body, toMethod.Body);
                    }
                    else if ((instructionsOperand = fromOperand as IEnumerable<Instruction>) != null)
                    {
                        toOperand = instructionsOperand.Select(fromTarget => MapInstruction(fromTarget, fromMethod.Body, toMethod.Body))
                            .ToArray();
                    }
                    else if ((methodOperand = fromOperand as MethodReference) != null)
                    {
                        toOperand = this.Resolve(methodOperand);
                    }
                    else if ((fieldOperand = fromOperand as FieldReference) != null)
                    {
                        toOperand = this.Resolve(fieldOperand);
                    }
                    else if ((typeOperand = fromOperand as TypeReference) != null)
                    {
                        toOperand = this.Resolve(typeOperand);
                    }
                    else if ((parameterOperand = fromOperand as ParameterDefinition) != null)
                    {
                        var index = fromMethod.Parameters.IndexOf(parameterOperand);
                        if (index < 0) { throw new InvalidOperationException("Invalid parameter reference"); }
                        toOperand = toMethod.Parameters[index];
                    }
                    else if ((variableOperand = fromOperand as VariableDefinition) != null)
                    {
                        var index = fromMethod.Body.Variables.IndexOf(variableOperand);
                        if (index < 0) { throw new InvalidOperationException("Invalid parameter reference"); }
                        toOperand = toMethod.Body.Variables[index];
                    }
                    else if (fromOperand is CallSite)
                    {
                        throw new NotSupportedException($"{fromInstruction.OpCode.Code} with {nameof(CallSite)} operand");
                    }
                    else if (fromOperand is int || fromOperand is long || fromOperand is double || fromOperand is string || fromOperand is byte || fromOperand is sbyte)
                    {
                        toOperand = fromOperand;
                    }
                    else
                    {
                        throw new NotSupportedException($"Unexpected operand type {fromOperand.GetType()} for op code {fromInstruction.OpCode.Code}");
                    }

                    toInstruction.Operand = toOperand;
                }

                foreach (var fromIteratorScope in fromMethod.Body.IteratorScopes)
                {
                    toMethod.Body.IteratorScopes.Add(new InstructionRange { Start = fromIteratorScope.Start, End = fromIteratorScope.End });
                }

                foreach (var fromHandler in fromMethod.Body.ExceptionHandlers)
                {
                    toMethod.Body.ExceptionHandlers.Add(new ExceptionHandler(fromHandler.HandlerType)
                    {
                        CatchType = fromHandler.CatchType,
                        FilterStart = MapInstruction(fromHandler.FilterStart, fromMethod.Body, toMethod.Body),
                        HandlerEnd = MapInstruction(fromHandler.HandlerEnd, fromMethod.Body, toMethod.Body),
                        HandlerStart = MapInstruction(fromHandler.HandlerStart, fromMethod.Body, toMethod.Body),
                        TryEnd = MapInstruction(fromHandler.TryEnd, fromMethod.Body, toMethod.Body),
                        TryStart = MapInstruction(fromHandler.TryStart, fromMethod.Body, toMethod.Body),
                    });
                }
            }
        }

        private static Instruction MapInstruction(Instruction from, MethodBody fromBody, MethodBody toBody)
        {
            if (from == null) { return null; }

            var index = fromBody.Instructions.IndexOf(from);
            if (index < 0) { throw new ArgumentException("Referenced instruction does not exist in its method body"); }

            return toBody.Instructions[index];
        }

        private ParameterDefinition CopyParameter(ParameterDefinition fromParameter)
        {
            if (fromParameter.HasMarshalInfo)
            {
                throw new NotSupportedException("MarshalInfo");
            }

            var toParameter = new ParameterDefinition(fromParameter.Name, fromParameter.Attributes, this.Resolve(fromParameter.ParameterType));
            if (fromParameter.HasConstant)
            {
                toParameter.Constant = fromParameter.Constant;
            }
            this.PopulateCustomAttributes(fromParameter.CustomAttributes, toParameter.CustomAttributes);

            return toParameter;
        }

        private void PopulateCustomAttributes(Collection<CustomAttribute> fromAttributes, Collection<CustomAttribute> toAttributes)
        {
            foreach (var fromAttribute in fromAttributes)
            {
                var toAttribute = new CustomAttribute(this.Resolve(fromAttribute.Constructor));
                foreach (var fromArgument in fromAttribute.ConstructorArguments)
                {
                    toAttribute.ConstructorArguments.Add(new CustomAttributeArgument(this.Resolve(fromArgument.Type), fromArgument.Value));
                }
                foreach (var fromField in fromAttribute.Fields)
                {
                    toAttribute.Fields.Add(new CustomAttributeNamedArgument(fromField.Name, new CustomAttributeArgument(this.Resolve(fromField.Argument.Type), fromField.Argument.Value)));
                }
                foreach (var fromProperty in fromAttribute.Properties)
                {
                    toAttribute.Properties.Add(new CustomAttributeNamedArgument(fromProperty.Name, new CustomAttributeArgument(this.Resolve(fromProperty.Argument.Type), fromProperty.Argument.Value)));
                }
                toAttributes.Add(toAttribute);
            }
        }

        private TypeReference Resolve(TypeReference reference)
        {
            if (reference == null) { return null; }

            if (reference.IsArray)
            {
                var arrayReference = (ArrayType)reference;
                return this.Resolve(arrayReference.ElementType).MakeArrayType(arrayReference.Rank);
            }

            var definition = reference.Resolve();
            TypeDefinition mapped;
            if (this.mappedTypes.TryGetValue(definition, out mapped))
            {
                return mapped;
            }

            return this.to.ImportReference(definition);
        }

        private MethodReference Resolve(MethodReference reference)
        {
            if (reference == null) { return null; }

            var definition = reference.Resolve();
            MethodDefinition mapped;
            if (this.mappedMethods.TryGetValue(definition, out mapped))
            {
                return mapped;
            }

            return this.to.ImportReference(definition);
        }

        private FieldReference Resolve(FieldReference reference)
        {
            if (reference == null) { return null; }

            var definition = reference.Resolve();
            FieldDefinition mapped;
            if (this.mappedFields.TryGetValue(definition, out mapped))
            {
                return mapped;
            }

            return this.to.ImportReference(definition);
        }
    }
}
