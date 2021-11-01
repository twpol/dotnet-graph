using System;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotNet_Graph
{
    class Program
    {
        static async Task Main(string path)
        {
            MSBuildLocator.RegisterDefaults();
            var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(path);
            Console.WriteLine($"Loaded solution {path}");
            foreach (var project in workspace.CurrentSolution.Projects)
            {
                Console.WriteLine($"Project {project.Name}");
            }
        }
    }
}
