namespace OpenNFS.Protocol.V3.Procedures
{
    internal sealed class Nfs3ProcedureDescriptor
    {
        internal Nfs3ProcedureDescriptor(uint procedureNumber, string name)
        {
            ProcedureNumber = procedureNumber;
            Name = name;
        }

        internal uint ProcedureNumber { get; }

        internal string Name { get; }
    }
}
