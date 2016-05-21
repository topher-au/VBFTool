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
        public ulong NumFiles => mNumFiles;

        private string mBigFilePath;
        private ushort[] mBlockList;
        private uint[] mBlockListStarts;
        private string[] mFileNameMd5s;
        private ulong[] mFileNameOffsets;
        private Dictionary<string, int> mMD5ToIndex;
        private ulong mNumFiles;
        private ulong[] mOriginalSizes;
        private ulong[] mStartOffsets;
        private byte[] mStringTable;

        public void LoadBigFileFile(string path)
        {
            mBigFilePath = path;
            var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                Functor<ushort> ReadUInt16 = () =>
                {
                    var buffer = new byte[2];
                    fs.Read(buffer, 0, 2);
                    return BitConverter.ToUInt16(buffer, 0);
                };

                Functor<uint> ReadUInt32 = () =>
                {
                    var buffer = new byte[4];
                    fs.Read(buffer, 0, 4);
                    return BitConverter.ToUInt32(buffer, 0);
                };

                Functor<ulong> ReadUInt64 = () =>
                {
                    var buffer = new byte[8];
                    fs.Read(buffer, 0, 8);
                    return BitConverter.ToUInt64(buffer, 0);
                };

                Functor<string> ReadMD5Hash = () =>
                {
                    var buffer = new byte[16];
                    fs.Read(buffer, 0, 16);
                    var stringBuilder = new StringBuilder();
                    foreach (var num in buffer)
                        stringBuilder.Append(num.ToString("X02"));
                    return stringBuilder.ToString();
                };

                // Check Header
                if ((int) ReadUInt32() != 1264144979)
                    throw new VirtuosBigFileException();

                var headerLength = ReadUInt32();
                mNumFiles = ReadUInt64();

                mFileNameMd5s = new string[mNumFiles];
                mFileNameOffsets = new ulong[mNumFiles];
                mBlockListStarts = new uint[mNumFiles];
                mOriginalSizes = new ulong[mNumFiles];
                mStartOffsets = new ulong[mNumFiles];
                mMD5ToIndex = new Dictionary<string, int>();


                for (var index = 0; (ulong)index < mNumFiles; ++index)
                {
                    mFileNameMd5s[index] = ReadMD5Hash();
                    mMD5ToIndex.Add(mFileNameMd5s[index], index);
                }

                for (var index = 0; (ulong)index < mNumFiles; ++index)
                {
                    mBlockListStarts[index] = ReadUInt32();
                    var num3 = (int) ReadUInt32();
                    mOriginalSizes[index] = ReadUInt64();
                    mStartOffsets[index] = ReadUInt64();
                    mFileNameOffsets[index] = ReadUInt64();
                }

                var stringTableSize = ReadUInt32();

                mStringTable = new byte[stringTableSize - 4U];
                fs.Read(mStringTable, 0, (int) stringTableSize - 4);

                uint blockCount = 0;
                foreach (var originalSize in mOriginalSizes)
                {
                    blockCount += (uint) (originalSize/65536UL);
                    if ((long) (originalSize%65536UL) != 0L)
                        ++blockCount;
                }

                mBlockList = new ushort[blockCount];

                for (var index = 0; index < blockCount; ++index)
                    mBlockList[index] = ReadUInt16();

                fs.Seek(0L, SeekOrigin.Begin);

                var buffer1 = new byte[headerLength];
                fs.Read(buffer1, 0, (int) headerLength);

                var buffer2 = new byte[16];
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

        public string[] ReadFileList()
        {
            if (mStringTable == null) return null; // No string table loaded

            // Convert string table bytes to string, split into individual file names
            var stringTable = Encoding.UTF8.GetString(mStringTable).Trim('\0');
            var fileList = stringTable.Split('\0');

            if((ulong)fileList.Length != mNumFiles)
                throw new VirtuosBigFileException(); // File list count does not match total files!

            return fileList;
        }

        public bool ExtractFileContents(string path, string outputFile)
        {
            path = path.ToLower();
            var md5 = MD5.Create();
            var stringBuilder = new StringBuilder();
            foreach (var num in md5.ComputeHash(Encoding.UTF8.GetBytes(path)))
                stringBuilder.Append(num.ToString("X02"));
            int fileIndex;
            if (!mMD5ToIndex.TryGetValue(stringBuilder.ToString(), out fileIndex))
                return false;
            var originalSize = mOriginalSizes[fileIndex];
            var blockCount = (int) (originalSize/65536UL);
            var blockRemainder = (int) (originalSize%65536UL);
            if (blockRemainder != 0)
                ++blockCount;
            else
                blockRemainder = 65536;
            var startOffset = mStartOffsets[fileIndex];
            var blockListStart = (int) mBlockListStarts[fileIndex];
            using (var fileStream = File.Open(mBigFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fileStream.Seek((long) startOffset, SeekOrigin.Begin);
                var outputStream = new FileStream(outputFile, FileMode.Create);
                for (var blockIndex = 0; blockIndex < blockCount; ++blockIndex)
                {
                    int blockLength = mBlockList[blockListStart + blockIndex];
                    if (blockLength == 0)
                        blockLength = 65536;
                    var compressedBuffer = new byte[blockLength];
                    fileStream.Read(compressedBuffer, 0, blockLength);
                    var decBlockSize = blockIndex != blockCount - 1 ? 65536 : blockRemainder;
                    byte[] decompressedBuffer;
                    if (blockLength != 65536)
                    {
                        if (blockIndex == blockCount - 1)
                        {
                            if (blockLength == blockRemainder)
                                goto MoveUncompressedData;
                        }
                        try
                        {
                            decompressedBuffer = new byte[decBlockSize];
                            var deflateStream = new DeflateStream(
                                new MemoryStream(compressedBuffer, 2, blockLength - 2), CompressionMode.Decompress);
                            deflateStream.Read(decompressedBuffer, 0, decBlockSize);
                            deflateStream.Close();
                            deflateStream.Dispose();
                            goto WriteBuffer;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Exception extracting file: {ex.Message}");
                            throw new VirtuosBigFileException();
                        }
                    }
                    MoveUncompressedData:
                    decompressedBuffer = compressedBuffer;
                    WriteBuffer:
                    outputStream.Write(decompressedBuffer, 0, decBlockSize);
                }
                fileStream.Close();
                outputStream.Close();
                return true;
            }
        }

        private delegate T Functor<out T>();
    }
}