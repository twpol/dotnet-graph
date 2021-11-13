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

        static async Task Main(string path)
        {
            MSBuildLocator.RegisterDefaults();
            var workspace = MSBuildWorkspace.Create();
            await workspace.OpenSolutionAsync(path);

            var compilation = await workspace.CurrentSolution.Projects.First().GetCompilationAsync();
            var global = compilation.GlobalNamespace;
            var rootClass = compilation.GetEntryPoint(System.Threading.CancellationToken.None).ContainingType;

            Console.WriteLine("digraph {");
            Console.WriteLine("  graph [splines = polyline]");
            Console.WriteLine("  edge [color = \"#cccccc\"]");
            WriteSymbolGraph(rootClass);
            WriteInterfaceImplementations(global);
            Console.WriteLine("}");
        }

        static void WriteSymbolGraph(INamespaceOrTypeSymbol symbol)
        {
            Unvisited.Enqueue(symbol);
            while (Unvisited.TryDequeue(out var next))
            {
                WriteSymbol(next);
            }
        }

        static void WriteSymbol(INamespaceOrTypeSymbol symbol)
        {
            if (Visited.Contains(symbol)) return;
            Visited.Add(symbol);

            var name = symbol.ToDisplayString();
            Console.WriteLine($@"  ""{name}"" [label = ""{symbol.Name}""]");

            var references = new HashSet<string>();

            if (symbol is ITypeSymbol type)
            {
                if (type.TypeKind == TypeKind.Interface)
                {
                    Interfaces.Add(type);
                }
                if (type.BaseType != null)
                {
                    AddType(type.BaseType, references);
                }
                foreach (var @interface in type.Interfaces)
                {
                    AddType(@interface, references);
                }
            }

            foreach (var member in symbol.GetMembers())
            {
                if (member is IFieldSymbol field)
                {
                    AddType(field.Type, references);
                }
            }

            foreach (var reference in references)
            {
                Console.WriteLine($@"  ""{name}"" -> ""{reference}""");
            }
        }

        static void AddType(ITypeSymbol type, ISet<string> references)
        {
            if (type.Locations.Any(location => location.IsInSource))
            {
                references.Add(type.ToDisplayString());
                Unvisited.Enqueue(type);
            }
            if (type is INamedTypeSymbol namedType)
            {
                foreach (var typeArg in namedType.TypeArguments)
                {
                    AddType(typeArg, references);
                }
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

            foreach (var child in ns.GetNamespaceMembers())
            {
                WriteInterfaceImplementations(child);
            }
        }
    }
}
