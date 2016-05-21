using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VBFTool
{
    public class VirtuosBigFileWriter
    {
        private static readonly MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();

        public void BuildVBF(string inputDirectory, string outputFile)
        {
            if (!Directory.Exists(inputDirectory)) return;

            Console.WriteLine("Searching for files...");

            var dirFiles = Directory.EnumerateFiles(inputDirectory, "*.*", SearchOption.AllDirectories);
            var vbfFiles = dirFiles.ToList();

            Console.WriteLine($"Found {vbfFiles.Count} files.\n");

            var fileNameHashes = new byte[vbfFiles.Count][];
            var fileOriginalSizes = new ulong[vbfFiles.Count];
            var fileNameOffset = new ulong[vbfFiles.Count];
            var fileStartOffsets = new ulong[vbfFiles.Count];
            var fileBlockListStarts = new uint[vbfFiles.Count];

            Console.Write("Generating file info tables... ");

            var stringTableStream = new MemoryStream();
            var fileProgress = 0;
            for (var i = 0; i < vbfFiles.Count; i++)
            {
                // Convert file name to byte array
                var fixedName = FixFileName(inputDirectory, vbfFiles[i].ToLower());
                var nameBytes = Encoding.UTF8.GetBytes(fixedName);

                // Generate hash
                var nameHash = md5.ComputeHash(nameBytes);
                fileNameHashes[i] = nameHash;

                // Get filename table offset
                fileNameOffset[i] = (uint) stringTableStream.Position;
                stringTableStream.Write(nameBytes, 0, nameBytes.Length);
                stringTableStream.Write(new[] {(byte) '\0'}, 0, 1);

                // Get file sizes
                var fileInfo = new FileInfo(vbfFiles[i]);
                fileOriginalSizes[i] = (ulong) fileInfo.Length;

                // Update console display
                var percent = (int) ((float) i/vbfFiles.Count*100);
                if (fileProgress != percent)
                {
                    fileProgress = percent;
                    Console.SetCursorPosition(31, Console.CursorTop);
                    Console.Write($"{fileProgress + 1}%");
                }
            }

            Console.Write('\n');

            var stringTable = stringTableStream.ToArray();
            stringTableStream.Dispose();

            using (var vbfStream = new FileStream(outputFile, FileMode.Create))
            using (var vbfWriter = new BinaryWriter(vbfStream))
            {
                vbfWriter.Write(1264144979); // VBF File Header
                vbfWriter.Write((uint) 0); // Header size
                vbfWriter.Write((ulong) vbfFiles.Count); // Total files in archive

                // Write filename hashes
                foreach (var nameHash in fileNameHashes)
                {
                    vbfWriter.Write(nameHash);
                }

                // Current location is where the block info list will be written later
                var posBlockInfo = vbfWriter.BaseStream.Position;

                // Seek to location of filename table and write it
                vbfWriter.Seek(32*vbfFiles.Count, SeekOrigin.Current);

                vbfWriter.Write((uint) stringTable.Length + 4);
                vbfWriter.Write(stringTable);

                // Calculate total blocks in archive

                uint blockCount = 0;
                foreach (var originalSize in fileOriginalSizes)
                {
                    blockCount += (uint) (originalSize/65536UL);
                    if ((long) (originalSize%65536UL) != 0L)
                        ++blockCount;
                }

                var blockSizes = new ushort[blockCount];
                var blockSizeTablePosition = vbfWriter.BaseStream.Position;
                var vbfHeaderLength = blockSizeTablePosition + 2*blockCount;

                // Seek to beginning of data segment
                vbfWriter.Seek((int) vbfHeaderLength, SeekOrigin.Begin);

                // Begin compressing files
                var currentBlock = 0;
                var progress = 0;
                for (var fileIndex = 0; fileIndex < vbfFiles.Count; fileIndex++)
                {
                    var sourceFileStream = new FileStream(vbfFiles[fileIndex], FileMode.Open);

                    var fileBlocks = sourceFileStream.Length/65536;
                    var fileMod = sourceFileStream.Length%65536;

                    if (fileMod != 0)
                        fileBlocks++;

                    fileBlockListStarts[fileIndex] = (uint) currentBlock;
                    fileStartOffsets[fileIndex] = (ulong) vbfStream.Position;

                    for (var blockIndex = 0; blockIndex < fileBlocks; blockIndex++)
                    {
                        var finalBlock = blockIndex == fileBlocks - 1;
                        var sourceBufferSize = 65536L;

                        if (finalBlock)
                            sourceBufferSize = sourceFileStream.Length - sourceFileStream.Position;

                        // read block from source file
                        var sourceBuffer = new byte[sourceBufferSize];
                        sourceFileStream.Read(sourceBuffer, 0, sourceBuffer.Length);

                        // compress data into memory stream and write to file
                        using (var deflatedMemoryStream = new MemoryStream())
                        {
                            if (!finalBlock)
                            {
                                // Deflate block into memory stream
                                using (var deflateStream = new DeflateStream(deflatedMemoryStream, CompressionMode.Compress))
                                    deflateStream.Write(sourceBuffer, 0, sourceBuffer.Length);
                            }

                            var deflatedBytes = deflatedMemoryStream.ToArray();

                            // Blocks that are over 65536 bytes and the last block of each file are uncompressed
                            if (deflatedBytes.Length >= 65536 || finalBlock)
                            {
                                // Replace deflated data with uncompressed data
                                deflatedBytes = sourceBuffer;
                                blockSizes[currentBlock] = deflatedBytes.Length == 65536
                                    ? (ushort) 0
                                    : (ushort) sourceBufferSize;
                            }
                            else
                            {
                                // Write compressed block
                                vbfWriter.Write((ushort) 0); // CRC??
                                blockSizes[currentBlock] = (ushort) (deflatedBytes.Length + 2);
                            }

                            vbfWriter.Write(deflatedBytes); // write block to file

                            //File.WriteAllBytes("blocks\\block_" + currentBlock.ToString(), deflatedBytes);
                        }

                        var currentProgress = (int) ((float) currentBlock/blockCount*100) + 1;

                        if (currentProgress != progress)
                        {
                            progress = currentProgress;
                            Console.SetCursorPosition(0, Console.CursorTop);
                            Console.Write($"Compressing {blockCount} blocks... {progress}%");
                        }

                        currentBlock++;
                    }
                    
                }

                // Write block size table to file
                vbfWriter.Seek((int) blockSizeTablePosition, SeekOrigin.Begin);

                foreach (var blockSize in blockSizes)
                    vbfWriter.Write(blockSize);

                // Write file block info
                vbfWriter.Seek((int) posBlockInfo, SeekOrigin.Begin);
                for (var f = 0; f < vbfFiles.Count; f++)
                {
                    vbfWriter.Write(fileBlockListStarts[f]);
                    vbfWriter.Write((uint) 4002806);
                    vbfWriter.Write(fileOriginalSizes[f]);
                    vbfWriter.Write(fileStartOffsets[f]);
                    vbfWriter.Write(fileNameOffset[f]);
                }

                // Generate checksum
                vbfStream.Seek(4, SeekOrigin.Begin);
                vbfWriter.Write((uint) vbfHeaderLength);

                vbfStream.Seek(0, SeekOrigin.Begin);
                var buffer = new byte[vbfHeaderLength];
                vbfStream.Read(buffer, 0, buffer.Length);

                var fileHash = MD5.Create().ComputeHash(buffer);
                vbfStream.Seek(0, SeekOrigin.End);
                vbfStream.Write(fileHash, 0, fileHash.Length);

                Console.WriteLine("\nBuild completed successfully!");
            }
        }

        //private byte[] GenerateChecksum()
        //{
        //    //unsigned short crc16(const unsigned char* data_p, unsigned char length){
        //    //    unsigned char x;
        //    //    unsigned short crc = 0xFFFF;

        //    //    while (length--)
        //    //    {
        //    //        x = crc >> 8 ^ *data_p++;
        //    //        x ^= x >> 4;
        //    //        crc = (crc << 8) ^ ((unsigned short)(x << 12)) ^ ((unsigned short)(x << 5)) ^ ((unsigned short)x);
        //    //    }
        //    //    return crc;
        //    //}
        //}

        private string FixFileName(string inputDirectory, string fileName)
        {
            var innerFile = fileName.Substring(inputDirectory.Length + 1);
            var fixedFile = innerFile.Replace('\\', '/');
            return fixedFile;
        }
    }
}