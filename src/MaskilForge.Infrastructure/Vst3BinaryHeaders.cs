using System.Buffers.Binary;

namespace MaskilForge.Infrastructure;

public sealed record BinaryHeaderInfo(string Status, string? Format, IReadOnlyList<string> Architectures);

/// <summary>Reads bounded header fields only. A recognized header is not a validated or loadable plugin.</summary>
public static class Vst3BinaryHeaders
{
    public static BinaryHeaderInfo Read(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            var header = At(0, 32);
            if (header[0] == 'M' && header[1] == 'Z')
            {
                var dos = At(0, 64);
                var offset = BinaryPrimitives.ReadUInt32LittleEndian(dos.AsSpan(60));
                if (offset < 64 || offset > 1024 * 1024) return new("Unsupported", "PE", []);
                var pe = At(offset, 26);
                if (!pe.AsSpan(0, 4).SequenceEqual(new byte[] { 80, 69, 0, 0 })) return Invalid();
                var machine = BinaryPrimitives.ReadUInt16LittleEndian(pe.AsSpan(4));
                var flags = BinaryPrimitives.ReadUInt16LittleEndian(pe.AsSpan(22));
                var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(pe.AsSpan(20));
                var magic = BinaryPrimitives.ReadUInt16LittleEndian(pe.AsSpan(24));
                if ((flags & 0x2000) == 0 || optionalSize < 2 || offset + 24L + optionalSize > stream.Length) return Invalid();
                var architecture = machine switch { 0x014c => "X86", 0x8664 => "X64", 0x01c4 => "Arm", 0xaa64 => "Arm64", 0xa641 => "Arm64EC", 0xa64e => "Arm64X", _ => "Unknown" };
                var is64 = architecture is "X64" or "Arm64" or "Arm64EC" or "Arm64X";
                if (magic is not (0x10b or 0x20b) || (architecture != "Unknown" && magic != (is64 ? 0x20b : 0x10b))) return Invalid();
                return new("Recognized", "PE", [architecture]);
            }
            if (header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x7f, 69, 76, 70 }))
            {
                var bits = header[4]; var little = header[5] == 1;
                if (bits is not (1 or 2) || header[5] is not (1 or 2) || header[6] != 1) return Invalid();
                var elf = At(0, bits == 1 ? 52 : 64);
                ushort U16(int offset) => little ? BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(offset)) : BinaryPrimitives.ReadUInt16BigEndian(elf.AsSpan(offset));
                if (U16(16) != 3 || U16(bits == 1 ? 40 : 52) != elf.Length) return Invalid();
                var architecture = U16(18) switch { 3 => "X86", 62 => "X64", 40 => "Arm", 183 => "Arm64", _ => "Unknown" };
                if (architecture != "Unknown" && ((architecture is "X64" or "Arm64") != (bits == 2))) return Invalid();
                // Endianness matters independently of the machine ID. Current supported hosts are little-endian.
                return new("Recognized", "ELF", [little ? architecture : $"{architecture} (big-endian)"]);
            }
            var magic32 = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (magic32 is 0xcafebabe or 0xcafebabf)
            {
                var count = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
                if (count is 0 or > 16) return new("Unsupported", "Mach-O", []);
                var size = magic32 == 0xcafebabf ? 32 : 20;
                var table = At(8, checked((int)count * size));
                var architectures = new List<string>();
                var ranges = new List<(ulong Start, ulong End)>();
                for (var index = 0; index < count; index++)
                {
                    var entry = table.AsSpan(index * size, size);
                    var cpu = BinaryPrimitives.ReadUInt32BigEndian(entry);
                    var offset = size == 32 ? BinaryPrimitives.ReadUInt64BigEndian(entry[8..]) : BinaryPrimitives.ReadUInt32BigEndian(entry[8..]);
                    var length = size == 32 ? BinaryPrimitives.ReadUInt64BigEndian(entry[16..]) : BinaryPrimitives.ReadUInt32BigEndian(entry[12..]);
                    if (offset < (ulong)(8 + table.Length) || length < 32 || offset > (ulong)stream.Length || length > (ulong)stream.Length - offset
                        || ranges.Any(range => offset < range.End && offset + length > range.Start)) return Invalid();
                    ranges.Add((offset, offset + length));
                    var slice = At((long)offset, 32);
                    var architecture = MachArchitecture(slice, out var sliceCpu);
                    if (architecture is null || sliceCpu != cpu) return Invalid();
                    architectures.Add(architecture);
                }
                return new("Recognized", "Mach-O", architectures.Distinct().ToArray());
            }
            var thin = MachArchitecture(header, out _);
            return thin is null ? new("Unsupported", null, []) : new("Recognized", "Mach-O", [thin]);
        }
        catch (EndOfStreamException) { return Invalid(); }

        byte[] At(long offset, int count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (offset < 0 || offset > stream.Length || count > stream.Length - offset) throw new EndOfStreamException();
            stream.Position = offset;
            var bytes = new byte[count];
            stream.ReadExactly(bytes);
            return bytes;
        }
    }

    private static BinaryHeaderInfo Invalid() => new("Invalid", null, []);
    private static string? MachArchitecture(byte[] header, out uint cpu)
    {
        cpu = 0;
        var magic = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (magic is not (0xfeedface or 0xfeedfacf or 0xcefaedfe or 0xcffaedfe)) return null;
        var little = magic is 0xcefaedfe or 0xcffaedfe;
        uint U32(int offset) => little ? BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(offset)) : BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(offset));
        cpu = U32(4);
        if (U32(12) is not (6 or 8)) return null; // dylib or bundle; executable apps are not plugin libraries.
        var architecture = cpu switch { 7 => "X86", 0x01000007 => "X64", 12 => "Arm", 0x0100000c => "Arm64", _ => "Unknown" };
        if (architecture != "Unknown" && ((architecture is "X64" or "Arm64") != (magic is 0xfeedfacf or 0xcffaedfe))) return null;
        return little ? architecture : $"{architecture} (big-endian)";
    }
}
