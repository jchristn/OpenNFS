namespace OpenNFS.XdrGen.Emission
{
    using System;
    using System.IO;
    using OpenNFS.XdrGen.Model;

    internal sealed class XdrEmissionCodecMemberSupport
    {
        private readonly XdrEmissionContext context;

        internal XdrEmissionCodecMemberSupport(XdrEmissionContext context)
        {
            this.context = context;
        }

        internal void EmitWriteMember(
            CodeWriter writer,
            string memberAccess,
            string memberContext,
            PropertyDefinition property,
            string writerIdentifier,
            int indentLevel)
        {
            if (property.Declarator.Kind == XdrDeclaratorKind.Pointer)
            {
                if (property.ValueDescriptor.IsValueType)
                {
                    writer.AppendLine(indentLevel, "if (" + memberAccess + ".HasValue)");
                    writer.AppendLine(indentLevel, "{");
                    writer.AppendLine(indentLevel + 1, writerIdentifier + ".WriteBoolean(true);");
                    EmitWriteScalarCore(
                        writer,
                        "XdrGeneratedCodec.RequireValue(" + memberAccess + ", \"" + memberContext + "\")",
                        memberContext,
                        property,
                        writerIdentifier,
                        indentLevel + 1);
                    writer.AppendLine(indentLevel, "}");
                    writer.AppendLine(indentLevel, "else");
                    writer.AppendLine(indentLevel, "{");
                    writer.AppendLine(indentLevel + 1, writerIdentifier + ".WriteBoolean(false);");
                    writer.AppendLine(indentLevel, "}");
                }
                else
                {
                    writer.AppendLine(indentLevel, "if (" + memberAccess + " is null)");
                    writer.AppendLine(indentLevel, "{");
                    writer.AppendLine(indentLevel + 1, writerIdentifier + ".WriteBoolean(false);");
                    writer.AppendLine(indentLevel, "}");
                    writer.AppendLine(indentLevel, "else");
                    writer.AppendLine(indentLevel, "{");
                    writer.AppendLine(indentLevel + 1, writerIdentifier + ".WriteBoolean(true);");
                    EmitWriteScalarCore(
                        writer,
                        "XdrGeneratedCodec.RequireReference(" + memberAccess + ", \"" + memberContext + "\")",
                        memberContext,
                        property,
                        writerIdentifier,
                        indentLevel + 1);
                    writer.AppendLine(indentLevel, "}");
                }

                return;
            }

            if ((property.Declarator.Kind == XdrDeclaratorKind.FixedArray || property.Declarator.Kind == XdrDeclaratorKind.VariableArray)
                && property.ValueDescriptor.Kind != CodecValueKind.Opaque
                && property.ValueDescriptor.Kind != CodecValueKind.String)
            {
                string arrayValueVariable = XdrEmissionContext.EscapeIdentifier(property.Name + "Value");
                string arrayTypeName = property.TypeReference.BaseTypeName;
                string requireArrayExpression = property.Declarator.Kind == XdrDeclaratorKind.FixedArray
                    ? "XdrGeneratedCodec.RequireFixedLength(" + memberAccess + ", " + context.Symbols.ResolveFixedCountLiteral(property.Declarator.BoundExpression!) + ", \"" + memberContext + "\")"
                    : "XdrGeneratedCodec.RequireReference(" + memberAccess + ", \"" + memberContext + "\")";
                writer.AppendLine(indentLevel, arrayTypeName + " " + arrayValueVariable + " = " + requireArrayExpression + ";");

                string arrayMethodName = property.Declarator.Kind == XdrDeclaratorKind.FixedArray ? "WriteFixedArray" : "WriteVariableArray";
                string arrayCountArgument = property.Declarator.Kind == XdrDeclaratorKind.FixedArray
                    ? "expectedCount: " + context.Symbols.ResolveFixedCountLiteral(property.Declarator.BoundExpression!)
                    : "maximumCount: " + context.Symbols.ResolveBoundArgumentLiteral(property.Declarator.BoundExpression);

                writer.AppendLine(indentLevel, writerIdentifier + "." + arrayMethodName + "<" + property.ValueDescriptor.TypeName + ">(");
                writer.AppendLine(indentLevel + 1, arrayValueVariable + ",");
                writer.AppendLine(indentLevel + 1, arrayCountArgument + ",");
                writer.AppendLine(indentLevel + 1, "writeElement: static (xdrWriter, element) =>");
                writer.AppendLine(indentLevel + 1, "{");
                EmitWriteArrayElement(writer, property, memberContext + "[]", "element", "xdrWriter", indentLevel + 2);
                writer.AppendLine(indentLevel + 1, "});");
                return;
            }

            string scalarValueExpression = memberAccess;
            if (property.ValueDescriptor.IsValueType && property.TypeReference.IsNullable)
            {
                scalarValueExpression = "XdrGeneratedCodec.RequireValue(" + memberAccess + ", \"" + memberContext + "\")";
            }

            EmitWriteScalarCore(writer, scalarValueExpression, memberContext, property, writerIdentifier, indentLevel);
        }

        internal void EmitReadMember(
            CodeWriter writer,
            string targetAccess,
            PropertyDefinition property,
            string readerIdentifier,
            int indentLevel)
        {
            if (property.Declarator.Kind == XdrDeclaratorKind.Pointer)
            {
                writer.AppendLine(indentLevel, "if (" + readerIdentifier + ".ReadBoolean())");
                writer.AppendLine(indentLevel, "{");
                writer.AppendLine(indentLevel + 1, targetAccess + " = " + BuildReadScalarExpression(property, readerIdentifier) + ";");
                writer.AppendLine(indentLevel, "}");
                writer.AppendLine(indentLevel, "else");
                writer.AppendLine(indentLevel, "{");
                writer.AppendLine(indentLevel + 1, targetAccess + " = null;");
                writer.AppendLine(indentLevel, "}");
                return;
            }

            if ((property.Declarator.Kind == XdrDeclaratorKind.FixedArray || property.Declarator.Kind == XdrDeclaratorKind.VariableArray)
                && property.ValueDescriptor.Kind != CodecValueKind.Opaque
                && property.ValueDescriptor.Kind != CodecValueKind.String)
            {
                string arrayMethodName = property.Declarator.Kind == XdrDeclaratorKind.FixedArray ? "ReadFixedArray" : "ReadVariableArray";
                string arrayCountArgument = property.Declarator.Kind == XdrDeclaratorKind.FixedArray
                    ? "expectedCount: " + context.Symbols.ResolveFixedCountLiteral(property.Declarator.BoundExpression!)
                    : "maximumCount: " + context.Symbols.ResolveBoundArgumentLiteral(property.Declarator.BoundExpression);

                writer.AppendLine(indentLevel, targetAccess + " = XdrGeneratedCodec.ToArray(");
                writer.AppendLine(indentLevel + 1, readerIdentifier + "." + arrayMethodName + "<" + property.ValueDescriptor.TypeName + ">(");
                writer.AppendLine(indentLevel + 2, arrayCountArgument + ",");
                writer.AppendLine(indentLevel + 2, "readElement: static xdrReader =>");
                writer.AppendLine(indentLevel + 2, "{");
                writer.AppendLine(indentLevel + 3, "return " + BuildReadArrayElementExpression(property, "xdrReader") + ";");
                writer.AppendLine(indentLevel + 2, "}));");
                return;
            }

            writer.AppendLine(indentLevel, targetAccess + " = " + BuildReadScalarExpression(property, readerIdentifier) + ";");
        }

        internal void EmitWriteScalarCore(
            CodeWriter writer,
            string valueExpression,
            string memberContext,
            PropertyDefinition property,
            string writerIdentifier,
            int indentLevel)
        {
            switch (property.ValueDescriptor.Kind)
            {
                case CodecValueKind.Bool:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteBoolean(" + valueExpression + ");");
                    break;

                case CodecValueKind.Int32:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteInt32(" + BuildInt32WriteExpression(property, valueExpression) + ");");
                    break;

                case CodecValueKind.UInt32:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteUInt32(" + valueExpression + ");");
                    break;

                case CodecValueKind.Int64:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteInt64(" + valueExpression + ");");
                    break;

                case CodecValueKind.UInt64:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteUInt64(" + valueExpression + ");");
                    break;

                case CodecValueKind.Float:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteSingle(" + valueExpression + ");");
                    break;

                case CodecValueKind.Double:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteDouble(" + valueExpression + ");");
                    break;

                case CodecValueKind.Opaque:
                    if (property.Declarator.Kind == XdrDeclaratorKind.FixedArray)
                    {
                        writer.AppendLine(
                            indentLevel,
                            "byte[] fixedOpaqueValue = XdrGeneratedCodec.RequireFixedLength(" + valueExpression + ", " + context.Symbols.ResolveFixedCountLiteral(property.Declarator.BoundExpression!) + ", \"" + memberContext + "\");");
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteFixedOpaque(fixedOpaqueValue);");
                    }
                    else
                    {
                        string opaqueValueExpression = "XdrGeneratedCodec.RequireReference(" + valueExpression + ", \"" + memberContext + "\")";
                        string maximumLengthArgument = property.Declarator.Kind == XdrDeclaratorKind.VariableArray
                            ? ", maximumLength: " + context.Symbols.ResolveBoundArgumentLiteral(property.Declarator.BoundExpression)
                            : string.Empty;
                        writer.AppendLine(indentLevel, writerIdentifier + ".WriteVariableOpaque(" + opaqueValueExpression + maximumLengthArgument + ");");
                    }

                    break;

                case CodecValueKind.String:
                {
                    string stringValueExpression = "XdrGeneratedCodec.RequireReference(" + valueExpression + ", \"" + memberContext + "\")";
                    string maximumStringArgument = property.Declarator.Kind == XdrDeclaratorKind.VariableArray
                        ? ", maximumUtf8ByteLength: " + context.Symbols.ResolveBoundArgumentLiteral(property.Declarator.BoundExpression)
                        : string.Empty;
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteString(" + stringValueExpression + maximumStringArgument + ");");
                    break;
                }

                case CodecValueKind.Enum:
                    writer.AppendLine(indentLevel, XdrEmissionContext.EscapeIdentifier(property.ValueDescriptor.TypeName + "_XdrCodec") + ".Write(" + writerIdentifier + ", " + valueExpression + ");");
                    break;

                case CodecValueKind.Class:
                    writer.AppendLine(
                        indentLevel,
                        "XdrGeneratedCodec.RequireReference(" + valueExpression + ", \"" + memberContext + "\").WriteTo(" + writerIdentifier + ");");
                    break;

                default:
                    throw new InvalidDataException("Unsupported XDR codec kind for scalar write: " + property.ValueDescriptor.Kind);
            }
        }

        internal string BuildReadScalarExpression(PropertyDefinition property, string readerIdentifier)
        {
            return property.ValueDescriptor.Kind switch
            {
                CodecValueKind.Bool => readerIdentifier + ".ReadBoolean()",
                CodecValueKind.Int32 => readerIdentifier + ".ReadInt32()",
                CodecValueKind.UInt32 => readerIdentifier + ".ReadUInt32()",
                CodecValueKind.Int64 => readerIdentifier + ".ReadInt64()",
                CodecValueKind.UInt64 => readerIdentifier + ".ReadUInt64()",
                CodecValueKind.Float => readerIdentifier + ".ReadSingle()",
                CodecValueKind.Double => readerIdentifier + ".ReadDouble()",
                CodecValueKind.Opaque when property.Declarator.Kind == XdrDeclaratorKind.FixedArray => readerIdentifier + ".ReadFixedOpaque(" + context.Symbols.ResolveFixedCountLiteral(property.Declarator.BoundExpression!) + ")",
                CodecValueKind.Opaque => readerIdentifier + ".ReadVariableOpaque(" + context.Symbols.BuildMaximumArgumentLiteral(property.Declarator.BoundExpression) + ")",
                CodecValueKind.String => readerIdentifier + ".ReadString(" + context.Symbols.BuildMaximumArgumentLiteral(property.Declarator.BoundExpression) + ")",
                CodecValueKind.Enum => XdrEmissionContext.EscapeIdentifier(property.ValueDescriptor.TypeName + "_XdrCodec") + ".Read(" + readerIdentifier + ")",
                CodecValueKind.Class => property.ValueDescriptor.TypeName + ".ReadFrom(" + readerIdentifier + ")",
                _ => throw new InvalidDataException("Unsupported XDR codec kind for scalar read: " + property.ValueDescriptor.Kind),
            };
        }

        private void EmitWriteArrayElement(
            CodeWriter writer,
            PropertyDefinition property,
            string memberContext,
            string elementExpression,
            string writerIdentifier,
            int indentLevel)
        {
            switch (property.ValueDescriptor.Kind)
            {
                case CodecValueKind.Bool:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteBoolean(" + elementExpression + ");");
                    break;

                case CodecValueKind.Int32:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteInt32(" + BuildInt32WriteExpression(property, elementExpression) + ");");
                    break;

                case CodecValueKind.UInt32:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteUInt32(" + elementExpression + ");");
                    break;

                case CodecValueKind.Int64:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteInt64(" + elementExpression + ");");
                    break;

                case CodecValueKind.UInt64:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteUInt64(" + elementExpression + ");");
                    break;

                case CodecValueKind.Float:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteSingle(" + elementExpression + ");");
                    break;

                case CodecValueKind.Double:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteDouble(" + elementExpression + ");");
                    break;

                case CodecValueKind.Enum:
                    writer.AppendLine(indentLevel, XdrEmissionContext.EscapeIdentifier(property.ValueDescriptor.TypeName + "_XdrCodec") + ".Write(" + writerIdentifier + ", " + elementExpression + ");");
                    break;

                case CodecValueKind.Class:
                    writer.AppendLine(indentLevel, "XdrGeneratedCodec.RequireReference(" + elementExpression + ", \"" + memberContext + "\").WriteTo(" + writerIdentifier + ");");
                    break;

                case CodecValueKind.String:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteString(XdrGeneratedCodec.RequireReference(" + elementExpression + ", \"" + memberContext + "\"));");
                    break;

                case CodecValueKind.Opaque:
                    writer.AppendLine(indentLevel, writerIdentifier + ".WriteVariableOpaque(XdrGeneratedCodec.RequireReference(" + elementExpression + ", \"" + memberContext + "\"));");
                    break;

                default:
                    throw new InvalidDataException("Unsupported array element codec kind: " + property.ValueDescriptor.Kind);
            }
        }

        private string BuildReadArrayElementExpression(PropertyDefinition property, string readerIdentifier)
        {
            return property.ValueDescriptor.Kind switch
            {
                CodecValueKind.Bool => readerIdentifier + ".ReadBoolean()",
                CodecValueKind.Int32 => readerIdentifier + ".ReadInt32()",
                CodecValueKind.UInt32 => readerIdentifier + ".ReadUInt32()",
                CodecValueKind.Int64 => readerIdentifier + ".ReadInt64()",
                CodecValueKind.UInt64 => readerIdentifier + ".ReadUInt64()",
                CodecValueKind.Float => readerIdentifier + ".ReadSingle()",
                CodecValueKind.Double => readerIdentifier + ".ReadDouble()",
                CodecValueKind.Enum => XdrEmissionContext.EscapeIdentifier(property.ValueDescriptor.TypeName + "_XdrCodec") + ".Read(" + readerIdentifier + ")",
                CodecValueKind.Class => property.ValueDescriptor.TypeName + ".ReadFrom(" + readerIdentifier + ")",
                CodecValueKind.String => readerIdentifier + ".ReadString()",
                CodecValueKind.Opaque => readerIdentifier + ".ReadVariableOpaque()",
                _ => throw new InvalidDataException("Unsupported array element codec kind for read: " + property.ValueDescriptor.Kind),
            };
        }

        private static string BuildInt32WriteExpression(PropertyDefinition property, string valueExpression)
        {
            if (string.Equals(property.ValueDescriptor.TypeName, "long", StringComparison.Ordinal))
            {
                return "checked((int)(" + valueExpression + "))";
            }

            return valueExpression;
        }
    }
}
