using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Text;
namespace CsxRunner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string pathToScript = args[0];
                        
            SourceText encodedSourceText = null;
            string codeAsPlainText = null;            
            using(var fileStream = new FileStream(pathToScript, FileMode.Open))
            {
                // We need to create a SourceText instance with an encoding
                encodedSourceText = SourceText.From(fileStream, Encoding.UTF8);
                codeAsPlainText = encodedSourceText.ToString();        
            }

            var scriptOptions = ScriptOptions.Default.WithFilePath(pathToScript);  
            var script = CSharpScript.Create(codeAsPlainText, scriptOptions);

            // Get the Compilation that gives us full access to the Roslyn Scriping API
            var compilation = script.GetCompilation();

            SyntaxTree syntaxTree = compilation.SyntaxTrees.First();

            // The problem with CSharpScript.Create is that it does not allow 
            // to specify the encoding needed to emit the debug information.
            // Might need to open up an issue on this in the Roslyn repo.
            
            // First hack
            var encodingField = syntaxTree.GetType().GetField("_encodingOpt",BindingFlags.Instance | BindingFlags.NonPublic);
            encodingField.SetValue(syntaxTree, Encoding.UTF8);

            // Second hack

            var lazyTextField = syntaxTree.GetType().GetField("_lazyText",BindingFlags.Instance | BindingFlags.NonPublic);
            lazyTextField.SetValue(syntaxTree, encodedSourceText);

            // Next we need to write out the dynamic assembly

            EmitResult emitResult;
            using(var peStream = new MemoryStream())
            {
                using(var pdbStream = new MemoryStream())
                {
                    var emitOptions = new EmitOptions()
                    .WithDebugInformationFormat(DebugInformationFormat.PortablePdb);
                    
                    emitResult = compilation.Emit(peStream, pdbStream,null,null,null,emitOptions);
                    if (emitResult.Success)
                    {
                        peStream.Position = 0;
                        pdbStream.Position = 0;
                        var assemblyLoadContext = AssemblyLoadContext.Default;
                        var assembly = assemblyLoadContext.LoadFromStream(peStream,pdbStream);

                        var type = assembly.GetType("Submission#0");
                        var method = type.GetMethod("<Factory>", BindingFlags.Static | BindingFlags.Public);
                        var submissionStates = new object[2];
                        submissionStates[0] = null;
                        method.Invoke(null, new[] { submissionStates });
                    }                             
                }
            }            
        }
    }
}
