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
        static HashSet<ISymbol> Visited = new(SymbolEqualityComparer.Default);
        static HashSet<ISymbol> Interfaces = new(SymbolEqualityComparer.Default);

        static async Task Main(string path)
        {
            MSBuildLocator.RegisterDefaults();
            var workspace = MSBuildWorkspace.Create();
            await workspace.OpenSolutionAsync(path);

            var compilation = await workspace.CurrentSolution.Projects.First().GetCompilationAsync();
            var global = compilation.GlobalNamespace;
            var rootClass = compilation.GetEntryPoint(System.Threading.CancellationToken.None).ContainingType;

            Console.WriteLine("graph {");
            WriteSymbolGraph(rootClass);
            WriteInterfaceImplementations(global);
            Console.WriteLine("}");
        }

        static void WriteSymbolGraph(INamespaceOrTypeSymbol symbol)
        {
            if (Visited.Contains(symbol)) return;
            Visited.Add(symbol);

            var name = symbol.ToDisplayString();
            var references = new HashSet<string>();
            var nextSymbols = new HashSet<INamespaceOrTypeSymbol>(SymbolEqualityComparer.Default);

            if (symbol is ITypeSymbol type)
            {
                if (type.TypeKind == TypeKind.Interface)
                {
                    Interfaces.Add(type);
                }
                foreach (var @interface in type.Interfaces)
                {
                    references.Add(@interface.ToDisplayString());
                }
            }

            foreach (var member in symbol.GetMembers())
            {
                if (member is IFieldSymbol field)
                {
                    var innerType = field.Type;
                    if (innerType.Locations.Any(location => location.IsInSource))
                    {
                        references.Add(innerType.ToDisplayString());
                        nextSymbols.Add(innerType);
                    }
                }
                else if (member is INamespaceOrTypeSymbol namespaceOrType)
                {
                    nextSymbols.Add(namespaceOrType);
                }
            }

            foreach (var reference in references) Console.WriteLine($@"  ""{name}"" -- ""{reference}""");

            foreach (var nextSymbol in nextSymbols) WriteSymbolGraph(nextSymbol);
        }

        static void WriteInterfaceImplementations(INamespaceSymbol ns)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                if (type.Interfaces.Any(i => Interfaces.Contains(i)))
                {
                    WriteSymbolGraph(type);
                }
            }

            foreach (var child in ns.GetNamespaceMembers()) WriteInterfaceImplementations(child);
        }
    }
}
