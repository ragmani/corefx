// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Xunit;

namespace System.IO.Tests
{
    public abstract class FileStream_ReadWrite_Span : FileSystemTest
    {
        protected abstract FileStream CreateFileStream(string path, FileMode mode, FileAccess access = FileAccess.ReadWrite);

        [Fact]
        public void DisposedStream_ReadWrite_Throws()
        {
            var fs = CreateFileStream(GetTestFilePath(), FileMode.Create);
            fs.Dispose();
            Assert.Throws<ObjectDisposedException>(() => fs.Read(new Span<byte>(new byte[1])));
            Assert.Throws<ObjectDisposedException>(() => fs.Write(new Span<byte>(new byte[1])));
        }

        [Fact]
        public void EmptyFile_Read_Succeeds()
        {
            using (var fs = CreateFileStream(GetTestFilePath(), FileMode.Create))
            {
                // use a recognizable pattern
                var buffer = (byte[])TestBuffer.Clone();

                Assert.Equal(0, fs.Read(Span<byte>.Empty));
                Assert.Equal(0, fs.Read(new Span<byte>(buffer, 0, 1)));
                Assert.Equal(TestBuffer, buffer);

                Assert.Equal(0, fs.Read(new Span<byte>(buffer, 0, buffer.Length)));
                Assert.Equal(TestBuffer, buffer);

                Assert.Equal(0, fs.Read(new Span<byte>(buffer, buffer.Length - 1, 1)));
                Assert.Equal(TestBuffer, buffer);

                Assert.Equal(0, fs.Read(new Span<byte>(buffer, buffer.Length / 2, buffer.Length - buffer.Length / 2)));
                Assert.Equal(TestBuffer, buffer);
            }
        }

        [Fact]
        public void NonEmptyFile_Read_GetsExpectedData()
        {
            string fileName = GetTestFilePath();
            File.WriteAllBytes(fileName, TestBuffer);

            using (var fs = CreateFileStream(fileName, FileMode.Open))
            {
                var buffer = new byte[TestBuffer.Length];
                Assert.Equal(TestBuffer.Length, fs.Read(new Span<byte>(buffer, 0, buffer.Length)));
                Assert.Equal(TestBuffer, buffer);

                // Larger than needed buffer, read into beginning, rest remains untouched
                fs.Position = 0;
                buffer = new byte[TestBuffer.Length * 2];
                Assert.Equal(TestBuffer.Length, fs.Read(new Span<byte>(buffer)));
                Assert.Equal(TestBuffer, buffer.Take(TestBuffer.Length));
                Assert.Equal(new byte[buffer.Length - TestBuffer.Length], buffer.Skip(TestBuffer.Length));

                // Larger than needed buffer, read into middle, beginning and end remain untouched
                fs.Position = 0;
                buffer = new byte[TestBuffer.Length * 2];
                Assert.Equal(TestBuffer.Length, fs.Read(new Span<byte>(buffer, 2, buffer.Length - 2)));
                Assert.Equal(TestBuffer, buffer.Skip(2).Take(TestBuffer.Length));
                Assert.Equal(new byte[2], buffer.Take(2));
                Assert.Equal(new byte[buffer.Length - TestBuffer.Length - 2], buffer.Skip(2 + TestBuffer.Length));
            }
        }

        [Fact]
        public void ReadOnly_Write_Throws()
        {
            string fileName = GetTestFilePath();
            File.WriteAllBytes(fileName, TestBuffer);

            using (var fs = CreateFileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                Assert.Throws<NotSupportedException>(() => fs.Write(new Span<byte>(new byte[1])));
                fs.Dispose();
                Assert.Throws<ObjectDisposedException>(() => fs.Write(new Span<byte>(new byte[1]))); // Disposed checking happens first
            }
        }

