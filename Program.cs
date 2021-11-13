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
        static Queue<INamespaceOrTypeSymbol> Unvisited = new();
        static HashSet<ISymbol> Interfaces = new(SymbolEqualityComparer.Default);
        static Dictionary<string, HashSet<string>> Namespaces = new();

        static async Task Main(string path)
        {
            MSBuildLocator.RegisterDefaults();
            var workspace = MSBuildWorkspace.Create();
            await workspace.OpenSolutionAsync(path);

            var compilation = await workspace.CurrentSolution.Projects.First().GetCompilationAsync();
            var global = compilation.GlobalNamespace;
            var rootClass = compilation.GetEntryPoint(System.Threading.CancellationToken.None).ContainingType;
            Unvisited.Enqueue(rootClass);

            Console.WriteLine("digraph {");
            Console.WriteLine("  graph [splines = polyline]");
            Console.WriteLine("  edge [color = \"#cccccc\"]");
            while (Unvisited.Count > 0) WriteNextSymbol();
            WriteInterfaceImplementations(global);
            Console.WriteLine("}");
        }

        static void WriteNextSymbol()
        {
            if (Unvisited.TryDequeue(out var symbol))
            {
                WriteSymbolGraph(symbol);
            }
        }

        static void WriteSymbolGraph(INamespaceOrTypeSymbol symbol)
        {
            if (Visited.Contains(symbol)) return;
            Visited.Add(symbol);

            var name = symbol.ToDisplayString();
            var ns = symbol.ContainingNamespace.ToDisplayString();
            if (!Namespaces.ContainsKey(ns))
            {
                Namespaces.Add(ns, new());
            }
            Namespaces[ns].Add(name);
            Console.WriteLine($@"  ""{name}"" [label = ""{symbol.Name}""]");

            var references = new HashSet<string>();

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
                    AddType(innerType, references);
                    if (innerType is INamedTypeSymbol namedType)
                    {
                        foreach (var typeArg in namedType.TypeArguments)
                        {
                            AddType(typeArg, references);
                        }
                    }
                }
                else if (member is INamespaceOrTypeSymbol namespaceOrType)
                {
                    Unvisited.Enqueue(namespaceOrType);
                }
            }

            foreach (var reference in references) Console.WriteLine($@"  ""{name}"" -> ""{reference}""");
        }

        static void AddType(ITypeSymbol type, ISet<string> references)
        {
            if (type.Locations.Any(location => location.IsInSource))
            {
                references.Add(type.ToDisplayString());
                Unvisited.Enqueue(type);
            }
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
