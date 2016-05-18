// Decompiled with JetBrains decompiler
// Type: VBFTool.VirtuosBigFileReader
// Assembly: GameLauncher, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 830B8ED0-270B-4C7E-9AF7-ABA2BB662E63
// Assembly location: D:\Games\Steam\SteamApps\common\FINAL FANTASY FFX&FFX-2 HD Remaster\FFX&X-2_LAUNCHER.exe

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VBFTool
{
  internal class VirtuosBigFileReader
  {
    private uint mNumFiles;
    private Dictionary<string, int> mMD5ToIndex;
    private string[] mFileNameMd5s;
    private uint[] mBlockListStarts;
    private ulong[] mOriginalSizes;
    private ulong[] mStartOffsets;
    private ulong[] mFileNameOffsets;
    private byte[] mStringTable;
    private ushort[] mBlockList;
    private string mBigFilePath;

    public void LoadBigFileFile(string path)
    {
      mBigFilePath = path;
      var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
      try
      {
        VirtuosBigFileReader.Functor<ushort> ReadUInt16 = () =>
        {
            byte[] buffer = new byte[2];
            fs.Read(buffer, 0, 2);
            return BitConverter.ToUInt16(buffer, 0);
        };

        VirtuosBigFileReader.Functor<uint> ReadUInt32 = () =>
        {
            byte[] buffer = new byte[4];
            fs.Read(buffer, 0, 4);
            return BitConverter.ToUInt32(buffer, 0);
        };

        VirtuosBigFileReader.Functor<ulong> ReadUInt64 = () =>
        {
            byte[] buffer = new byte[8];
            fs.Read(buffer, 0, 8);
            return BitConverter.ToUInt64(buffer, 0);
        };

        VirtuosBigFileReader.Functor<string> ReadMD5Hash = () =>
        {
            byte[] buffer = new byte[16];
            fs.Read(buffer, 0, 16);
            StringBuilder stringBuilder = new StringBuilder();
            foreach (byte num in buffer)
                stringBuilder.Append(num.ToString("X02"));
            return stringBuilder.ToString();
        };

        // Check Header
        if ((int) ReadUInt32() != 1264144979)
          throw new VirtuosBigFileException();

        var num1 = ReadUInt32();
        mNumFiles = ReadUInt32();

        mFileNameMd5s = new string[mNumFiles];
        mFileNameOffsets = new ulong[mNumFiles];
        mBlockListStarts = new uint[mNumFiles];
        mOriginalSizes = new ulong[mNumFiles];
        mStartOffsets = new ulong[mNumFiles];
        mMD5ToIndex = new Dictionary<string, int>();

        var num2 = (int) ReadUInt32();


        for (var index = 0; index < mNumFiles; ++index)
        {
          mFileNameMd5s[index] = ReadMD5Hash();
          mMD5ToIndex.Add(mFileNameMd5s[index], index);
        }

        for (var index = 0; index < mNumFiles; ++index)
        {
          mBlockListStarts[index] = ReadUInt32();
          int num3 = (int) ReadUInt32();
          mOriginalSizes[index] = ReadUInt64();
          mStartOffsets[index] = ReadUInt64();
          mFileNameOffsets[index] = ReadUInt64();
        }

        var num4 = ReadUInt32();

        mStringTable = new byte[num4 - 4U];
        fs.Read(mStringTable, 0, (int) num4 - 4);

        uint num5 = 0;
        foreach (var num3 in mOriginalSizes)
        {
          num5 += (uint) (num3 / 65536UL);
          if ((long) (num3 % 65536UL) != 0L)
            ++num5;
        }

        mBlockList = new ushort[num5];

        for (int index = 0; index < num5; ++index)
          mBlockList[index] = ReadUInt16();

        fs.Seek(0L, SeekOrigin.Begin);

        byte[] buffer1 = new byte[num1];
        fs.Read(buffer1, 0, (int) num1);

        byte[] buffer2 = new byte[16];
        fs.Seek(-16L, SeekOrigin.End);
        fs.Read(buffer2, 0, 16);
        fs.Close();

        if (!MD5.Create().ComputeHash(buffer1).SequenceEqual(buffer2))
          throw new VirtuosBigFileException();
      }
      finally
      {
        fs.Dispose();
      }
    }

      public bool ExtractFileContents(string path, string outputFile)
      {
            path = path.ToLower();
            MD5 md5 = MD5.Create();
            StringBuilder stringBuilder = new StringBuilder();
            foreach (byte num in md5.ComputeHash(Encoding.UTF8.GetBytes(path)))
                stringBuilder.Append(num.ToString("X02"));
            int index1;
            if (!mMD5ToIndex.TryGetValue(stringBuilder.ToString(), out index1))
                return false;
            ulong num1 = mOriginalSizes[index1];
            int num2 = (int)(num1 / 65536UL);
            int num3 = (int)(num1 % 65536UL);
            if (num3 != 0)
                ++num2;
            else
                num3 = 65536;
            ulong num4 = mStartOffsets[index1];
            int num5 = (int)mBlockListStarts[index1];
            using (FileStream fileStream = File.Open(mBigFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fileStream.Seek((long)num4, SeekOrigin.Begin);
                FileStream outputStream = new FileStream(outputFile, FileMode.Create);
                for (int index2 = 0; index2 < num2; ++index2)
                {
                    int count1 = mBlockList[num5 + index2];
                    if (count1 == 0)
                        count1 = 65536;
                    var buffer1 = new byte[count1];
                    fileStream.Read(buffer1, 0, count1);
                    var count2 = index2 != num2 - 1 ? 65536 : num3;
                    byte[] buffer2;
                    if (count1 != 65536)
                    {
                        if (index2 == num2 - 1)
                        {
                            if (count1 == num3)
                                goto label_14;
                        }
                        try
                        {
                            buffer2 = new byte[count2];
                            var deflateStream = new DeflateStream((Stream)new MemoryStream(buffer1, 2, count1 - 2), CompressionMode.Decompress);
                            deflateStream.Read(buffer2, 0, count2);
                            deflateStream.Close();
                            deflateStream.Dispose();
                            goto label_17;
                        }
                        catch
                        {
                            throw new VirtuosBigFileException();
                        }
                    }
                    label_14:
                    buffer2 = buffer1;
                    label_17:
                    outputStream.Write(buffer2, 0, count2);
                }
                fileStream.Close();
                outputStream.Close();
                return true;
            }
        }

    private delegate T Functor<out T>();
  }
}
