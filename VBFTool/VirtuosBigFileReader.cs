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
      this.mBigFilePath = path;
      FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
      try
      {
        VirtuosBigFileReader.Functor<ushort> functor1 = (VirtuosBigFileReader.Functor<ushort>) (() =>
        {
          byte[] buffer = new byte[2];
          fs.Read(buffer, 0, 2);
          return BitConverter.ToUInt16(buffer, 0);
        });
        VirtuosBigFileReader.Functor<uint> functor2 = (VirtuosBigFileReader.Functor<uint>) (() =>
        {
          byte[] buffer = new byte[4];
          fs.Read(buffer, 0, 4);
          return BitConverter.ToUInt32(buffer, 0);
        });
        VirtuosBigFileReader.Functor<ulong> functor3 = (VirtuosBigFileReader.Functor<ulong>) (() =>
        {
          byte[] buffer = new byte[8];
          fs.Read(buffer, 0, 8);
          return BitConverter.ToUInt64(buffer, 0);
        });
        VirtuosBigFileReader.Functor<string> functor4 = (VirtuosBigFileReader.Functor<string>) (() =>
        {
          byte[] buffer = new byte[16];
          fs.Read(buffer, 0, 16);
          StringBuilder stringBuilder = new StringBuilder();
          foreach (byte num in buffer)
            stringBuilder.Append(num.ToString("X02"));
          return stringBuilder.ToString();
        });
        if ((int) functor2() != 1264144979)
          throw new VirtuosBigFileException();
        uint num1 = functor2();
        this.mNumFiles = functor2();
        this.mFileNameMd5s = new string[this.mNumFiles];
        this.mFileNameOffsets = new ulong[this.mNumFiles];
        this.mBlockListStarts = new uint[this.mNumFiles];
        this.mOriginalSizes = new ulong[this.mNumFiles];
        this.mStartOffsets = new ulong[this.mNumFiles];
        this.mMD5ToIndex = new Dictionary<string, int>();
        int num2 = (int) functor2();
        for (int index = 0; (long) index < (long) this.mNumFiles; ++index)
        {
          this.mFileNameMd5s[index] = functor4();
          this.mMD5ToIndex.Add(this.mFileNameMd5s[index], index);
        }
        for (int index = 0; (long) index < (long) this.mNumFiles; ++index)
        {
          this.mBlockListStarts[index] = functor2();
          int num3 = (int) functor2();
          this.mOriginalSizes[index] = functor3();
          this.mStartOffsets[index] = functor3();
          this.mFileNameOffsets[index] = functor3();
        }
        uint num4 = functor2();
        this.mStringTable = new byte[(num4 - 4U)];
        fs.Read(this.mStringTable, 0, (int) num4 - 4);
        uint num5 = 0;
        foreach (ulong num3 in this.mOriginalSizes)
        {
          num5 += (uint) (num3 / 65536UL);
          if ((long) (num3 % 65536UL) != 0L)
            ++num5;
        }
        this.mBlockList = new ushort[num5];
        for (int index = 0; (long) index < (long) num5; ++index)
          this.mBlockList[index] = functor1();
        fs.Seek(0L, SeekOrigin.Begin);
        byte[] buffer1 = new byte[num1];
        fs.Read(buffer1, 0, (int) num1);
        byte[] buffer2 = new byte[16];
        fs.Seek(-16L, SeekOrigin.End);
        fs.Read(buffer2, 0, 16);
        fs.Close();
        if (!Enumerable.SequenceEqual<byte>((IEnumerable<byte>) MD5.Create().ComputeHash(buffer1), (IEnumerable<byte>) buffer2))
          throw new VirtuosBigFileException();
      }
      finally
      {
        if (fs != null)
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
            if (!this.mMD5ToIndex.TryGetValue(stringBuilder.ToString(), out index1))
                return false;
            ulong num1 = this.mOriginalSizes[index1];
            int num2 = (int)(num1 / 65536UL);
            int num3 = (int)(num1 % 65536UL);
            if (num3 != 0)
                ++num2;
            else
                num3 = 65536;
            ulong num4 = this.mStartOffsets[index1];
            int num5 = (int)this.mBlockListStarts[index1];
            using (FileStream fileStream = File.Open(this.mBigFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fileStream.Seek((long)num4, SeekOrigin.Begin);
                FileStream outputStream = new FileStream(outputFile, FileMode.Create);
                for (int index2 = 0; index2 < num2; ++index2)
                {
                    int count1 = (int)this.mBlockList[num5 + index2];
                    if (count1 == 0)
                        count1 = 65536;
                    byte[] buffer1 = new byte[count1];
                    fileStream.Read(buffer1, 0, count1);
                    int count2 = index2 != num2 - 1 ? 65536 : num3;
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
                            DeflateStream deflateStream = new DeflateStream((Stream)new MemoryStream(buffer1, 2, count1 - 2), CompressionMode.Decompress);
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

    private delegate T Functor<T>();
  }
}
