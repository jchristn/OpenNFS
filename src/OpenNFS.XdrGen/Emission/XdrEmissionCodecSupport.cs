namespace OpenNFS.XdrGen.Emission
{
    using System.Collections.Generic;

    internal sealed class XdrEmissionCodecSupport
    {
        private readonly XdrEmissionCodecTypeEmitter typeEmitter;

        internal XdrEmissionCodecSupport(XdrEmissionContext context)
        {
            typeEmitter = new XdrEmissionCodecTypeEmitter(context);
        }

        internal void EmitEnumCodecType(string typeName)
        {
            typeEmitter.EmitEnumCodecType(typeName);
        }

        internal void AppendAliasCodecMembers(CodeWriter writer, string typeName, PropertyDefinition valueProperty)
        {
            typeEmitter.AppendAliasCodecMembers(writer, typeName, valueProperty);
        }

        internal void AppendStructCodecMembers(CodeWriter writer, string typeName, IReadOnlyList<PropertyDefinition> properties)
        {
            typeEmitter.AppendStructCodecMembers(writer, typeName, properties);
        }

        internal void AppendUnionCodecMembers(
            CodeWriter writer,
            string typeName,
            PropertyDefinition discriminantProperty,
            IReadOnlyList<UnionArmDefinition> arms)
        {
            typeEmitter.AppendUnionCodecMembers(writer, typeName, discriminantProperty, arms);
        }
    }
}
