using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;
using System.Text.RegularExpressions;
using System.IO;

namespace PreCompiledRegex.Fody
{
    internal sealed class TypeProcessor
    {
        private readonly WeavingContext context;
        private readonly RegexReferenceFinder referenceFinder;

        private readonly Dictionary<MethodDefinition, List<RegexDefinition>> replacableRegexDefinitionsByMethod
            = new Dictionary<MethodDefinition, List<RegexDefinition>>();

        public TypeProcessor(WeavingContext context)
        {
            this.context = context;
            this.referenceFinder = new RegexReferenceFinder(context);
        }

        public void PreProcessType(TypeDefinition type)
        {
            if (type.HasMethods)
            {
                foreach (var method in type.Methods)
                {
                    this.PreProcessMethod(method);
                }
            }
        }

        private void PreProcessMethod(MethodDefinition method)
        {
            if (!method.HasBody) { return; }

            List<RegexDefinition> definitions = null;
            var referenceFinder = this.referenceFinder;
            var instructions = method.Body.Instructions;
            for (var i = 0; i < instructions.Count; ++i)
            {
                var instruction = instructions[i];
                var reference = this.referenceFinder.TryGetRegexReference(instruction);
                if (reference != null)
                {
                    (definitions ?? (definitions = new List<RegexDefinition>())).Add(reference.Definition);
                }
            }

            if (definitions != null)
            {
                this.replacableRegexDefinitionsByMethod.Add(method, definitions);
            }
        }

        public void PostProcessAllTypes()
        {
            var regularExpressionsToCompile = this.replacableRegexDefinitionsByMethod
                .SelectMany(kvp => kvp.Value)
                .Distinct()
                .Select((def, index) => new RegexCompilationInfo(
                    pattern: def.Pattern,
                    options: def.Options,
                    name: "PreCompiledRegex" + index,
                    fullnamespace: "PreCompiledRegex.Fody",
                    // TODO could do false + internals visible
                    ispublic: true
                ))
                .ToArray();

            if (!regularExpressionsToCompile.Any()) { return; }

            var tempAssemblyName = new System.Reflection.AssemblyName("PreCompiledRegexTemporaryAssembly_" + Guid.NewGuid().ToString("N"));

            var originalCurrentDirectory = Environment.CurrentDirectory;
            string tempAssemblyPath;
            try
            {
                Environment.CurrentDirectory = Path.GetTempPath();
                Regex.CompileToAssembly(regularExpressionsToCompile, tempAssemblyName);
                tempAssemblyPath = Path.Combine(Path.GetTempPath(), tempAssemblyName.Name + ".dll");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex); // todo
                tempAssemblyPath = null;
            }
            finally
            {
                Environment.CurrentDirectory = originalCurrentDirectory;
            }

            if (tempAssemblyPath == null) { return; }

            var module = this.replacableRegexDefinitionsByMethod.First().Key.Module;

            // create replacement type
            var type = new TypeDefinition("PrecompiledRegex.Fody", "RegularExpressions", TypeAttributes.NotPublic | TypeAttributes.Sealed);
            type.BaseType = module.TypeSystem.Object;
            module.Types.Add(type);

            // TODO assembly as resource
            // based on http://sblakemore.com/blog/post/An-alternative-to-ILMerge-for-WPF.aspx

            //module.Resources.Add(new EmbeddedResource(tempAssemblyName.Name, ManifestResourceAttributes.Private, File.ReadAllBytes(tempAssemblyPath)));
            //module.AssemblyReferences.Add(AssemblyNameReference.Parse(tempAssemblyName.FullName));

            //var staticConstructor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.ImportReference(typeof(void)));

            //staticConstructor.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(Stream))));
            //staticConstructor.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(byte[]))));

