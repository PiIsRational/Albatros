using System;
using System.Collections;
using System.Drawing;

namespace AlbatrosEngine.search;

internal unsafe class TTable
{
    private byte* baseIndex;
    private byte[,] table;
    private const int ENTRY_SIZE = 16;
    private const int LOG_ENTRY_SIZE = 4;

    public int Size => table.GetLength(0);

    public TTable(int mB)
    {
        table = new byte[mB * 65536, ENTRY_SIZE];

        fixed (byte* idx = &table[0, 0])
            baseIndex = idx;
    }

    public void Clear()
    {
        Array.Clear(table);
    }

    public void Resize(int mB)
    {
        table = new byte[mB * 65536, ENTRY_SIZE];

        fixed (byte* idx = &table[0, 0])
            baseIndex = idx;
    }

    public int EntryCount()
    {
        int count = 0;
        for (int i = 0; i < table.GetLength(0); i++)
            if (table[i, 0] != 0)
                count++;

        return count;
    }

    public void Add(int move, int depth, int value, ulong key, byte beta_cutoff, byte alpha_cutoff)
    {
        int index = (int)(key % (ulong)table.GetLength(0));

        //standart logging pattern
        /*
         * depth
         * 
         * then value
         * 
         * then Move
         * 
         * the the  key
         */
        byte[] log = new byte[8];

        //save the depth and the beta cutoff (adding one to see if there is an entry)
        log[0] = (byte)Math.Min(depth + 1, 128);

        //save the evaluation
        for (int i = 0; i < 4; i++)
            log[i + 1] = BitConverter.GetBytes(value)[i];

        //save the move
        for (int i = 5; i < 7; i++)
            log[i] = BitConverter.GetBytes((short)move + 1)[i - 5];

        //add the flag for the beta cutoff
        log[7] += (byte)(beta_cutoff << 0);

        //add the flag for the alpha cutoff at the last index of the move
        log[7] += (byte)(alpha_cutoff << 1);

        byte[] keyArray = BitConverter.GetBytes(key);

        for (int i = 0; i < 16; i++)
            table[index, i] = 0;

        for (int i = 0; i < 8; i++)
            table[index, i] = log[i];

        for (int i = 8; i < keyArray.Length + 8; i++)
            table[index, i] = keyArray[i - 8];
    }

    public unsafe int IsValid(ulong key)
    {
        int index = (int)(key % (ulong)table.GetLength(0));

        if (*(baseIndex + (index << LOG_ENTRY_SIZE)) == 0)
            return -2;

        byte[] values = new byte[8];
        for (int i = 0; i < 8; i++)
            values[i] = table[index, 8 + i];

        ulong otherkey = BitConverter.ToUInt64(values);

        if (otherkey == key)
            return 1;
        else
            return -1;
    }

    public TTableEntry GetInfo(ulong key)
    {
        int index = (int)(key % (ulong)table.GetLength(0));
        byte depth = (byte)(table[index, 0] - 1);
        byte[] EvalParts = new byte[4];
        byte[] move_parts = new byte[2];
        EvalParts[0] = table[index, 1];
        EvalParts[1] = table[index, 2];
        EvalParts[2] = table[index, 3];
        EvalParts[3] = table[index, 4];
        int eval = BitConverter.ToInt32(EvalParts);

        //get the flag for the beta cutoff
        bool betaCutoff = (table[index, 7] & 0b01) != 0;

        //get the flag for the alpha cutoff
        bool alpha_cutoff = (table[index, 7] & 0b10) != 0;

        for (int i = 5; i < 7; i++)
            move_parts[i - 5] = table[index, i];

        int move = BitConverter.ToInt16(move_parts) - 1;

        return new TTableEntry(move, eval, depth, betaCutoff, alpha_cutoff, !betaCutoff && !alpha_cutoff);
    }
}

struct TTableEntry
{
    public int bestMove;
    public int score;
    public byte depth;
    public bool failHigh, failLow, exact;
    public TTableEntry(int Bestmove, int CurrentScore, byte Currentdepth, bool cut_node, bool all_node, bool pv_node)
    {
        bestMove = Bestmove;
        score = CurrentScore;
        depth = Currentdepth;
        failHigh = cut_node;
        failLow = all_node;
        exact = pv_node;
    }
}

