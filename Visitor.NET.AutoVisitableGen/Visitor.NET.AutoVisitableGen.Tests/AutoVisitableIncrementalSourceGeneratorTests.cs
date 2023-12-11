using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Visitor.NET.AutoVisitableGen.Tests;

public class AutoVisitableIncrementalSourceGeneratorTests
{
    [Theory]
    [InlineData("class")]
    [InlineData("record")]
    public void CorrectlyGeneratedSourceTest(string keyword)
    {
        var inputCompilation = CreateCompilation($@"
using Visitor.NET;

namespace MyNamespace;

public abstract {keyword} BinaryTreeNode : IVisitable<BinaryTreeNode>
{{
    public abstract TReturn Accept<TReturn>(
        IVisitor<BinaryTreeNode, TReturn> visitor);
}}

[AutoVisitable<BinaryTreeNode>]
public partial {keyword} Operation(
    char Symbol,
    BinaryTreeNode Left,
    BinaryTreeNode Right) : BinaryTreeNode
{{
}}
");

        var expectedSource = $@"// <auto-generated/>

using Visitor.NET;

namespace MyNamespace;

public partial {keyword} Operation :
    IVisitable<Operation>
{{
    public override TReturn Accept<TReturn>(
        IVisitor<BinaryTreeNode, TReturn> visitor) =>
        Accept(visitor);

    public TReturn Accept<TReturn>(
        IVisitor<Operation, TReturn> visitor) =>
        visitor.Visit(this);
}}
";

        var generator = new AutoVisitableIncrementalSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        
        driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);
        Debug.Assert(diagnostics.IsEmpty);
        Debug.Assert(outputCompilation.SyntaxTrees.Count() == 3);

        var runResult = driver.GetRunResult();

        var generatedFileSyntax = runResult.GeneratedTrees
            .Single(t => t.FilePath.EndsWith("Operation.g.cs"));

        Assert.Equal(
            expectedSource,
            generatedFileSyntax.GetText().ToString(),
            ignoreLineEndingDifferences: true);
    }

    private static Compilation CreateCompilation(string source) =>
        CSharpCompilation.Create("compilation",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[] { MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
}