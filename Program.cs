using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotNetGraph
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


        static Solution Solution;
        static OutputFormat Format;
        static bool IncludeBase;
        static bool IncludeDerived;
        static bool IncludeImplementations;
        static bool IncludeInterfaces;
        static bool IncludeMembers;
        static bool IncludeTypeArgs;
        static bool IncludeEnums;
        static List<string> Excludes;

        /// <summary>
        /// Command-line tool for creating Graphviz/Mermaid graphs of .NET code
        /// </summary>
        /// <param name="path">Specify the Visual Studio Solution file to analyse</param>
        /// <param name="format">Specify the output format</param>
        /// <param name="root">Specify the root class to analyse (default: program entry point)</param>
        /// <param name="includeBase">Analyse base classes</param>
        /// <param name="includeDerived">Analyse derived classes</param>
        /// <param name="includeImplementations">Analyse implementations of interfaces</param>
        /// <param name="includeInterfaces">Analyse implemented interfaces</param>
        /// <param name="includeMembers">Analyse class members (properties and fields)</param>
        /// <param name="includeTypeArgs">Analyse class type arguments</param>
        /// <param name="includeEnums">Analyse enum classes</param>
        /// <param name="excludes">Exclude types from analysis</param>
        static async Task Main(string path, OutputFormat format = OutputFormat.Graphviz, string root = null, bool includeBase = false, bool includeDerived = false, bool includeImplementations = false, bool includeInterfaces = false, bool includeMembers = false, bool includeTypeArgs = false, bool includeEnums = false, List<string> excludes = null)
        {
            Format = format;
            IncludeBase = includeBase;
            IncludeDerived = includeDerived;
            IncludeImplementations = includeImplementations;
            IncludeInterfaces = includeInterfaces;
            IncludeMembers = includeMembers;
            IncludeTypeArgs = includeTypeArgs;
            IncludeEnums = includeEnums;
            Excludes = excludes ?? new List<string>();

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
                    Console.WriteLine($@"  {GetSafeMermaidName(name)}[{symbol.Name}]");
                    break;
            }

            var references = new HashSet<string>();

            if (symbol is INamedTypeSymbol namedType)
            {
                if (IncludeDerived)
                {
                    foreach (var derived in await SymbolFinder.FindDerivedClassesAsync(namedType, Solution, false, null, CancellationToken.None))
                    {
                        AddType(derived, references);
                    }
                    foreach (var derived in await SymbolFinder.FindDerivedInterfacesAsync(namedType, Solution, false, null, CancellationToken.None))
                    {
                        AddType(derived, references);
                    }
                }

                if (IncludeImplementations)
                {
                    foreach (var derived in await SymbolFinder.FindImplementationsAsync(namedType, Solution, false, null, CancellationToken.None))
                    {
                        AddType(derived, references);
                    }
                }
            }

            if (symbol is ITypeSymbol type)
            {
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
                        Console.WriteLine($@"  {GetSafeMermaidName(name)} --> {GetSafeMermaidName(reference)}");
                        break;
                }
            }
        }

        static void AddType(ITypeSymbol type, ISet<string> references)
        {
            if (Excludes.Contains(type.ToDisplayString())) return;
            if (!IncludeEnums && type.TypeKind == TypeKind.Enum) return;

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

        static string GetSafeMermaidName(string name)
        {
            return name.Replace(".", "_").Replace("<", "__").Replace(",", "_").Replace(" ", "").Replace(">", "");
        }
    }
}
