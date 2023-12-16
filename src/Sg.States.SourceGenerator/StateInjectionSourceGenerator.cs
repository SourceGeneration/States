﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sg.States.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public class StateInjectionSourceGenerator : IIncrementalGenerator
{
    public const string StateAttribute = "Sg.States.StateAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methodDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            StateAttribute,
            predicate: static (node, token) =>
            {
                if (node is TypeDeclarationSyntax type
                    //|| method.IsAbstract()
                    && type.IsPublic()
                    && !type.IsAbstract()
                    && type.TypeParameterList == null)
                {
                    return true;
                }

                return false;
            },
            transform: static (context, token) =>
            {
                return (TypeDeclarationSyntax)context.TargetNode;
            });

        var source = methodDeclarations.Collect().Combine(context.CompilationProvider);

        context.RegisterSourceOutput(source, static (sourceContext, source) =>
        {
            CancellationToken cancellationToken = sourceContext.CancellationToken;
            ImmutableArray<TypeDeclarationSyntax> types = source.Left;
            Compilation compilation = source.Right;

            CSharpCodeBuilder builder = new();

            builder.AppendAutoGeneratedComment();
            builder.AppendBlock("internal static class ___StateInitializer", () =>
            {
                builder.AppendLine("[global::System.Runtime.CompilerServices.ModuleInitializer]");
                builder.AppendBlock("public static void Initialize()", () =>
                {
                    foreach (var type in types)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var stateModel = (ITypeSymbol)compilation.GetSemanticModel(type.SyntaxTree).GetDeclaredSymbol(type, cancellationToken)!;
                        builder.AppendLine(GenerateProxy(stateModel));
                    }
                });
            });
            sourceContext.AddSource($"Sg.States.___StateInitializer.g.cs", builder.ToString());

        });
    }

    private static string GenerateProxy(ITypeSymbol stateModel)
    {
        var methods = stateModel.GetMembers().OfType<IMethodSymbol>().Where(method
                    => method.IsVirtual
                    //&& !method.IsDefinition
                    && !method.IsSealed
                    && !method.IsStatic
                    && !method.IsImplicitlyDeclared).ToList();

        var ns = stateModel.GetNamespace();

        return $"global::Sg.States.StateRegister.Add<{ns}.{stateModel.Name}>();";
    }
}
