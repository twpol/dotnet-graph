using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotNet_Graph
{
    class Program
    {
        static async Task Main(string path)
        {
            MSBuildLocator.RegisterDefaults();
            var workspace = MSBuildWorkspace.Create();
            await workspace.OpenSolutionAsync(path);

            var compilation = await workspace.CurrentSolution.Projects.First().GetCompilationAsync();
            var global = compilation.GlobalNamespace;

            var rootClass = compilation.GetEntryPoint(System.Threading.CancellationToken.None).ContainingType;
            var visited = new HashSet<string>();
            Console.WriteLine("graph {");
            DumpType(rootClass, visited);
            Console.WriteLine("}");
        }

        static void DumpType(INamespaceOrTypeSymbol symbol, ISet<string> visited)
        {
            var name = symbol.ToDisplayString();
            if (visited.Contains(name)) return;
            visited.Add(name);

            var referencedTypes = new HashSet<string>();
            var newSymbols = new HashSet<INamespaceOrTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var member in symbol.GetMembers())
            {
                if (member is IFieldSymbol field)
                {
                    var type = field.Type;
                    if (type.Locations.Any(location => location.IsInSource))
                    {
                        var innerName = type.ToDisplayString();
                        if (!referencedTypes.Contains(innerName) && innerName != name) referencedTypes.Add(innerName);
                        newSymbols.Add(type);
                    }
                }
            }

            foreach (var referencedType in referencedTypes) Console.WriteLine($@"  ""{name}"" -- ""{referencedType}""");

            foreach (var newSymbol in newSymbols) DumpType(newSymbol, visited);
        }
    }
}
