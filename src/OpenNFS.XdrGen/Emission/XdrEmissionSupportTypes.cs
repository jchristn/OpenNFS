namespace OpenNFS.XdrGen.Emission
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using OpenNFS.XdrGen.Model;

    internal sealed class PropertyDefinition
    {
        public PropertyDefinition(
            string name,
            string wireName,
            CSharpTypeReference typeReference,
            CodecValueDescriptor valueDescriptor,
            XdrDeclarator declarator,
            string summary)
        {
            Name = name;
            WireName = wireName;
            TypeReference = typeReference;
            ValueDescriptor = valueDescriptor;
            Declarator = declarator;
            Summary = summary;
        }

        public string Name { get; }

        public string WireName { get; }

        public CSharpTypeReference TypeReference { get; }

        public CodecValueDescriptor ValueDescriptor { get; }

        public XdrDeclarator Declarator { get; }

        public string TypeName => TypeReference.DisplayName;

        public string Summary { get; }
    }

    internal sealed class UnionArmDefinition
    {
        public UnionArmDefinition(PropertyDefinition property, IReadOnlyList<string> caseLabels, bool isDefault)
        {
            Property = property;
            CaseLabels = caseLabels;
            IsDefault = isDefault;
        }

        public PropertyDefinition Property { get; }

        public IReadOnlyList<string> CaseLabels { get; }

        public bool IsDefault { get; }
    }

    internal sealed class CodecValueDescriptor
    {
        public CodecValueDescriptor(CodecValueKind kind, string typeName, bool isValueType)
        {
            Kind = kind;
            TypeName = typeName;
            IsValueType = isValueType;
        }

        public CodecValueKind Kind { get; }

        public string TypeName { get; }

        public bool IsValueType { get; }
    }

    internal enum CodecValueKind
    {
        Bool,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float,
        Double,
        Opaque,
        String,
        Enum,
        Class,
    }

    internal enum EmittedTypeKind
    {
        Enum,
        Class,
    }

    internal sealed class CSharpTypeReference
    {
        public CSharpTypeReference(string baseTypeName, bool isValueType, bool isNullable = false)
        {
            BaseTypeName = baseTypeName;
            IsValueType = isValueType;
            IsNullable = isNullable;
        }

        public string BaseTypeName { get; }

        public bool IsValueType { get; }

        public bool IsNullable { get; }

        public string DisplayName
        {
            get
            {
                if (!IsNullable)
                {
                    return BaseTypeName;
                }

                return BaseTypeName + "?";
            }
        }

        public string NonNullableDisplayName => BaseTypeName;

        public CSharpTypeReference DefaultForProperty()
        {
            if (IsValueType)
            {
                return this;
            }

            return new CSharpTypeReference(BaseTypeName, isValueType: false, isNullable: true);
        }

        public CSharpTypeReference MakeNullable()
        {
            if (IsNullable)
            {
                return this;
            }

            return new CSharpTypeReference(BaseTypeName, IsValueType, isNullable: true);
        }
    }

    internal sealed class CodeWriter
    {
        private readonly StringBuilder builder;

        public CodeWriter()
        {
            builder = new StringBuilder();
        }

        public void AppendLine()
        {
            builder.Append('\n');
        }

        public void AppendLine(string line)
        {
            builder.Append(line);
            builder.Append('\n');
        }

        public void AppendLine(int indentLevel, string line)
        {
            builder.Append(' ', indentLevel * 4);
            builder.Append(line);
            builder.Append('\n');
        }

        public void RemoveTrailingBlankLine()
        {
            string value = builder.ToString();
            while (value.EndsWith("\n\n", StringComparison.Ordinal))
            {
                value = value.Substring(0, value.Length - 1);
            }

            builder.Clear();
            builder.Append(value);
        }

        public override string ToString()
        {
            return builder.ToString();
        }
    }
}
