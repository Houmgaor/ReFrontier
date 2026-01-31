using System;
using System.Buffers;

namespace LibReFrontier.Abstractions
{
    /// <summary>
    /// Helper class for buffer pooling to reduce GC pressure.
    /// Wraps ArrayPool for common use cases in file processing.
    /// </summary>
    public static class BufferPool
    {
        /// <summary>
        /// Rent a buffer from the shared pool.
        /// </summary>
        /// <param name="minimumLength">Minimum required length.</param>
        /// <returns>A buffer of at least the requested size.</returns>
        public static byte[] Rent(int minimumLength)
        {
            return ArrayPool<byte>.Shared.Rent(minimumLength);
        }

        /// <summary>
        /// Return a buffer to the shared pool.
        /// </summary>
        /// <param name="buffer">Buffer to return.</param>
        /// <param name="clearArray">Whether to clear the array before returning.</param>
        public static void Return(byte[] buffer, bool clearArray = false)
        {
            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray);
            }
        }

        /// <summary>
        /// Create a span of the exact requested size from a rented buffer.
        /// </summary>
        /// <param name="buffer">The rented buffer (may be larger than needed).</param>
        /// <param name="length">The actual data length.</param>
        /// <returns>A span of the exact size.</returns>
        public static Span<byte> GetSpan(byte[] buffer, int length)
        {
            return buffer.AsSpan(0, length);
        }

        /// <summary>
        /// Copy exact data from a rented buffer to a new array.
        /// Use this when you need to pass ownership (e.g., to WriteAllBytes).
        /// </summary>
        /// <param name="buffer">The rented buffer.</param>
        /// <param name="length">The actual data length.</param>
        /// <returns>A new array with the exact data.</returns>
        public static byte[] ToExactArray(byte[] buffer, int length)
        {
            var result = new byte[length];
            Array.Copy(buffer, 0, result, 0, length);
            return result;
        }
    }

    /// <summary>
    /// A rental wrapper that ensures the buffer is returned when disposed.
    /// </summary>
    public readonly struct RentedBuffer : IDisposable
    {
        private readonly byte[] _buffer;

        /// <summary>
        /// The actual data length (not the buffer length).
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// The underlying buffer array.
        /// </summary>
        public byte[] Array => _buffer;

        /// <summary>
        /// Get a span of the actual data.
        /// </summary>
        public Span<byte> Span => _buffer.AsSpan(0, Length);

        /// <summary>
        /// Create a new rented buffer.
        /// </summary>
        /// <param name="length">Required length.</param>
        public RentedBuffer(int length)
        {
            _buffer = BufferPool.Rent(length);
            Length = length;
        }

        /// <summary>
        /// Create a rented buffer and copy data into it.
        /// </summary>
        /// <param name="data">Data to copy.</param>
        public RentedBuffer(byte[] data)
        {
            _buffer = BufferPool.Rent(data.Length);
            Length = data.Length;
            System.Array.Copy(data, 0, _buffer, 0, data.Length);
        }

        /// <summary>
        /// Copy the data to a new exact-sized array.
        /// </summary>
        public byte[] ToArray()
        {
            return BufferPool.ToExactArray(_buffer, Length);
        }

        /// <summary>
        /// Return the buffer to the pool.
        /// </summary>
        public void Dispose()
        {
            BufferPool.Return(_buffer);
        }
    }
}
