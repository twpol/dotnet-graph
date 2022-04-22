using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotNet_Graph
{
    class Program
    {
        enum OutputFormat
        {
            Graphviz,
            MermaidFlowchart,
        }

        static HashSet<ISymbol> Visited = new(SymbolEqualityComparer.Default);
        static Queue<INamespaceOrTypeSymbol> Unvisited = new();
        static HashSet<ISymbol> Interfaces = new(SymbolEqualityComparer.Default);


        static Solution Solution;
        static OutputFormat Format;
        static bool IncludeDerived;
        static bool IncludeBase;
        static bool IncludeInterfaces;
        static bool IncludeMembers;
        static bool IncludeTypeArgs;

        static async Task Main(string path, OutputFormat format = OutputFormat.Graphviz, string root = null, bool includeDerived = false, bool includeBase = false, bool includeInterfaces = false, bool includeMembers = false, bool includeTypeArgs = false)
        {
            Format = format;
            IncludeDerived = includeDerived;
            IncludeBase = includeBase;
            IncludeInterfaces = includeInterfaces;
            IncludeMembers = includeMembers;
            IncludeTypeArgs = includeTypeArgs;

            if (IncludeDerived == false && IncludeBase == false && IncludeInterfaces == false && IncludeMembers == false && IncludeTypeArgs == false)
            {
                IncludeDerived = true;
                IncludeBase = true;
                IncludeInterfaces = true;
                IncludeMembers = true;
                IncludeTypeArgs = true;
            }

            MSBuildLocator.RegisterDefaults();
            var workspace = MSBuildWorkspace.Create();
            await workspace.OpenSolutionAsync(path);

            Solution = workspace.CurrentSolution;

            var compilation = await workspace.CurrentSolution.Projects.First().GetCompilationAsync();
            var global = compilation.GlobalNamespace;
            var rootClass = root == null ? compilation.GetEntryPoint(System.Threading.CancellationToken.None).ContainingType : compilation.GetTypeByMetadataName(root);

            switch (Format)
            {
                case OutputFormat.Graphviz:
                    Console.WriteLine("digraph {");
                    Console.WriteLine("  graph [splines = polyline]");
                    Console.WriteLine("  edge [color = \"#cccccc\"]");
                    break;
                case OutputFormat.MermaidFlowchart:
                    Console.WriteLine("```mermaid");
                    Console.WriteLine("flowchart");
                    break;
            }
            await WriteSymbolGraph(rootClass);
            await WriteInterfaceImplementations(global, WriteSymbolGraph);
            switch (Format)
            {
                case OutputFormat.Graphviz:
                    Console.WriteLine("}");
                    break;
                case OutputFormat.MermaidFlowchart:
                    Console.WriteLine("```");
                    break;
            }
        }

        static async Task WriteSymbolGraph(INamespaceOrTypeSymbol symbol)
        {
            Unvisited.Enqueue(symbol);
            while (Unvisited.TryDequeue(out var next))
            {
                await WriteSymbol(next);
            }
        }

        static async Task WriteSymbol(INamespaceOrTypeSymbol symbol)
        {
            if (Visited.Contains(symbol)) return;
            Visited.Add(symbol);

            var name = symbol.ToDisplayString();
            switch (Format)
            {
                case OutputFormat.Graphviz:
                    Console.WriteLine($@"  ""{name}"" [label = ""{symbol.Name}""]");
                    break;
                case OutputFormat.MermaidFlowchart:
                    Console.WriteLine($@"  {name.Replace(".", "_")}[{symbol.Name}]");
                    break;
            }

            var references = new HashSet<string>();

            if (IncludeDerived && symbol is INamedTypeSymbol namedType)
            {
                foreach (var derived in await SymbolFinder.FindDerivedClassesAsync(namedType, Solution, false, null, CancellationToken.None))
                {
                    AddType(derived, references);
                }
            }

            if (symbol is ITypeSymbol type)
            {
                if (type.TypeKind == TypeKind.Interface)
                {
                    Interfaces.Add(type);
                }
                if (IncludeBase && type.BaseType != null)
                {
                    AddType(type.BaseType, references);
                }
                if (IncludeInterfaces)
                {
                    foreach (var @interface in type.Interfaces)
                    {
                        AddType(@interface, references);
                    }
                }
            }

            if (IncludeMembers)
            {
                foreach (var member in symbol.GetMembers())
                {
                    if (member is IFieldSymbol field)
                    {
                        AddType(field.Type, references);
                    }
                }
            }

            foreach (var reference in references)
            {
                switch (Format)
                {
                    case OutputFormat.Graphviz:
                        Console.WriteLine($@"  ""{name}"" -> ""{reference}""");
                        break;
                    case OutputFormat.MermaidFlowchart:
                        Console.WriteLine($@"  {name.Replace(".", "_")} --> {reference.Replace(".", "_")}");
                        break;
                }
            }
        }

        static void AddType(ITypeSymbol type, ISet<string> references)
        {
            if (type.Locations.Any(location => location.IsInSource))
            {
                references.Add(type.ToDisplayString());
                Unvisited.Enqueue(type);
            }
            if (IncludeTypeArgs && type is INamedTypeSymbol namedType)
            {
                foreach (var typeArg in namedType.TypeArguments)
                {
                    AddType(typeArg, references);
                }
            }
        }

        static async Task WriteInterfaceImplementations(INamespaceSymbol ns, Func<INamedTypeSymbol, Task> visitor)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                if (type.Interfaces.Any(i => Interfaces.Contains(i)))
                {
                    await visitor(type);
                }
            }

            foreach (var child in ns.GetNamespaceMembers())
            {
                await WriteInterfaceImplementations(child, visitor);
            }
        }
    }
}
