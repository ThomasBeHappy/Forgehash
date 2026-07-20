namespace ForgeHash;

/// <summary>
/// Optional trace record emitted for each processed memory block.
/// Used by research tooling; never enabled on production hashing paths by default.
/// </summary>
public sealed record BlockReferenceTrace(
    int Pass,
    int Slice,
    int Lane,
    int BlockIndex,
    int PreviousIndex,
    int ReferenceLane,
    int ReferenceIndex,
    bool CrossLane);

/// <summary>
/// Callback used by analysis tooling to observe block references.
/// </summary>
public delegate void BlockReferenceObserver(BlockReferenceTrace trace);

/// <summary>
/// Callback fired after a block is written, with the raw ForgeMix output words.
/// For pass 0 this matches the stored block; for later passes it is the mix result
/// before XOR with the previous block contents.
/// </summary>
public delegate void ForgeMixSampleObserver(int pass, int lane, int blockIndex, ReadOnlySpan<ulong> mixOutput);
