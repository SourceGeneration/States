﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SourceGeneration.ChangeTracking.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public partial class ChanageTrackingSourceGenerator : IIncrementalGenerator
{
    public const string RootNamespace = "SourceGeneration.ChangeTracking";

    public const string ChangeTrackingAttribute = $"{RootNamespace}.ChangeTrackingAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methodDeclarations = context.SyntaxProvider.ForAttributeWithMetadataName(
            ChangeTrackingAttribute,
            predicate: static (node, token) =>
            {
                if (node is not TypeDeclarationSyntax type
                    || !type.IsPartial()
                    || type.IsAbstract()
                    || type.TypeParameterList != null)
                {
                    return false;
                }

                if (!type.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ClassDeclaration) &&
                    !type.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.RecordDeclaration))
                {
                    return false;
                }

                //如果是内部类，需要确保父级都是 class，record 且为 partial
                //var parent = type.Parent;
                //while (parent != null)
                //{
                //    if (parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
                //        break;

                //    if (parent is not TypeDeclarationSyntax tp)
                //        return false;

                //    if (!tp.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ClassDeclaration) &&
                //        !tp.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.RecordDeclaration))
                //    {
                //        return false;
                //    }

                //    if (!tp.IsPartial() || tp.TypeParameterList != null)
                //        return false;

                //    parent = parent.Parent;
                //}

                return true;
            },
            transform: static (context, token) =>
            {
                return (TypeDeclarationSyntax)context.TargetNode;
            });

        var source = methodDeclarations.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(source, static (sourceContext, source) =>
        {
            CancellationToken cancellationToken = sourceContext.CancellationToken;
            TypeDeclarationSyntax type = source.Left;
            Compilation compilation = source.Right;

            SemanticModel model = compilation.GetSemanticModel(type.SyntaxTree);
            var typeSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(type, cancellationToken)!;

            var typeProxy = CreateProxy(typeSymbol, cancellationToken);

            var root = (CompilationUnitSyntax)type.SyntaxTree.GetRoot();

            CSharpCodeBuilder builder = new();
            builder.AppendAutoGeneratedComment();

            if (typeProxy.Namespace == null)
            {
                CreateSource(typeProxy, builder);
            }
            else
            {
                builder.AppendBlock($"namespace {typeProxy.Namespace}", () =>
                {
                    CreateSource(typeProxy, builder);
                });
            }

            var code = builder.ToString();
            sourceContext.AddSource($"{typeSymbol.GetFullName(false)}.ChangeTackingProxy.g.cs", code);
        });
    }
    
    private static void CreateSource(TypeDefinition typeProxy, CSharpCodeBuilder builder)
    {
        var typekind = typeProxy.IsRecord ? "record" : "class";

        List<string> implementations = [];

        if (!typeProxy.NotifyPropertyChanging) implementations.Add("global::System.ComponentModel.INotifyPropertyChanging");
        if (!typeProxy.NotifyPropertyChanged) implementations.Add("global::System.ComponentModel.INotifyPropertyChanged");
        if (!typeProxy.NotifyCollectionChanged) implementations.Add("global::System.Collections.Specialized.INotifyCollectionChanged");
        if (!typeProxy.ChangeTracking) implementations.Add($"global::{RootNamespace}.ICascadingChangeTracking");

        string? interfaces = null;
        if (implementations.Count > 0)
            interfaces = " : " + string.Join(", ", implementations);

        builder.AppendBlock($"partial {typekind} {typeProxy.Name}{interfaces}", () =>
        {
            if (!typeProxy.NotifyPropertyChanging)
                builder.AppendLine("public event global::System.ComponentModel.PropertyChangingEventHandler PropertyChanging;");

            if (!typeProxy.NotifyPropertyChanged)
                builder.AppendLine("public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;");

            if (!typeProxy.NotifyCollectionChanged)
                builder.AppendLine("public event global::System.Collections.Specialized.NotifyCollectionChangedEventHandler CollectionChanged;");

            if (!typeProxy.ChangeTracking)
            {
                builder.AppendLine("protected bool __cascadingChanged;");
                builder.AppendLine("protected bool __baseChanged;");
                builder.AppendLine();

                builder.AppendLine("public bool IsChanged => __baseChanged || __cascadingChanged;");
                builder.AppendLine("public bool IsCascadingChanged => __cascadingChanged;");

                builder.AppendLine();
                builder.AppendBlock("protected void OnPropertyChanging(string propertyName)", () =>
                {
                    builder.AppendLine("((global::System.ComponentModel.INotifyPropertyChanging)this).PropertyChanging?.Invoke(this, new global::System.ComponentModel.PropertyChangingEventArgs(propertyName));");
                });

                builder.AppendLine();
                builder.AppendBlock("protected void OnPropertyChanging(object sender, global::System.ComponentModel.PropertyChangingEventArgs e)", () =>
                {
                    builder.AppendLine("((global::System.ComponentModel.INotifyPropertyChanging)this).PropertyChanging?.Invoke(sender, e);");
                });

                builder.AppendLine();
                builder.AppendBlock("protected void OnPropertyChanged(string propertyName)", () =>
                {
                    builder.AppendLine("__baseChanged = true;");
                    builder.AppendLine("((global::System.ComponentModel.INotifyPropertyChanged)this).PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(propertyName));");
                });

                builder.AppendLine();
                builder.AppendBlock("protected void OnPropertyChanged(object sender, global::System.ComponentModel.PropertyChangedEventArgs e)", () =>
                {
                    builder.AppendLine("__cascadingChanged = true;");
                    builder.AppendLine("((global::System.ComponentModel.INotifyPropertyChanged)this).PropertyChanged?.Invoke(sender, e);");
                });

                builder.AppendLine();
                builder.AppendBlock("protected void OnCollectionChanged(object sender, global::System.Collections.Specialized.NotifyCollectionChangedEventArgs e)", () =>
                {
                    builder.AppendLine("__cascadingChanged = true;");
                    builder.AppendLine("((global::System.Collections.Specialized.INotifyCollectionChanged)this).CollectionChanged?.Invoke(sender, e);");
                });

                builder.AppendLine();
            }

            if (typeProxy.ChangeTracking)
            {
                builder.AppendBlock($"public override void AcceptChanges()", () =>
                {
                    builder.AppendBlock("if (__cascadingChanged)", () =>
                    {
                        foreach (var property in typeProxy.Properties.Where(x => x.ChangeTracking))
                        {
                            builder.AppendLine($"((global::System.ComponentModel.IChangeTracking)this.{property.PropertyName})?.AcceptChanges();");
                        }
                    });

                    builder.AppendLine("base.AcceptChanges();");
                });
            }
            else
            {
                builder.AppendBlock($"public virtual void AcceptChanges()", () =>
                {
                    builder.AppendLine("__baseChanged = false;");

                    builder.AppendBlock("if (__cascadingChanged)", () =>
                    {
                        builder.AppendLine("__cascadingChanged = false;");
                        foreach (var property in typeProxy.Properties.Where(x => x.ChangeTracking))
                        {
                            builder.AppendLine($"((global::System.ComponentModel.IChangeTracking)this.{property.PropertyName})?.AcceptChanges();");
                        }
                    });
                });
            }


            builder.AppendLine();

            foreach (var property in typeProxy.Properties)
            {
                var required = property.Required ? "required " : string.Empty;

                builder.AppendLine($"private {property.Type} {property.FieldName};");

                builder.AppendBlock($"public partial {required}{property.Type} {property.PropertyName}", () =>
                {
                    builder.AppendLine($"get => {property.FieldName};");

                    builder.AppendBlock(property.IsInitOnly ? "init" : "set", () =>
                    {
                        EmitSetMethod(builder, property);
                    });
                });
                builder.AppendLine();
            }
        });
    }

    private static void EmitSetMethod(CSharpCodeBuilder builder, PropertyDefinition property)
    {
        builder.AppendBlock($"if (!global::System.Collections.Generic.EqualityComparer<{property.Type}>.Default.Equals({property.FieldName}, value))", () =>
        {
            builder.AppendLine($"this.OnPropertyChanging(\"{property.PropertyName}\");");

            if (property.Kind == TypeProxyKind.Value)
            {
                builder.AppendLine($"{property.FieldName} = value;");
            }
            else
            {
                if (property.NotifyPropertyChanging || property.NotifyPropertyChanged || property.NotifyCollectionChanged)
                {
                    builder.AppendBlock($"if ({property.FieldName} is not null)", () =>
                    {
                        if (property.NotifyPropertyChanging)
                        {
                            builder.AppendLine($"((global::System.ComponentModel.INotifyPropertyChanging){property.FieldName}).PropertyChanging -= OnPropertyChanging;");
                            //builder.AppendBlock($"if ({property.FieldName} is global::System.ComponentModel.INotifyPropertyChanging __propertyChanging__)", () =>
                            //{
                            //    builder.AppendLine("__propertyChanging__.PropertyChanging -= OnPropertyChanging;");
                            //});
                        }
                        if (property.NotifyPropertyChanged)
                        {
                            builder.AppendLine($"((global::System.ComponentModel.INotifyPropertyChanged){property.FieldName}).PropertyChanged -= OnPropertyChanged;");
                            //builder.AppendBlock($"if ({property.FieldName} is global::System.ComponentModel.INotifyPropertyChanged __propertyChanged__)", () =>
                            //{
                            //    builder.AppendLine("__propertyChanged__.PropertyChanged -= OnPropertyChanged;");
                            //});
                        }
                        if (property.NotifyCollectionChanged)
                        {
                            builder.AppendLine($"((global::System.Collections.Specialized.INotifyCollectionChanged){property.FieldName}).CollectionChanged -= OnCollectionChanged;");
                            //builder.AppendBlock($"if ({property.FieldName} is global::System.Collections.Specialized.INotifyCollectionChanged __collectionChanged__)", () =>
                            //{
                            //    builder.AppendLine("__collectionChanged__.CollectionChanged -= OnCollectionChanged;");
                            //});
                        }
                    });
                    builder.AppendLine();
                }

                builder.AppendBlock("if (value is null)", () =>
                {
                    builder.AppendLine($"{property.FieldName} = null;");
                });
                builder.AppendBlock("else", () =>
                {
                    SetPropertyProxy(builder, property);
                });
            }

            builder.AppendLine($"OnPropertyChanged(\"{property.PropertyName}\");");
        });
        if (property.Kind == TypeProxyKind.Collection)
        {
            builder.AppendBlock("else if (value is not null && value is not global::System.ComponentModel.IChangeTracking)", () =>
            {
                SetPropertyProxy(builder, property);
            });
        }
    }

    private static void SetPropertyProxy(CSharpCodeBuilder builder, PropertyDefinition property)
    {
        if (property.Kind == TypeProxyKind.Collection)
        {
            builder.AppendLine($"{property.FieldName} = new global::{RootNamespace}.ChangeTrackingList<{property.ElementType}>(value);");
        }
        else if (property.Kind == TypeProxyKind.Dictionary)
        {
            builder.AppendLine($"{property.FieldName} = new global::{RootNamespace}.ChangeTrackingDictionary<{property.KeyType}, {property.ElementType}>(value);");
        }
        else
        {
            builder.AppendLine($"{property.FieldName} = value;");
        }

        if (property.NotifyPropertyChanging)
        {
            builder.AppendLine($"((global::System.ComponentModel.INotifyPropertyChanging){property.FieldName}).PropertyChanging += OnPropertyChanging;");
        }
        if (property.NotifyPropertyChanged)
        {
            builder.AppendLine($"((global::System.ComponentModel.INotifyPropertyChanged){property.FieldName}).PropertyChanged += OnPropertyChanged;");
        }
        if (property.NotifyCollectionChanged)
        {
            builder.AppendLine($"((global::System.Collections.Specialized.INotifyCollectionChanged){property.FieldName}).CollectionChanged += OnCollectionChanged;");
        }
        if (property.ChangeTracking)
        {
            builder.AppendLine($"__cascadingChanged = ((global::System.ComponentModel.IChangeTracking){property.FieldName}).IsChanged;");
        }
    }

    private static TypeDefinition CreateProxy(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var properties = type.GetMembers().OfType<IPropertySymbol>().Where(x => !x.IsReadOnly && !x.IsStatic && !x.IsWriteOnly && x.IsPartialDefinition);

        TypeDefinition typeProxy = new(type.Name, type.GetNamespace(), type.IsRecord);

        foreach (var property in properties)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var propertyDefinition = CreateProperty(property);
            if (propertyDefinition != null)
            {
                typeProxy.Properties.Add(propertyDefinition);
            }
        }

        if (type.BaseType?.HasAttribute(ChangeTrackingAttribute) == true)
        {
            typeProxy.NotifyPropertyChanging = true;
            typeProxy.NotifyPropertyChanged = true;
            typeProxy.NotifyPropertyChanged = true;
            typeProxy.ChangeTracking = true;
        }
        else
        {
            CheckTypeInterface(type, out bool notifyPropertyChanging, out bool notifyPropertyChanged,out bool notifyCollectionChanged);
            typeProxy.NotifyPropertyChanging = notifyPropertyChanging;
            typeProxy.NotifyPropertyChanged = notifyPropertyChanged;
            typeProxy.NotifyCollectionChanged = notifyCollectionChanged;
            typeProxy.ChangeTracking = false;
        }

        return typeProxy;


        static PropertyDefinition? CreateProperty(IPropertySymbol property)
        {
            var type = property.Type;
            var typeName = property.Type.GetFullName();
            var propertyName = property.Name;

            if (type.IsValueType ||
                type.TypeKind == TypeKind.Struct ||
                type.TypeKind == TypeKind.Enum ||
                type.IsTupleType ||
                typeName == "string")
            {
                return new PropertyDefinition(TypeProxyKind.Value, propertyName, typeName, property.IsRequired)
                {
                    IsInitOnly = property.SetMethod?.IsInitOnly == true,
                };
            }

            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var definition = type.OriginalDefinition.GetFullName();

                if (definition == "global::System.Collections.Generic.IDictionary<TKey, TValue>")
                {
                    return new PropertyDefinition(TypeProxyKind.Dictionary, propertyName, typeName, property.IsRequired)
                    {
                        IsInitOnly = property.SetMethod?.IsInitOnly == true,
                        KeyType = namedType.TypeArguments[0].GetFullName(),
                        ElementType = namedType.TypeArguments[1].GetFullName(),
                        NotifyCollectionChanged = true,
                        NotifyPropertyChanged = true,
                        ChangeTracking = true,
                    };
                }
                else if (definition == "global::System.Collections.Generic.IList<T>" ||
                         definition == "global::System.Collections.Generic.ICollection<T>" ||
                         definition == "global::System.Collections.Generic.IEnumerable<T>")
                {
                    return new PropertyDefinition(TypeProxyKind.Collection, propertyName, typeName, property.IsRequired)
                    {
                        IsInitOnly = property.SetMethod?.IsInitOnly == true,
                        ElementType = namedType.TypeArguments[0].GetFullName(),
                        NotifyCollectionChanged = true,
                        NotifyPropertyChanged = true,
                        ChangeTracking = true,
                    };
                }
            }

            if (type.HasAttribute(ChangeTrackingAttribute))
            {
                return new PropertyDefinition(TypeProxyKind.Object, propertyName, typeName, property.IsRequired)
                {
                    IsInitOnly = property.SetMethod?.IsInitOnly == true,
                    NotifyCollectionChanged = false,
                    NotifyPropertyChanging = true,
                    NotifyPropertyChanged = true,
                    ChangeTracking = true,
                };
            }
            else
            {
                CheckPropertyInterface(type, out bool notifyPropertyChanging, out bool notifyPropertyChanged, out bool notifyCollectionChanged, out bool changeTracking);
                return new PropertyDefinition(TypeProxyKind.Object, propertyName, typeName, property.IsRequired)
                {
                    IsInitOnly = property.SetMethod?.IsInitOnly == true,
                    NotifyCollectionChanged = notifyCollectionChanged,
                    NotifyPropertyChanging = notifyPropertyChanging,
                    NotifyPropertyChanged = notifyPropertyChanged,
                    ChangeTracking = changeTracking,
                };
            }
        }

        static void CheckTypeInterface(ITypeSymbol type, out bool notifyPropertyChanging, out bool notifyPropertyChanged, out bool notifyCollectionChanged)
        {
            notifyPropertyChanging = false;
            notifyPropertyChanged = false;
            notifyCollectionChanged = false;
            foreach (var @interface in type.AllInterfaces)
            {
                var fullName = @interface.GetFullName();

                if (fullName == "global::System.ComponentModel.INotifyPropertyChanging")
                {
                    notifyPropertyChanging = true;
                }
                else if (fullName == "global::System.ComponentModel.INotifyPropertyChanged")
                {
                    notifyPropertyChanged = true;
                }
                else if (fullName == "global::System.Collections.Specialized.INotifyCollectionChanged")
                {
                    notifyCollectionChanged = true;
                }

                if (notifyPropertyChanging && notifyPropertyChanged && notifyCollectionChanged)
                    break;
            }
        }

        static void CheckPropertyInterface(ITypeSymbol type, out bool notifyPropertyChanging, out bool notifyPropertyChanged, out bool notifyCollectionChanged, out bool changeTracking)
        {
            notifyPropertyChanging = false;
            notifyPropertyChanged = false;
            notifyCollectionChanged = false;
            changeTracking = false;
            foreach (var @interface in type.AllInterfaces)
            {
                var fullName = @interface.GetFullName();

                if (fullName == "global::System.ComponentModel.INotifyPropertyChanging")
                {
                    notifyPropertyChanging = true;
                }
                else if (fullName == "global::System.ComponentModel.INotifyPropertyChanged")
                {
                    notifyPropertyChanged = true;
                }
                else if (fullName == "global::System.Collections.Specialized.INotifyCollectionChanged")
                {
                    notifyCollectionChanged = true;
                }
                else if (fullName == "global::System.ComponentModel.IChangeTracking")
                {
                    changeTracking = true;
                }

                if (notifyPropertyChanging && notifyPropertyChanged && notifyCollectionChanged && changeTracking)
                    break;
            }
        }
    }

    private sealed class TypeDefinition(string name, string? ns, bool isRecord)
    {
        public readonly string? Namespace = ns;
        public readonly string Name = name;
        public readonly bool IsRecord = isRecord;

        public bool NotifyPropertyChanged;
        public bool NotifyPropertyChanging;
        public bool NotifyCollectionChanged;
        public bool ChangeTracking;


        public readonly List<PropertyDefinition> Properties = [];
    }

    private sealed class PropertyDefinition
    {
        public PropertyDefinition(TypeProxyKind proxyKind, string propertyName, string type, bool required)
        {
            Kind = proxyKind;
            PropertyName = propertyName;
            Type = type;
            Required = required;

            FieldName = "__" + char.ToLower(PropertyName[0]) + propertyName.Substring(1);
        }

        public readonly string FieldName;
        public readonly string PropertyName;
        public readonly string Type;
        public readonly TypeProxyKind Kind;
        public readonly bool Required;
        public bool IsInitOnly;

        public string? KeyType;
        public string? ElementType;

        public bool NotifyPropertyChanged;
        public bool NotifyPropertyChanging;
        public bool NotifyCollectionChanged;
        public bool ChangeTracking;

    }

    private enum TypeProxyKind
    {
        Value,
        Object,
        Collection,
        Dictionary,
    }
}

