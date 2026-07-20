using System.Buffers.Binary;
using System.Text;

namespace ForgeHashX;

/// <summary>
/// ForgeX sponge (SPECIFICATION_X §4). Experimental — not for production use.
/// </summary>
public sealed class ForgeX
{
    public const int StateWords = 16;
    public const int RateWords = 8;
    public const int RateBytes = RateWords * sizeof(ulong);

    private readonly ulong[] _state = new ulong[StateWords];
    private int _offset;
    private bool _squeezing;

    public static byte[] Hash(string domainTag, ReadOnlySpan<byte> data)
    {
        var x = new ForgeX();
        x.AbsorbDomain(domainTag, data);
        return x.Squeeze(32);
    }

    public static byte[] Xof(string domainTag, ReadOnlySpan<byte> data, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var x = new ForgeX();
        x.AbsorbDomain(domainTag, data);
        return x.Squeeze(length);
    }

    public void AbsorbDomain(string domainTag, ReadOnlySpan<byte> data)
    {
        byte[] tag = Encoding.ASCII.GetBytes(domainTag);
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(len, tag.Length);
        Absorb(len);
        Absorb(tag);
        Absorb(data);
    }

    public void Absorb(ReadOnlySpan<byte> input)
    {
        if (_squeezing)
        {
            throw new InvalidOperationException("Cannot absorb after squeezing.");
        }

        Span<byte> rate = RateAsBytes();
        foreach (byte b in input)
        {
            rate[_offset] ^= b;
            _offset++;
            if (_offset == RateBytes)
            {
                ForgePerm.Permute(_state);
                _offset = 0;
            }
        }
    }

    public byte[] Squeeze(int length)
    {
        if (!_squeezing)
        {
            PadAndSwitch();
            _squeezing = true;
        }

        byte[] output = new byte[length];
        int written = 0;
        Span<byte> rate = RateAsBytes();
        while (written < length)
        {
            if (_offset == RateBytes)
            {
                ForgePerm.Permute(_state);
                _offset = 0;
                rate = RateAsBytes();
            }

            int n = Math.Min(RateBytes - _offset, length - written);
            rate.Slice(_offset, n).CopyTo(output.AsSpan(written));
            _offset += n;
            written += n;
        }

        return output;
    }

    private void PadAndSwitch()
    {
        Span<byte> rate = RateAsBytes();
        rate[_offset] ^= 0x01;
        rate[RateBytes - 1] ^= 0x80;
        ForgePerm.Permute(_state);
        _offset = 0;
    }

    private Span<byte> RateAsBytes()
        => System.Runtime.InteropServices.MemoryMarshal.AsBytes(_state.AsSpan(0, RateWords));
}
