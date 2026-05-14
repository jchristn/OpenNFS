namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NfsV3Generated = OpenNFS.Protocol.V3.Generated;
    using NfsV41Generated = OpenNFS.Protocol.V41.Generated;
    using RpcGenerated = OpenNFS.Rpc.Generated;
    using OpenNFS.Rpc.Xdr;
    using OpenNFS.XdrGen.Model;
    using OpenNFS.XdrGen.Parsing;
    using OpenNFS.XdrGen;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;
    using static Test.Shared.GeneratorSuiteSupport;

    /// <summary>
    /// Scalar, union, and decode-bounds runtime XDR suites.
    /// </summary>
    internal static class GeneratorRuntimeXdrCases
    {
        internal static IReadOnlyList<TestCaseDescriptor> CreateCases()
        {
            return new TestCaseDescriptor[]
            {
                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "ScalarRoundTrip",
                        displayName: "XDR runtime round-trips scalar, opaque, string, and array values",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            XdrWriter writer = new XdrWriter();
                            byte[] expectedFixedOpaque = new byte[] { 0x01, 0x02, 0x03 };
                            byte[] expectedVariableOpaque = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
                            int[] expectedArray = new int[] { 7, -8, 9 };

                            writer.WriteBoolean(true);
                            writer.WriteInt32(-17);
                            writer.WriteUInt32(19);
                            writer.WriteInt64(-1234567890123456789L);
                            writer.WriteUInt64(12345678901234567890UL);
                            writer.WriteSingle(3.25F);
                            writer.WriteDouble(-12.5D);
                            writer.WriteFixedOpaque(expectedFixedOpaque);
                            writer.WriteVariableOpaque(expectedVariableOpaque, maximumLength: 8);
                            writer.WriteString("nfs-test", maximumUtf8ByteLength: 16);
                            writer.WriteVariableArray<int>(
                                expectedArray,
                                maximumCount: 4,
                                writeElement: static (xdrWriter, value) => xdrWriter.WriteInt32(value));

                            byte[] encoded = writer.ToArray();
                            if ((encoded.Length % 4) != 0)
                            {
                                throw new InvalidOperationException("Expected XDR output length to remain four-byte aligned.");
                            }

                            XdrReader reader = new XdrReader(encoded);
                            if (!reader.ReadBoolean())
                            {
                                throw new InvalidOperationException("Expected round-tripped boolean value to be true.");
                            }

                            if (reader.ReadInt32() != -17)
                            {
                                throw new InvalidOperationException("Expected round-tripped Int32 value to match.");
                            }

                            if (reader.ReadUInt32() != 19)
                            {
                                throw new InvalidOperationException("Expected round-tripped UInt32 value to match.");
                            }

                            if (reader.ReadInt64() != -1234567890123456789L)
                            {
                                throw new InvalidOperationException("Expected round-tripped Int64 value to match.");
                            }

                            if (reader.ReadUInt64() != 12345678901234567890UL)
                            {
                                throw new InvalidOperationException("Expected round-tripped UInt64 value to match.");
                            }

                            if (reader.ReadSingle() != 3.25F)
                            {
                                throw new InvalidOperationException("Expected round-tripped single value to match.");
                            }

                            if (reader.ReadDouble() != -12.5D)
                            {
                                throw new InvalidOperationException("Expected round-tripped double value to match.");
                            }

                            byte[] actualFixedOpaque = reader.ReadFixedOpaque(3);
                            if (!actualFixedOpaque.SequenceEqual(expectedFixedOpaque))
                            {
                                throw new InvalidOperationException("Expected fixed opaque payload to round-trip exactly.");
                            }

                            byte[] actualVariableOpaque = reader.ReadVariableOpaque(maximumLength: 8);
                            if (!actualVariableOpaque.SequenceEqual(expectedVariableOpaque))
                            {
                                throw new InvalidOperationException("Expected variable opaque payload to round-trip exactly.");
                            }

                            if (!string.Equals(reader.ReadString(maximumUtf8ByteLength: 16), "nfs-test", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected string payload to round-trip exactly.");
                            }

                            IReadOnlyList<int> actualArray = reader.ReadVariableArray<int>(
                                maximumCount: 4,
                                readElement: static xdrReader => xdrReader.ReadInt32());
                            if (actualArray.Count != expectedArray.Length)
                            {
                                throw new InvalidOperationException("Expected array payload length to round-trip exactly.");
                            }

                            for (int index = 0; index < expectedArray.Length; index++)
                            {
                                if (actualArray[index] != expectedArray[index])
                                {
                                    throw new InvalidOperationException("Expected array payload element " + index + " to round-trip exactly.");
                                }
                            }

                            reader.EnsureFullyConsumed();
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "UnionRoundTrip",
                        displayName: "XDR runtime round-trips discriminated unions",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            static void WriteUnionArm(XdrWriter writer, uint discriminant)
                            {
                                switch (discriminant)
                                {
                                    case 0:
                                        writer.WriteString("alpha");
                                        break;
                                    case 1:
                                        writer.WriteUInt64(42);
                                        break;
                                    case 2:
                                        break;
                                    default:
                                        throw new InvalidOperationException("Unexpected discriminant " + discriminant + " during union write.");
                                }
                            }

                            static string ReadUnionArm(XdrReader reader, uint discriminant)
                            {
                                switch (discriminant)
                                {
                                    case 0:
                                        return "string:" + reader.ReadString();
                                    case 1:
                                        return "number:" + reader.ReadUInt64();
                                    case 2:
                                        return "void";
                                    default:
                                        throw new InvalidOperationException("Unexpected discriminant " + discriminant + " during union read.");
                                }
                            }

                            XdrWriter writer = new XdrWriter();
                            writer.WriteDiscriminatedUnion<uint>(
                                0,
                                writeDiscriminant: static (xdrWriter, value) => xdrWriter.WriteUInt32(value),
                                writeArm: WriteUnionArm);
                            writer.WriteDiscriminatedUnion<uint>(
                                1,
                                writeDiscriminant: static (xdrWriter, value) => xdrWriter.WriteUInt32(value),
                                writeArm: WriteUnionArm);
                            writer.WriteDiscriminatedUnion<uint>(
                                2,
                                writeDiscriminant: static (xdrWriter, value) => xdrWriter.WriteUInt32(value),
                                writeArm: WriteUnionArm);

                            XdrReader reader = new XdrReader(writer.ToArray());
                            string first = reader.ReadDiscriminatedUnion<uint, string>(
                                readDiscriminant: static xdrReader => xdrReader.ReadUInt32(),
                                readArm: ReadUnionArm);
                            string second = reader.ReadDiscriminatedUnion<uint, string>(
                                readDiscriminant: static xdrReader => xdrReader.ReadUInt32(),
                                readArm: ReadUnionArm);
                            string third = reader.ReadDiscriminatedUnion<uint, string>(
                                readDiscriminant: static xdrReader => xdrReader.ReadUInt32(),
                                readArm: ReadUnionArm);

                            if (!string.Equals(first, "string:alpha", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected string union arm to round-trip exactly.");
                            }

                            if (!string.Equals(second, "number:42", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected numeric union arm to round-trip exactly.");
                            }

                            if (!string.Equals(third, "void", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("Expected void union arm to round-trip exactly.");
                            }

                            reader.EnsureFullyConsumed();
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "RpcXdrSuites",
                        caseId: "DecodeBoundsFailures",
                        displayName: "XDR runtime rejects truncated and over-limit payloads",
                        tags: new List<string> { TestCategories.Unit, TestCategories.Automated },
                        executeAsync: _ =>
                        {
                            byte[] truncatedString = new byte[] { 0x00, 0x00, 0x00, 0x04, 0x41, 0x42 };
                            byte[] overLimitString = new byte[] { 0x00, 0x00, 0x00, 0x05, 0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x00, 0x00, 0x00 };
                            byte[] invalidBoolean = new byte[] { 0x00, 0x00, 0x00, 0x02 };

                            ExpectXdrDataException(
                                () =>
                                {
                                    XdrReader reader = new XdrReader(truncatedString);
                                    reader.ReadString();
                                },
                                expectedMessageFragment: "Insufficient data",
                                expectedPosition: 4);

                            ExpectXdrDataException(
                                () =>
                                {
                                    XdrReader reader = new XdrReader(overLimitString);
                                    reader.ReadString(maximumUtf8ByteLength: 4);
                                },
                                expectedMessageFragment: "maximum allowed length",
                                expectedPosition: 0);

                            ExpectXdrDataException(
                                () =>
                                {
                                    XdrReader reader = new XdrReader(invalidBoolean);
                                    reader.ReadBoolean();
                                },
                                expectedMessageFragment: "boolean value",
                                expectedPosition: 0);

                            return Task.CompletedTask;
                        }),

            };
        }
    }
}
