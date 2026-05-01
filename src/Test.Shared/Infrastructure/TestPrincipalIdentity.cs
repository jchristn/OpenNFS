namespace Test.Shared.Infrastructure
{
    using System;

    internal sealed class TestPrincipalIdentity
    {
        public TestPrincipalIdentity(
            string machineName,
            uint userId,
            uint groupId,
            uint[] supplementaryGroupIds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(machineName);
            ArgumentNullException.ThrowIfNull(supplementaryGroupIds);

            MachineName = machineName;
            UserId = userId;
            GroupId = groupId;
            SupplementaryGroupIds = supplementaryGroupIds.AsSpan().ToArray();
        }

        public string MachineName { get; }

        public uint UserId { get; }

        public uint GroupId { get; }

        public uint[] SupplementaryGroupIds { get; }

        public static TestPrincipalIdentity CreateDefaultAuthSys()
        {
            return new TestPrincipalIdentity(
                Environment.MachineName,
                0U,
                0U,
                Array.Empty<uint>());
        }
    }
}