            //type.Methods.Add(staticConstructor);
            //var il = staticConstructor.Body.GetILProcessor();
            //// push Assembly.GetExecutingAssembly()
            //il.Emit(OpCodes.Call, module.ImportReference(typeof(System.Reflection.Assembly).GetMethod("GetExecutingAssembly")));
            //// push resource name
            //il.Emit(OpCodes.Ldstr, tempAssemblyName.Name);
            //// var0 = assembly.GetManifestResourceStream(resource name)
            //il.Emit(OpCodes.Callvirt, module.ImportReference(typeof(System.Reflection.Assembly).GetMethod("GetManifestResourceStream", new[] { typeof(string) })));
            //il.Emit(OpCodes.Stloc_0);
            //// var1 = new byte[length]
            //var bytes = checked((int)new FileInfo(tempAssemblyPath).Length);
            //il.Emit(OpCodes.Ldc_I4, bytes);
            //il.Emit(OpCodes.Newarr, module.ImportReference(typeof(byte)));
            //il.Emit(OpCodes.Stloc_1);
            //// push var0
            //il.Emit(OpCodes.Ldloc_0);
            //// push var1
            //il.Emit(OpCodes.Ldloc_1);
            //// push 0
            //il.Emit(OpCodes.Ldc_I4_0);
            //// push length
            //il.Emit(OpCodes.Ldc_I4, bytes);
            //// call stream.Read(bytes, 0, length) and discard result
            //il.Emit(OpCodes.Callvirt, module.ImportReference(typeof(Stream).GetMethod("Read", new[] { typeof(byte[]), typeof(int), typeof(int) })));
            //il.Emit(OpCodes.Pop);
            //// call Assembly.Load(bytes), discarding the result
            //il.Emit(OpCodes.Call, module.ImportReference(typeof(System.Reflection.Assembly).GetMethod("Load", new[] { typeof(byte[]) })));
            //il.Emit(OpCodes.Pop);
            //// dispose the stream
            //// if we want to do this with try-finally, check out http://stackoverflow.com/questions/12769699/mono-cecil-injecting-try-finally
            //il.Emit(OpCodes.Ldloc_0);
            //il.Emit(OpCodes.Call, module.ImportReference(typeof(Stream).GetMethod("Dispose")));
            //il.Emit(OpCodes.Pop);
            //// return
            //il.Emit(OpCodes.Ret);
            //staticConstructor.Body.OptimizeMacros();

            var newAssemblyPath = Path.Combine(Path.GetDirectoryName(this.context.AssemblyFilePath), Path.GetFileName(tempAssemblyPath));
            File.Move(tempAssemblyPath, newAssemblyPath);

            var newAssembly = AssemblyDefinition.ReadAssembly(newAssemblyPath);
            module.AssemblyReferences.Add(AssemblyNameReference.Parse(newAssembly.FullName));

