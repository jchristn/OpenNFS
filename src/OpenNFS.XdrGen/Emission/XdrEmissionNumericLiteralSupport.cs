namespace OpenNFS.XdrGen.Emission
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    internal sealed class XdrEmissionNumericLiteralSupport
    {
        private readonly XdrEmissionContext context;

        internal XdrEmissionNumericLiteralSupport(XdrEmissionContext context)
        {
            this.context = context;
        }

        internal string ResolveUnionCaseLabel(PropertyDefinition discriminantProperty, string caseLabel)
        {
            if (discriminantProperty.ValueDescriptor.Kind == CodecValueKind.Bool)
            {
                if (string.Equals(caseLabel, "TRUE", StringComparison.OrdinalIgnoreCase))
                {
                    return "true";
                }

                if (string.Equals(caseLabel, "FALSE", StringComparison.OrdinalIgnoreCase))
                {
                    return "false";
                }

                ulong caseValue = ResolveExpressionValue(caseLabel);
                return caseValue == 0 ? "false" : "true";
            }

            if (discriminantProperty.ValueDescriptor.Kind == CodecValueKind.Enum)
            {
                string trimmedCaseLabel = caseLabel.Trim();
                if (TryParseUnsignedIntegerLiteral(trimmedCaseLabel, out _)
                    || context.NumericSymbols.ContainsKey(trimmedCaseLabel))
                {
                    return "(" + discriminantProperty.ValueDescriptor.TypeName + ")" + ResolveExpressionLiteral(trimmedCaseLabel);
                }

                return discriminantProperty.ValueDescriptor.TypeName + "." + XdrEmissionContext.EscapeIdentifier(trimmedCaseLabel);
            }

            return ResolveExpressionLiteral(caseLabel);
        }

        internal string ResolveBoundArgumentLiteral(string? boundExpression)
        {
            return boundExpression is null ? "null" : ResolveExpressionLiteral(boundExpression);
        }

        internal string BuildMaximumArgumentLiteral(string? boundExpression)
        {
            return boundExpression is null ? string.Empty : ResolveExpressionLiteral(boundExpression);
        }

        internal string ResolveFixedCountLiteral(string boundExpression)
        {
            ulong value = ResolveExpressionValue(boundExpression);
            if (value > int.MaxValue)
            {
                throw new InvalidDataException("The fixed-length XDR bound '" + boundExpression + "' exceeds the supported CLR array length limit.");
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }

        internal ulong ResolveExpressionValue(string expression)
        {
            return ResolveExpressionValue(expression, new HashSet<string>(StringComparer.Ordinal));
        }

        internal string ResolveNumericLiteral(string symbolName)
        {
            if (context.ResolvedNumericLiterals.TryGetValue(symbolName, out string? existingLiteral))
            {
                return existingLiteral;
            }

            HashSet<string> stack = new HashSet<string>(StringComparer.Ordinal);
            string resolvedLiteral = ResolveNumericLiteral(symbolName, stack);
            context.ResolvedNumericLiterals.Add(symbolName, resolvedLiteral);
            return resolvedLiteral;
        }

        internal string ResolveExpressionLiteral(string expression)
        {
            return ResolveExpressionLiteral(expression, new HashSet<string>(StringComparer.Ordinal));
        }

        private ulong ResolveExpressionValue(string expression, ISet<string> stack)
        {
            string trimmedExpression = expression.Trim();
            if (context.NumericSymbols.TryGetValue(trimmedExpression, out string? symbolExpression))
            {
                if (!stack.Add(trimmedExpression))
                {
                    throw new InvalidDataException("The XDR numeric symbol '" + trimmedExpression + "' contains a circular reference.");
                }

                ulong value = ResolveExpressionValue(symbolExpression, stack);
                stack.Remove(trimmedExpression);
                return value;
            }

            if (TryParseUnsignedIntegerLiteral(trimmedExpression, out ulong literalValue))
            {
                return literalValue;
            }

            if (TryResolveExternalNumericSymbol(trimmedExpression, out ulong externalValue))
            {
                return externalValue;
            }

            throw new InvalidDataException("Unsupported XDR numeric expression for codec emission: '" + trimmedExpression + "'.");
        }

        private string ResolveNumericLiteral(string symbolName, ISet<string> stack)
        {
            if (!context.NumericSymbols.TryGetValue(symbolName, out string? expression))
            {
                throw new InvalidDataException("The XDR numeric symbol '" + symbolName + "' was not collected for emission.");
            }

            if (!stack.Add(symbolName))
            {
                throw new InvalidDataException("The XDR numeric symbol '" + symbolName + "' contains a circular reference.");
            }

            string literal = ResolveExpressionLiteral(expression, stack);
            stack.Remove(symbolName);
            return literal;
        }

        private string ResolveExpressionLiteral(string expression, ISet<string> stack)
        {
            string trimmedExpression = expression.Trim();

            if (context.NumericSymbols.ContainsKey(trimmedExpression))
            {
                if (context.ResolvedNumericLiterals.TryGetValue(trimmedExpression, out string? existingLiteral))
                {
                    return existingLiteral;
                }

                string resolvedLiteral = ResolveNumericLiteral(trimmedExpression, stack);
                context.ResolvedNumericLiterals[trimmedExpression] = resolvedLiteral;
                return resolvedLiteral;
            }

            if (TryParseUnsignedIntegerLiteral(trimmedExpression, out ulong value))
            {
                return FormatUnsignedIntegerLiteral(value);
            }

            if (TryResolveExternalNumericSymbol(trimmedExpression, out ulong externalValue))
            {
                return FormatUnsignedIntegerLiteral(externalValue);
            }

            throw new InvalidDataException("Unsupported XDR numeric expression for C# emission: '" + trimmedExpression + "'.");
        }

        private static bool TryParseUnsignedIntegerLiteral(string expression, out ulong value)
        {
            if (expression.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ulong.TryParse(
                    expression.Substring(2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out value);
            }

            return ulong.TryParse(expression, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryResolveExternalNumericSymbol(string expression, out ulong value)
        {
            switch (expression)
            {
                case "AUTH_NONE":
                    value = 0;
                    return true;

                case "AUTH_SYS":
                    value = 1;
                    return true;

                case "AUTH_SHORT":
                    value = 2;
                    return true;

                case "AUTH_DH":
                    value = 3;
                    return true;

                case "RPCSEC_GSS":
                    value = 6;
                    return true;

                default:
                    value = 0;
                    return false;
            }
        }

        private static string FormatUnsignedIntegerLiteral(ulong value)
        {
            if (value <= int.MaxValue)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }

            if (value <= uint.MaxValue)
            {
                return value.ToString(CultureInfo.InvariantCulture) + "U";
            }

            if (value <= long.MaxValue)
            {
                return value.ToString(CultureInfo.InvariantCulture) + "L";
            }

            return value.ToString(CultureInfo.InvariantCulture) + "UL";
        }
    }
}
