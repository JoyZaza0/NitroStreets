using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public static class ProjectGeneration
{
    private static bool ShouldGenerateProject(Assembly assembly)
    {
        return assembly.sourceFiles.Any(f =>
            f.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
            f.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase) ||
            f.StartsWith("Packages/com.antigravity.ide", StringComparison.OrdinalIgnoreCase) ||
            f.StartsWith("Packages\\com.antigravity.ide", StringComparison.OrdinalIgnoreCase));
    }

    public static void Sync()
    {
        var assemblies = CompilationPipeline.GetAssemblies()
            .Where(ShouldGenerateProject)
            .ToArray();
        foreach (var assembly in assemblies)
        {
            GenerateCsproj(assembly);
        }
        GenerateSolution(assemblies);
    }

    public static void SyncIfNeeded(string[] addedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, string[] importedAssets)
    {
        Sync();
    }

    private static void GenerateCsproj(Assembly assembly)
    {
        string projectPath = Path.Combine(Directory.GetCurrentDirectory(), $"{assembly.name}.csproj");
        
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<Project ToolsVersion=\"4.0\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
        
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <BaseIntermediateOutputPath>Temp\\obj\\Debug\\$(MSBuildProjectName)\\</BaseIntermediateOutputPath>");
        sb.AppendLine("    <IntermediateOutputPath>$(BaseIntermediateOutputPath)</IntermediateOutputPath>");
        sb.AppendLine("    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>");
        sb.AppendLine("    <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>");
        sb.AppendLine("    <ProductVersion>10.0.20506</ProductVersion>");
        sb.AppendLine("    <SchemaVersion>2.0</SchemaVersion>");
        sb.AppendLine($"    <ProjectGuid>{{{GenerateGuid(assembly.name)}}}</ProjectGuid>");
        sb.AppendLine("    <OutputType>Library</OutputType>");
        sb.AppendLine($"    <AssemblyName>{assembly.name}</AssemblyName>");
        sb.AppendLine("    <TargetFrameworkVersion>v4.7.1</TargetFrameworkVersion>");
        sb.AppendLine("    <LangVersion>9.0</LangVersion>");
        sb.AppendLine("    <FileAlignment>512</FileAlignment>");
        sb.AppendLine("    <BaseDirectory>.</BaseDirectory>");
        sb.AppendLine("    <OutputPath>Temp\\bin\\Debug\\$(MSBuildProjectName)\\</OutputPath>");
        sb.AppendLine($"    <DefineConstants>{string.Join(";", assembly.defines)};UNITY_EDITOR</DefineConstants>");
        sb.AppendLine($"    <AllowUnsafeBlocks>{assembly.compilerOptions.AllowUnsafeCode.ToString().ToLowerInvariant()}</AllowUnsafeBlocks>");
        sb.AppendLine("    <NoConfig>true</NoConfig>");
        sb.AppendLine("    <NoStdLib>true</NoStdLib>");
        sb.AppendLine("    <AddAdditionalExplicitAssemblyReferences>false</AddAdditionalExplicitAssemblyReferences>");
        sb.AppendLine("    <ImplicitlyExpandNETStandardFacades>false</ImplicitlyExpandNETStandardFacades>");
        sb.AppendLine("    <ImplicitlyExpandDesignTimeFacades>false</ImplicitlyExpandDesignTimeFacades>");
        sb.AppendLine("    <NoWarn>0169;0649;0414;CS0618;USG0001</NoWarn>");
        sb.AppendLine("  </PropertyGroup>");

        sb.AppendLine("  <ItemGroup>");
        foreach (var reference in assembly.compiledAssemblyReferences)
        {
            sb.AppendLine($"    <Reference Include=\"{Path.GetFileNameWithoutExtension(reference)}\">");
            sb.AppendLine($"      <HintPath>{reference}</HintPath>");
            sb.AppendLine("    </Reference>");
        }
        foreach (var refAssembly in assembly.assemblyReferences)
        {
            string dllPath = $"Library/ScriptAssemblies/{refAssembly.name}.dll";
            sb.AppendLine($"    <Reference Include=\"{refAssembly.name}\">");
            sb.AppendLine($"      <HintPath>{dllPath}</HintPath>");
            sb.AppendLine("    </Reference>");
        }
        sb.AppendLine("  </ItemGroup>");

        sb.AppendLine("  <ItemGroup>");
        foreach (var sourceFile in assembly.sourceFiles)
        {
            sb.AppendLine($"    <Compile Include=\"{sourceFile}\" />");
        }
        sb.AppendLine("  </ItemGroup>");

        sb.AppendLine("  <Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />");
        sb.AppendLine("  <Target Name=\"GetFrameworkPaths\" />");
        sb.AppendLine("  <Target Name=\"GetReferenceAssemblyPaths\" />");
        sb.AppendLine("  <Target Name=\"GenerateTargetFrameworkMonikerAttribute\" />");
        sb.AppendLine("</Project>");

        File.WriteAllText(projectPath, sb.ToString());
    }

    private static void GenerateSolution(Assembly[] assemblies)
    {
        string solutionPath = Path.Combine(Directory.GetCurrentDirectory(), $"{Path.GetFileName(Directory.GetCurrentDirectory())}.sln");
        var sb = new StringBuilder();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio 15");
        
        foreach (var assembly in assemblies)
        {
            string guid = GenerateGuid(assembly.name);
            sb.AppendLine($"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{assembly.name}\", \"{assembly.name}.csproj\", \"{{{guid}}}\"");
            sb.AppendLine("EndProject");
        }

        sb.AppendLine("Global");
        sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        sb.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
        sb.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var assembly in assemblies)
        {
            string guid = GenerateGuid(assembly.name);
            sb.AppendLine($"\t\t{{{guid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            sb.AppendLine($"\t\t{{{guid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            sb.AppendLine($"\t\t{{{guid}}}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            sb.AppendLine($"\t\t{{{guid}}}.Release|Any CPU.Build.0 = Release|Any CPU");
        }
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(SolutionProperties) = preSolution");
        sb.AppendLine("\t\tHideSolutionNode = FALSE");
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("EndGlobal");
        
        File.WriteAllText(solutionPath, sb.ToString());
    }

    private static string GenerateGuid(string input)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.Default.GetBytes(input));
            return new Guid(hash).ToString().ToUpper();
        }
    }
}