            var compiledRegexMethods = new Dictionary<RegexDefinition, CompiledRegexMethods>();
            foreach (var regex in regularExpressionsToCompile)
            {
                var noTimeoutField = new FieldDefinition("cached" + regex.Name, FieldAttributes.Private | FieldAttributes.Static, module.ImportReference(typeof(Regex)));
                type.Fields.Add(noTimeoutField);

                var noTimeoutMethod = new MethodDefinition(regex.Name, MethodAttributes.Public | MethodAttributes.Static, module.ImportReference(typeof(Regex)));
                type.Methods.Add(noTimeoutMethod);

                var il = noTimeoutMethod.Body.GetILProcessor();
                il.Emit(OpCodes.Ldsfld, noTimeoutField);
                var epilogStartInstruction = Instruction.Create(OpCodes.Ldsfld, noTimeoutField);
                // Branch if value on stack is true, not null or non-zero
                il.Emit(OpCodes.Brtrue, epilogStartInstruction);
                il.Emit(OpCodes.Newobj, module.ImportReference(newAssembly.MainModule.GetType(regex.Namespace + "." + regex.Name).GetConstructors().Single(c => !c.HasParameters)));
                il.Emit(OpCodes.Stsfld, noTimeoutField);
                il.Append(epilogStartInstruction);
                il.Emit(OpCodes.Ret);
                noTimeoutMethod.Body.OptimizeMacros();

                var timeoutField = new FieldDefinition("cached" + regex.Name + "WithTimeout", FieldAttributes.Private | FieldAttributes.Static, module.ImportReference(typeof(Regex)));
                type.Fields.Add(timeoutField);

                var timeoutMethod = new MethodDefinition(regex.Name, MethodAttributes.Public | MethodAttributes.Static, module.ImportReference(typeof(Regex)));
                timeoutMethod.Body.InitLocals = true;
                timeoutMethod.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(Regex))));
                timeoutMethod.Parameters.Add(new ParameterDefinition("matchTimeout", ParameterAttributes.None, module.ImportReference(typeof(TimeSpan))));
                type.Methods.Add(timeoutMethod);

                il = timeoutMethod.Body.GetILProcessor();
                // var regex = cached
                il.Emit(OpCodes.Ldsfld, timeoutField);
                il.Emit(OpCodes.Stloc_0);
                // if (regex == null
                il.Emit(OpCodes.Ldloc_0);
                var startBuildingRegexInstruction = Instruction.Create(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Brfalse, startBuildingRegexInstruction);
                // || regex.MatchTimeout != matchTimeout)
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Callvirt, module.ImportReference(typeof(Regex).GetProperty("MatchTimeout").GetMethod));
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, module.ImportReference(typeof(TimeSpan).GetMethod("op_Inequality", new[] { typeof(TimeSpan), typeof(TimeSpan) }))); 
                epilogStartInstruction = Instruction.Create(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Brfalse, epilogStartInstruction);
                // regex = new [CustomRegex](matchTimeout)
                il.Append(startBuildingRegexInstruction);
                il.Emit(
                    OpCodes.Newobj,
                    module.ImportReference(
                        newAssembly.MainModule
                            .GetType(regex.Namespace + "." + regex.Name)
                            .GetConstructors()
                            .Single(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.Name == nameof(TimeSpan))
                    )
                );
                il.Emit(OpCodes.Stloc_0);
                // cached = regex
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Stsfld, timeoutField);
                // return regex
                il.Append(epilogStartInstruction);
                il.Emit(OpCodes.Ret);
                timeoutMethod.Body.OptimizeMacros();

                compiledRegexMethods.Add(new RegexDefinition(regex.Pattern, regex.Options), new CompiledRegexMethods(noTimeoutMethod, timeoutMethod));
            }

            foreach (var referencingMethod in this.replacableRegexDefinitionsByMethod.Keys)
            {
                this.PostProcessMethod(referencingMethod, compiledRegexMethods);
            }
        }

        private void PostProcessMethod(MethodDefinition method, Dictionary<RegexDefinition, CompiledRegexMethods> compiledRegexes)
        {
            var definitions = this.replacableRegexDefinitionsByMethod[method];

            method.Body.SimplifyMacros();
            try
            {
                foreach (var definition in definitions)
                {
                    var reference = method.Body.Instructions
                        .Select(this.referenceFinder.TryGetRegexReference)
                        .First(@ref => @ref != null);

                    var compiledRegex = compiledRegexes[reference.Definition];
                    var il = method.Body.GetILProcessor();
                    il.Remove(reference.PatternInstruction); // delete ldstr
                    if (reference.OptionsInstruction != null)
                    {
                        il.Remove(reference.OptionsInstruction);
                    }
                    var replacementMethod = reference.RegexMethod.TimeoutParameterIndex.HasValue
                        ? compiledRegex.TimeoutMethod
                        : compiledRegex.NoTimeoutMethod;
                    il.Replace(reference.CallInstruction, Instruction.Create(OpCodes.Call, replacementMethod));
                }
            }
            finally
            {
                method.Body.OptimizeMacros();
            }
        }

        private sealed class CompiledRegexMethods
        {
            public CompiledRegexMethods(MethodDefinition noTimeoutMethod, MethodDefinition timeoutMethod)
            {
                this.NoTimeoutMethod = noTimeoutMethod;
                this.TimeoutMethod = timeoutMethod;
            }

            public MethodDefinition NoTimeoutMethod { get; }
            public MethodDefinition TimeoutMethod { get; }
        }
    }
}
