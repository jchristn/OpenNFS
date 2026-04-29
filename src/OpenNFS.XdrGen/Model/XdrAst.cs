#pragma warning disable CS1572
#pragma warning disable CS1573
#pragma warning disable CS1591

namespace OpenNFS.XdrGen.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// Parsed XDR document.
    /// </summary>
    /// <param name="filePath">Absolute path to the parsed source file.</param>
    /// <param name="definitions">Top-level definitions in source order.</param>
    public sealed record class XdrDocument(string FilePath, IReadOnlyList<XdrDefinition> Definitions);

    /// <summary>
    /// Base type for top-level XDR definitions.
    /// </summary>
    public abstract record class XdrDefinition;

    /// <summary>
    /// Constant definition.
    /// </summary>
    /// <param name="name">Constant identifier.</param>
    /// <param name="valueExpression">Raw constant value expression.</param>
    public sealed record class XdrConstDefinition(string Name, string ValueExpression) : XdrDefinition;

    /// <summary>
    /// Type definition expressed via typedef.
    /// </summary>
    /// <param name="typeSpecifier">Source type being aliased.</param>
    /// <param name="declarator">Alias declarator.</param>
    public sealed record class XdrTypedefDefinition(XdrTypeSpecifier TypeSpecifier, XdrDeclarator Declarator) : XdrDefinition;

    /// <summary>
    /// Top-level enum, struct, or union definition.
    /// </summary>
    /// <param name="typeSpecifier">Defined type specifier.</param>
    public sealed record class XdrTypeDefinition(XdrTypeSpecifier TypeSpecifier) : XdrDefinition;

    /// <summary>
    /// Program definition.
    /// </summary>
    /// <param name="name">Program identifier.</param>
    /// <param name="versions">Contained version declarations.</param>
    /// <param name="valueExpression">Program number expression.</param>
    public sealed record class XdrProgramDefinition(string Name, IReadOnlyList<XdrVersionDefinition> Versions, string ValueExpression) : XdrDefinition;

    /// <summary>
    /// Base type for type specifiers.
    /// </summary>
    public abstract record class XdrTypeSpecifier;

    /// <summary>
    /// Built-in XDR type specifier.
    /// </summary>
    /// <param name="name">Built-in type name as it appears in source.</param>
    public sealed record class XdrBuiltinTypeSpecifier(string Name) : XdrTypeSpecifier;

    /// <summary>
    /// Identifier-based type reference.
    /// </summary>
    /// <param name="name">Referenced type name.</param>
    public sealed record class XdrIdentifierTypeSpecifier(string Name) : XdrTypeSpecifier;

    /// <summary>
    /// Qualified type reference such as <c>struct entry3</c>.
    /// </summary>
    /// <param name="qualifier">Qualifier token.</param>
    /// <param name="name">Referenced type name.</param>
    public sealed record class XdrQualifiedTypeSpecifier(string Qualifier, string Name) : XdrTypeSpecifier;

    /// <summary>
    /// Enum definition usable both inline and top-level.
    /// </summary>
    /// <param name="name">Optional enum name.</param>
    /// <param name="Members">Enum members in source order.</param>
    public sealed record class XdrEnumTypeSpecifier(string? Name, IReadOnlyList<XdrEnumMember> Members) : XdrTypeSpecifier;

    /// <summary>
    /// Struct definition usable both inline and top-level.
    /// </summary>
    /// <param name="name">Optional struct name.</param>
    /// <param name="fields">Struct fields in source order.</param>
    public sealed record class XdrStructTypeSpecifier(string? Name, IReadOnlyList<XdrFieldDeclaration> Fields) : XdrTypeSpecifier;

    /// <summary>
    /// Union definition usable both inline and top-level.
    /// </summary>
    /// <param name="name">Optional union name.</param>
    /// <param name="switchType">Discriminant type.</param>
    /// <param name="switchName">Discriminant identifier.</param>
    /// <param name="arms">Union arms in source order.</param>
    public sealed record class XdrUnionTypeSpecifier(string? Name, XdrTypeSpecifier SwitchType, string SwitchName, IReadOnlyList<XdrUnionArm> Arms) : XdrTypeSpecifier;

    /// <summary>
    /// Enum member definition.
    /// </summary>
    /// <param name="name">Member identifier.</param>
    /// <param name="valueExpression">Raw assigned value expression.</param>
    public sealed record class XdrEnumMember(string Name, string ValueExpression);

    /// <summary>
    /// Field or arm declaration.
    /// </summary>
    /// <param name="typeSpecifier">Field type.</param>
    /// <param name="declarator">Field declarator.</param>
    public sealed record class XdrFieldDeclaration(XdrTypeSpecifier TypeSpecifier, XdrDeclarator Declarator);

    /// <summary>
    /// Declarator kind.
    /// </summary>
    public enum XdrDeclaratorKind
    {
        Identifier,
        Pointer,
        FixedArray,
        VariableArray,
    }

    /// <summary>
    /// XDR declarator.
    /// </summary>
    /// <param name="Identifier">Declared identifier.</param>
    /// <param name="Kind">Declarator form.</param>
    /// <param name="BoundExpression">Optional array or max-length expression.</param>
    public sealed record class XdrDeclarator(string Identifier, XdrDeclaratorKind Kind, string? BoundExpression);

    /// <summary>
    /// Union arm.
    /// </summary>
    /// <param name="CaseLabels">Case labels associated with the arm.</param>
    /// <param name="IsDefault">True when the arm is selected by <c>default</c>.</param>
    /// <param name="Declaration">Optional arm declaration. Null represents <c>void;</c>.</param>
    public sealed record class XdrUnionArm(IReadOnlyList<string> CaseLabels, bool IsDefault, XdrFieldDeclaration? Declaration);

    /// <summary>
    /// Program version definition.
    /// </summary>
    /// <param name="name">Version identifier.</param>
    /// <param name="procedures">Procedures in source order.</param>
    /// <param name="valueExpression">Version number expression.</param>
    public sealed record class XdrVersionDefinition(string Name, IReadOnlyList<XdrProcedureDefinition> Procedures, string ValueExpression);

    /// <summary>
    /// Procedure definition inside a version block.
    /// </summary>
    /// <param name="returnType">Return type specifier.</param>
    /// <param name="name">Procedure identifier.</param>
    /// <param name="argumentType">Argument type specifier.</param>
    /// <param name="valueExpression">Procedure number expression.</param>
    public sealed record class XdrProcedureDefinition(XdrTypeSpecifier ReturnType, string Name, XdrTypeSpecifier ArgumentType, string ValueExpression);
}

#pragma warning restore CS1591
#pragma warning restore CS1573
#pragma warning restore CS1572
