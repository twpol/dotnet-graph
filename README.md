# DotNetGraph

A simple utility to create Graphviz/Mermaid graphs of .NET code.

## Usage

```
dotnet run -- [options]
```
```
DotNetGraph [options]
```

## Options

- `--path <path>`

  Specify the Visual Studio Solution file to analyse (required).

- `--format <Graphviz|MermaidFlowchart>`

  Specify the output format (default: Graphviz).

- `--root <root>`

  Specify the root class to analyse (default: program entry point).

- `--include-base`

  Analyse base classes.

- `--include-derived`

  Analyse derived classes.

- `--include-implementations`

  Analyse implementations of interfaces.

- `--include-interfaces`

  Analyse implemented interfaces.

- `--include-members`

  Analyse class members (properties and fields).

- `--include-type-args`

  Analyse class type arguments.

- `--include-enums`

  Analyse enum classes.
