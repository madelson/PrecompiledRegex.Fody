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
        private readonly RegexMethods regexMethods;
        private readonly WeavingContext context;

        private readonly Dictionary<MethodDefinition, List<KeyValuePair<string, RegexOptions>>> constantRegularExpressionsByMethod
            = new Dictionary<MethodDefinition, List<KeyValuePair<string, RegexOptions>>>();

        public TypeProcessor(RegexMethods regexMethods, WeavingContext context)
        {
            this.regexMethods = regexMethods;
            this.context = context;
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

        public void PostProcessAllTypes()
        {
            var regularExpressionsToCompile = this.constantRegularExpressionsByMethod
                .SelectMany(kvp => kvp.Value)
                .Distinct()
                .Select((kvp, index) => new RegexCompilationInfo(
                    pattern: kvp.Key,
                    options: kvp.Value & ~RegexOptions.Compiled,
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

            var module = this.constantRegularExpressionsByMethod.First().Key.Module;

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

            foreach (var regex in regularExpressionsToCompile)
            {
                var field = new FieldDefinition("cached" + regex.Name, FieldAttributes.Private | FieldAttributes.Static, module.ImportReference(typeof(Regex)));
                type.Fields.Add(field);

                var method = new MethodDefinition(regex.Name, MethodAttributes.Public | MethodAttributes.Static, module.ImportReference(typeof(Regex)));
                type.Methods.Add(method);

                var il = method.Body.GetILProcessor();
                il.Emit(OpCodes.Ldsfld, field);
                var epilogStartInstruction = Instruction.Create(OpCodes.Ldsfld, field);
                // Branch if value on stack is true, not null or non-zero
                il.Emit(OpCodes.Brtrue, epilogStartInstruction);
                il.Emit(OpCodes.Newobj, module.ImportReference(newAssembly.MainModule.GetType(regex.Namespace + "." + regex.Name).GetConstructors().Single(c => !c.HasParameters)));
                il.Emit(OpCodes.Stsfld, field);
                il.Append(epilogStartInstruction);
                il.Emit(OpCodes.Ret);
                method.Body.OptimizeMacros();
            }
        }

        private void PreProcessMethod(MethodDefinition method)
        {
            // quick checks
            if (!method.HasBody) { return; }
            if (!this.HasAnyRegexMethodReferences(method.Body)) { return; }

            // see http://stackoverflow.com/questions/7267480/does-mono-cecil-take-care-of-branches-etc-location
            method.Body.SimplifyMacros();
            try
            {
                List<KeyValuePair<string, RegexOptions>> patterns = null;
                foreach (var instruction in method.Body.Instructions)
                {
                    if (this.regexMethods.IsRegexMethodReference(instruction))
                    {
                        var kvp = PatternAndOptionsFinder.TryFindPatternAndOptions(instruction);
                        if (kvp.HasValue)
                        {
                            (patterns = (patterns ?? new List<KeyValuePair<string, RegexOptions>>())).Add(kvp.Value);
                        }
                    }
                }

                if (patterns != null)
                {
                    this.constantRegularExpressionsByMethod.Add(method, patterns);
                }
            }
            finally
            {
                method.Body.OptimizeMacros();
            }
        }

        private bool HasAnyRegexMethodReferences(MethodBody body)
        {
            var regexMethods = this.regexMethods;
            var instructions = body.Instructions;

            for (var i = 0; i < instructions.Count; ++i)
            {
                if (regexMethods.IsRegexMethodReference(instructions[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
