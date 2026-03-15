using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileConvert.Core.Interfaces;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;

namespace FileConvert.Infrastructure.Converters
{
    /// <summary>
    /// Handles archive format conversions (GZ, TAR, BZ2, ZIP, 7Z, RAR).
    /// Uses SharpZipLib and SharpCompress for cross-platform archive support.
    /// </summary>
    public class ArchiveConverter : IArchiveConverter
    {
        private const int StreamBufferSize = 4096;
        private const int DefaultZipCompressionLevel = 6;
        private const long MaxUncompressedSize = 1024 * 1024 * 500; // 500MB max per entry
        private const long MaxTotalUncompressedSize = 1024 * 1024 * 1024; // 1GB max total
        private const int MaxEntryCount = 10000;

        #region GZ/TGZ Conversions

        public Task<MemoryStream> ConvertGzToTar(MemoryStream gzStream)
        {
            var outputStream = new MemoryStream();
            gzStream.Position = 0;

            using (var gzipStream = new GZipInputStream(gzStream))
            {
                var buffer = new byte[StreamBufferSize];
                long totalBytesWritten = 0;
                int bytesRead;

                while ((bytesRead = gzipStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    totalBytesWritten += bytesRead;
                    if (totalBytesWritten > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    outputStream.Write(buffer, 0, bytesRead);
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertTarToGz(MemoryStream tarStream)
        {
            var outputStream = new MemoryStream();
            tarStream.Position = 0;

            using (var gzipStream = new GZipOutputStream(outputStream))
            {
                gzipStream.IsStreamOwner = false;
                var buffer = new byte[StreamBufferSize];
                StreamUtils.Copy(tarStream, gzipStream, buffer);
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        #endregion

        #region BZ2 Conversions

        public Task<MemoryStream> ConvertBz2ToTar(MemoryStream bz2Stream)
        {
            var outputStream = new MemoryStream();
            bz2Stream.Position = 0;

            using (var bzip2Stream = new ICSharpCode.SharpZipLib.BZip2.BZip2InputStream(bz2Stream))
            {
                var buffer = new byte[StreamBufferSize];
                long totalBytesWritten = 0;
                int bytesRead;

                while ((bytesRead = bzip2Stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    totalBytesWritten += bytesRead;
                    if (totalBytesWritten > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    outputStream.Write(buffer, 0, bytesRead);
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        #endregion

        #region ZIP Conversions

        public Task<MemoryStream> ConvertZipToTar(MemoryStream zipStream)
        {
            var outputStream = new MemoryStream();
            zipStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;

            using (var zipFile = new ZipFile(zipStream))
            using (var tarOutputStream = new TarOutputStream(outputStream, Encoding.UTF8))
            {
                tarOutputStream.IsStreamOwner = false;

                var entries = new List<ZipEntry>();
                foreach (ZipEntry zipEntry in zipFile)
                {
                    if (!zipEntry.IsDirectory)
                        entries.Add(zipEntry);
                }

                if (entries.Count > MaxEntryCount)
                    throw new InvalidOperationException("Archive contains too many entries");

                foreach (var zipEntry in entries)
                {
                    // Check size - handle unknown sizes (-1) by tracking actual bytes read
                    var hasKnownSize = zipEntry.Size > 0;
                    if (hasKnownSize && zipEntry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{zipEntry.Name}' exceeds maximum allowed size");

                    if (hasKnownSize)
                    {
                        totalExtractedSize += zipEntry.Size;
                        if (totalExtractedSize > MaxTotalUncompressedSize)
                            throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");
                    }

                    var sanitizedName = SanitizeArchiveEntryPath(zipEntry.Name);

                    var tarEntry = TarEntry.CreateTarEntry(sanitizedName);
                    tarEntry.Size = zipEntry.Size;

                    if (zipEntry.DateTime != DateTime.MinValue)
                    {
                        tarEntry.ModTime = zipEntry.DateTime;
                    }

                    tarOutputStream.PutNextEntry(tarEntry);

                    using (var zipInputStream = zipFile.GetInputStream(zipEntry))
                    {
                        // For unknown sizes, track bytes during copy
                        if (!hasKnownSize)
                        {
                            int bytesRead;
                            long entryBytesRead = 0;
                            while ((bytesRead = zipInputStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                entryBytesRead += bytesRead;
                                totalExtractedSize += bytesRead;

                                if (entryBytesRead > MaxUncompressedSize)
                                    throw new InvalidOperationException($"Entry '{zipEntry.Name}' exceeds maximum allowed size");
                                if (totalExtractedSize > MaxTotalUncompressedSize)
                                    throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                                tarOutputStream.Write(buffer, 0, bytesRead);
                            }
                        }
                        else
                        {
                            StreamUtils.Copy(zipInputStream, tarOutputStream, buffer);
                        }
                    }

                    tarOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertTarToZip(MemoryStream tarStream)
        {
            var outputStream = new MemoryStream();
            tarStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;
            int entryCount = 0;

            using (var tarInputStream = new TarInputStream(tarStream, Encoding.UTF8))
            using (var zipOutputStream = new ZipOutputStream(outputStream))
            {
                zipOutputStream.IsStreamOwner = false;
                zipOutputStream.SetLevel(DefaultZipCompressionLevel);

                TarEntry tarEntry;
                while ((tarEntry = tarInputStream.GetNextEntry()) != null)
                {
                    if (tarEntry.IsDirectory)
                        continue;

                    entryCount++;
                    if (entryCount > MaxEntryCount)
                        throw new InvalidOperationException("Archive contains too many entries");

                    // Check size - handle unknown sizes by tracking actual bytes read
                    var hasKnownSize = tarEntry.Size > 0;
                    if (hasKnownSize && tarEntry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{tarEntry.Name}' exceeds maximum allowed size");

                    if (hasKnownSize)
                    {
                        totalExtractedSize += tarEntry.Size;
                        if (totalExtractedSize > MaxTotalUncompressedSize)
                            throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");
                    }

                    var sanitizedName = SanitizeArchiveEntryPath(tarEntry.Name);

                    var zipEntry = new ZipEntry(sanitizedName)
                    {
                        DateTime = tarEntry.ModTime,
                        Size = tarEntry.Size
                    };

                    zipOutputStream.PutNextEntry(zipEntry);

                    // For unknown sizes, track bytes during copy
                    if (!hasKnownSize)
                    {
                        int bytesRead;
                        long entryBytesRead = 0;
                        while ((bytesRead = tarInputStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            entryBytesRead += bytesRead;
                            totalExtractedSize += bytesRead;

                            if (entryBytesRead > MaxUncompressedSize)
                                throw new InvalidOperationException($"Entry '{tarEntry.Name}' exceeds maximum allowed size");
                            if (totalExtractedSize > MaxTotalUncompressedSize)
                                throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                            zipOutputStream.Write(buffer, 0, bytesRead);
                        }
                    }
                    else
                    {
                        StreamUtils.Copy(tarInputStream, zipOutputStream, buffer);
                    }

                    zipOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        #endregion

        #region 7Z Conversions

        public Task<MemoryStream> Convert7zToZip(MemoryStream sevenZipStream)
        {
            var outputStream = new MemoryStream();
            sevenZipStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;

            using (var archive = SevenZipArchive.Open(sevenZipStream))
            using (var zipOutputStream = new ZipOutputStream(outputStream))
            {
                zipOutputStream.IsStreamOwner = false;
                zipOutputStream.SetLevel(DefaultZipCompressionLevel);

                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                if (entries.Count > MaxEntryCount)
                    throw new InvalidOperationException("Archive contains too many entries");

                foreach (var entry in entries)
                {
                    if (entry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{entry.Key}' exceeds maximum allowed size");

                    totalExtractedSize += entry.Size;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    var sanitizedName = SanitizeArchiveEntryPath(entry.Key);

                    var zipEntry = new ZipEntry(sanitizedName)
                    {
                        DateTime = entry.CreatedTime ?? DateTime.Now,
                        Size = entry.Size
                    };

                    zipOutputStream.PutNextEntry(zipEntry);

                    using (var entryStream = entry.OpenEntryStream())
                    {
                        StreamUtils.Copy(entryStream, zipOutputStream, buffer);
                    }

                    zipOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> Convert7zToTar(MemoryStream sevenZipStream)
        {
            var outputStream = new MemoryStream();
            sevenZipStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;

            using (var archive = SevenZipArchive.Open(sevenZipStream))
            using (var tarOutputStream = new TarOutputStream(outputStream, Encoding.UTF8))
            {
                tarOutputStream.IsStreamOwner = false;

                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                if (entries.Count > MaxEntryCount)
                    throw new InvalidOperationException("Archive contains too many entries");

                foreach (var entry in entries)
                {
                    if (entry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{entry.Key}' exceeds maximum allowed size");

                    totalExtractedSize += entry.Size;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    var sanitizedName = SanitizeArchiveEntryPath(entry.Key);

                    var tarEntry = TarEntry.CreateTarEntry(sanitizedName);
                    tarEntry.Size = entry.Size;

                    if (entry.CreatedTime.HasValue)
                    {
                        tarEntry.ModTime = entry.CreatedTime.Value;
                    }

                    tarOutputStream.PutNextEntry(tarEntry);

                    using (var entryStream = entry.OpenEntryStream())
                    {
                        StreamUtils.Copy(entryStream, tarOutputStream, buffer);
                    }

                    tarOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        #endregion

        #region RAR Conversions

        public Task<MemoryStream> ConvertRarToZip(MemoryStream rarStream)
        {
            var outputStream = new MemoryStream();
            rarStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;

            using (var archive = RarArchive.Open(rarStream))
            using (var zipOutputStream = new ZipOutputStream(outputStream))
            {
                zipOutputStream.IsStreamOwner = false;
                zipOutputStream.SetLevel(DefaultZipCompressionLevel);

                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                if (entries.Count > MaxEntryCount)
                    throw new InvalidOperationException("Archive contains too many entries");

                foreach (var entry in entries)
                {
                    if (entry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{entry.Key}' exceeds maximum allowed size");

                    totalExtractedSize += entry.Size;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    var sanitizedName = SanitizeArchiveEntryPath(entry.Key);

                    var zipEntry = new ZipEntry(sanitizedName)
                    {
                        DateTime = entry.CreatedTime ?? DateTime.Now,
                        Size = entry.Size
                    };

                    zipOutputStream.PutNextEntry(zipEntry);

                    using (var entryStream = entry.OpenEntryStream())
                    {
                        StreamUtils.Copy(entryStream, zipOutputStream, buffer);
                    }

                    zipOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertRarToTar(MemoryStream rarStream)
        {
            var outputStream = new MemoryStream();
            rarStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;

            using (var archive = RarArchive.Open(rarStream))
            using (var tarOutputStream = new TarOutputStream(outputStream, Encoding.UTF8))
            {
                tarOutputStream.IsStreamOwner = false;

                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                if (entries.Count > MaxEntryCount)
                    throw new InvalidOperationException("Archive contains too many entries");

                foreach (var entry in entries)
                {
                    if (entry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{entry.Key}' exceeds maximum allowed size");

                    totalExtractedSize += entry.Size;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    var sanitizedName = SanitizeArchiveEntryPath(entry.Key);

                    var tarEntry = TarEntry.CreateTarEntry(sanitizedName);
                    tarEntry.Size = entry.Size;

                    if (entry.CreatedTime.HasValue)
                    {
                        tarEntry.ModTime = entry.CreatedTime.Value;
                    }

                    tarOutputStream.PutNextEntry(tarEntry);

                    using (var entryStream = entry.OpenEntryStream())
                    {
                        StreamUtils.Copy(entryStream, tarOutputStream, buffer);
                    }

                    tarOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        #endregion

        #region Helper Methods

        private static string SanitizeArchiveEntryPath(string entryPath)
        {
            if (string.IsNullOrWhiteSpace(entryPath))
                return "unknown";

            var normalizedPath = entryPath.Replace('\\', '/').TrimStart('/');

            var components = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var safeComponents = new List<string>();

            foreach (var component in components)
            {
                if (string.IsNullOrEmpty(component) || component == "." || component == "..")
                    continue;

                if (component.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    var safeComponent = new string(component.Select(c =>
                        Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
                    if (!string.IsNullOrEmpty(safeComponent))
                        safeComponents.Add(safeComponent);
                }
                else
                {
                    safeComponents.Add(component);
                }
            }

            var safePath = string.Join("/", safeComponents);

            if (string.IsNullOrEmpty(safePath) || safePath.StartsWith("..") || safePath.Contains("/../"))
                return "unknown";

            return safePath;
        }

        #endregion
    }
}