        [Fact]
        public void WriteOnly_Read_Throws()
        {
            using (var fs = CreateFileStream(GetTestFilePath(), FileMode.Create, FileAccess.Write))
            {
                Assert.Throws<NotSupportedException>(() => fs.Read(new Span<byte>(new byte[1])));
                fs.Dispose();
                Assert.Throws<ObjectDisposedException>(() => fs.Read(new Span<byte>(new byte[1]))); // Disposed checking happens first
            }
        }

        [Fact]
        public void EmptyWrites_NoDataWritten()
        {
            using (var fs = CreateFileStream(GetTestFilePath(), FileMode.Create))
            {
                fs.Write(Span<byte>.Empty);
                Assert.Equal(0, fs.Length);
                Assert.Equal(0, fs.Position);
            }
        }

        [Fact]
        public void NonEmptyWrite_WritesExpectedData()
        {
            using (var fs = CreateFileStream(GetTestFilePath(), FileMode.Create))
            {
                fs.Write(new Span<byte>(TestBuffer));
                Assert.Equal(TestBuffer.Length, fs.Length);
                Assert.Equal(TestBuffer.Length, fs.Position);

                fs.Position = 0;
                var buffer = new byte[TestBuffer.Length];
                Assert.Equal(TestBuffer.Length, fs.Read(new Span<byte>(buffer)));
                Assert.Equal(TestBuffer, buffer);
            }
        }
    }

    public class Sync_FileStream_ReadWrite_Span : FileStream_ReadWrite_Span
    {
        protected override FileStream CreateFileStream(string path, FileMode mode, FileAccess access) =>
            new FileStream(path, mode, access, FileShare.None, 0x1000, FileOptions.None);
    }

    public class Async_FileStream_ReadWrite_Span : FileStream_ReadWrite_Span
    {
        protected override FileStream CreateFileStream(string path, FileMode mode, FileAccess access) =>
            new FileStream(path, mode, access, FileShare.None, 0x1000, FileOptions.Asynchronous);
    }

    public sealed class Sync_DerivedFileStream_ReadWrite_Span : Sync_FileStream_ReadWrite_Span
    {
        protected override FileStream CreateFileStream(string path, FileMode mode, FileAccess access) =>
            new DerivedFileStream(path, mode, access, FileShare.None, 0x1000, FileOptions.None);

        [Fact]
        public void CallSpanReadWriteOnDerivedFileStream_ArrayMethodsUsed()
        {
            using (var fs = (DerivedFileStream)CreateFileStream(GetTestFilePath(), FileMode.Create, FileAccess.ReadWrite))
            {
                Assert.False(fs.WriteArrayInvoked);
                Assert.False(fs.ReadArrayInvoked);

                fs.Write(new ReadOnlySpan<byte>(new byte[1]));
                Assert.True(fs.WriteArrayInvoked);
                Assert.False(fs.ReadArrayInvoked);

                fs.Position = 0;
                fs.Read(new Span<byte>(new byte[1]));
                Assert.True(fs.WriteArrayInvoked);
                Assert.True(fs.ReadArrayInvoked);
            }
        }
    }

    public sealed class Async_DerivedFileStream_ReadWrite_Span : Async_FileStream_ReadWrite_Span
    {
        protected override FileStream CreateFileStream(string path, FileMode mode, FileAccess access) =>
            new DerivedFileStream(path, mode, access, FileShare.None, 0x1000, FileOptions.Asynchronous);
    }

    internal sealed class DerivedFileStream : FileStream
    {
        public bool ReadArrayInvoked = false, WriteArrayInvoked = false;

        public DerivedFileStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options) :
            base(path, mode, access, share, bufferSize, options)
        {
        }

        public override int Read(byte[] array, int offset, int count)
        {
            ReadArrayInvoked = true;
            return base.Read(array, offset, count);
        }

        public override void Write(byte[] array, int offset, int count)
        {
            WriteArrayInvoked = true;
            base.Write(array, offset, count);
        }
    }
}