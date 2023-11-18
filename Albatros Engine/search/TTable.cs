using System;
using System.Diagnostics;

namespace AlbatrosEngine.search;

internal class TTable
{
    private TranspositionTableEntry[] _table;

    public int Size => _table.Length;

    public TTable(int mB)
    {
        _table = new TranspositionTableEntry[mB * 65536];
    }

    public void Clear()
    {
        Array.Clear(_table);
    }

    public void Resize(int mB)
    {
        _table = new TranspositionTableEntry[mB * 65536];
    }

    public int EntryCount()
    {
        var count = 0;
        for (var i = 0; i < Size; i++)
            if (_table[i].BestMove != 0)
                count++;

        return count;
    }

    public void Add(int move, byte depth, int value, ulong key, bool betaCutoff, bool alphaCutoff)
    {
        var index = key % (ulong)Size;

        _table[index] = new(move, value, depth, alphaCutoff, betaCutoff, !alphaCutoff && !betaCutoff , key);
    }

    public int IsValid(ulong key)
    {
        var index = key % (ulong)Size;

        if (_table[index].BestMove == 0)
            return -2;

        var otherKey = _table[index].Key;

        if (otherKey == key)
            return 1;
        
        return -1;
    }

    public TranspositionTableEntry GetInfo(ulong key)
    {
        var index = (int)(key % (ulong)Size);
        return _table[index];
    }
}

internal struct TranspositionTableEntry
{
    public readonly ulong Key;
    public readonly short BestMove;
    public int Score;
    public readonly byte Depth;
    private readonly byte _flags = 0;
    public readonly bool FailLow => (_flags & 1) != 0;
    public readonly bool FailHigh => (_flags & 2) != 0;
    public readonly bool ExactScore => (_flags & 4) != 0;

    public TranspositionTableEntry(int bestMove, int currentScore, byte currentDepth, bool failLow, bool failHigh, bool exactScore, ulong key)
    {
        BestMove = (short)bestMove;
        Score = currentScore;
        Depth = currentDepth;
        _flags |= Convert.ToByte(failLow);
        _flags |= (byte)(Convert.ToByte(failHigh) << 1);
        _flags |= (byte)(Convert.ToByte(exactScore) << 2);
        Key = key;
        Debug.Assert(failHigh == FailHigh);
        Debug.Assert(exactScore == ExactScore);
        Debug.Assert(failLow == FailLow);
    }
}
