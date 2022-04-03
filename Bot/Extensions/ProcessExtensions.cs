using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Extensions
{
    public static class ProcessExtensions
    {
        public static async Task<Stream> InputAsync(this Process process, Stream input, Int32 bufferSize = 4096, CancellationToken? token = default)
        {
            CancellationToken cancellationToken = token ?? CancellationToken.None;
            Stream output = new MemoryStream();

            Task standardInput = Task.Run(async () =>
            {
                input.Seek(0, SeekOrigin.Begin);
                Int32 total = default;
                Int32 count = default;
                do
                {
                    // Rent a buffer from the shared ArrayPool
                    Byte[] buffer = ArrayPool<Byte>.Shared.Rent(bufferSize);
                    // Initialize memory from the buffer
                    Memory<Byte> memory = new(buffer);
                    // Read characters from the input stream to memory and store the count of characters read
                    count = await input.ReadAsync(memory, cancellationToken);
                    // Write the read segment of memory to the standard input
                    await process.StandardInput.BaseStream.WriteAsync(memory[..count]);
                    // Return the buffer to the shared ArrayPool
                    ArrayPool<Byte>.Shared.Return(buffer);
                    // Increment total by count
                    total += count;
                }
                while (count is not default(Int32));
                Debug.WriteLine($"SI: Wrote {total}B");

                process.StandardInput.Close();
            });

            Task standardOutput = Task.Run(async () =>
            {
                output.Seek(0, SeekOrigin.Begin);
                Int32 total = default;
                Int32 count = default;
                do
                {
                    // Rent a buffer from the shared ArrayPool
                    Byte[] buffer = ArrayPool<Byte>.Shared.Rent(bufferSize);
                    // Initialize memory from the buffer
                    Memory<Byte> memory = new(buffer);
                    // Read characters from the standard output to memory and store the count of characters read
                    count = await process.StandardOutput.BaseStream.ReadAsync(memory, cancellationToken);
                    // Write the read segment of memory to the output stream
                    await output.WriteAsync(memory[..count]);
                    // Return the buffer to the shared ArrayPool
                    ArrayPool<Byte>.Shared.Return(buffer);
                    // Increment total by count
                    total += count;
                }
                while (count is not default(Int32) || process.HasExited is false);
                Debug.WriteLine($"SO: Read {total}B");
            });

            Task standardError = Task.Run(async () =>
            {
                do
                {
                    String? line = await process.StandardError.ReadLineAsync();
                    Debug.WriteLine($"SE: {line}");
                }
                while (process.HasExited is false);
            });

            await Task.WhenAll(standardInput, standardOutput, standardError);

            return output;
        }

        public static async Task<Stream> PipeAsync(this Process source, Process destination, Int32 bufferSize = 4096, CancellationToken? token = default)
        {
            CancellationToken cancellationToken = token ?? CancellationToken.None;
            Stream output = new MemoryStream();

            Task sourceOutput = Task.Run(async () =>
            {
                using Stream cache = new MemoryStream();

                Int32 total;
                Int32 count;

                total = default;
                count = default;
                do
                {
                    // Rent a buffer from the shared ArrayPool
                    Byte[] buffer = ArrayPool<Byte>.Shared.Rent(bufferSize);
                    // Initialize memory from the buffer
                    Memory<Byte> memory = new(buffer);
                    // Read characters from the standard output to memory and store the count of characters read
                    count = await source.StandardOutput.BaseStream.ReadAsync(memory, cancellationToken);
                    // Write the read segment of memory to the cache stream
                    await cache.WriteAsync(memory[..count]);
                    // Return the buffer to the shared ArrayPool
                    ArrayPool<Byte>.Shared.Return(buffer);
                    // Increment total by count
                    total += count;
                }
                while (count is not default(Int32) || source.HasExited is false);
                Debug.WriteLine($"SO: Read {total}B");

                cache.Seek(0, SeekOrigin.Begin);

                total = default;
                count = default;
                do
                {
                    // Rent a buffer from the shared ArrayPool
                    Byte[] buffer = ArrayPool<Byte>.Shared.Rent(bufferSize);
                    // Initialize memory from the buffer
                    Memory<Byte> memory = new(buffer);
                    // Read characters from the cache stream to memory and store the count of characters read
                    count = await cache.ReadAsync(memory, cancellationToken);
                    // Write the read segment of memory to the standard input
                    await destination.StandardInput.BaseStream.WriteAsync(memory[..count]);
                    // Return the buffer to the shared ArrayPool
                    ArrayPool<Byte>.Shared.Return(buffer);
                    // Increment total by count
                    total += count;
                }
                while (count is not default(Int32));
                Debug.WriteLine($"DI: Wrote {total}B");

                destination.StandardInput.Close();
            });

            Task destinationOutput = Task.Run(async () =>
            {
                output.Seek(0, SeekOrigin.Begin);
                Int32 total;
                Int32 count;

                total = default;
                count = default;
                do
                {
                    // Rent a buffer from the shared ArrayPool
                    Byte[] buffer = ArrayPool<Byte>.Shared.Rent(bufferSize);
                    // Initialize memory from the buffer
                    Memory<Byte> memory = new(buffer);
                    // Read characters from the standard output to memory and store the count of characters read
                    count = await destination.StandardOutput.BaseStream.ReadAsync(memory, cancellationToken);
                    // Write the read segment of memory to the storage Stream
                    await output.WriteAsync(memory[..count]);
                    // Return the buffer to the shared ArrayPool
                    ArrayPool<Byte>.Shared.Return(buffer);
                    // Increment total by count
                    total += count;
                }
                while (count is not default(Int32) || destination.HasExited is false);
                Debug.WriteLine($"DO: Read {total}B");
            });

            Task sourceError = Task.Run(async () =>
            {
                String? line = default;
                do
                {
                    line = await source.StandardError.ReadLineAsync();
                    Debug.WriteLine($"SE: {line}");
                }
                while (source.HasExited is false);
            });

            Task destinationError = Task.Run(async () =>
            {
                String? line = default;
                do
                {
                    line = await destination.StandardError.ReadLineAsync();
                    Debug.WriteLine($"DE: {line}");
                }
                while (destination.HasExited is false);
            });

            await Task.WhenAll(sourceOutput, sourceError, destinationOutput, destinationError);

            return output;
        }

        public static async Task<T?> DeserializeAsync<T>(this Process source, Int32 bufferSize = 4096, CancellationToken? token = default)
        {
            CancellationToken cancellationToken = token ?? CancellationToken.None;
            T? output = default;

            Task sourceOutput = Task.Run(async () =>
            {
                String? line = default;
                do
                {
                    line = await source.StandardOutput.ReadLineAsync();
                    Debug.WriteLine($"SE: {line}");
                }
                while (source.HasExited is false);
            });

            Task sourceError = Task.Run(async () =>
            {
                using Stream cache = new MemoryStream();

                Int32 total;
                Int32 count;

                total = default;
                count = default;
                do
                {
                    // Rent a buffer from the shared ArrayPool
                    Byte[] buffer = ArrayPool<Byte>.Shared.Rent(bufferSize);
                    // Initialize memory from the buffer
                    Memory<Byte> memory = new(buffer);
                    // Read characters from the standard output to memory and store the count of characters read
                    count = await source.StandardError.BaseStream.ReadAsync(memory, cancellationToken);
                    // Write the read segment of memory to the cache stream
                    await cache.WriteAsync(memory[..count]);
                    // Return the buffer to the shared ArrayPool
                    ArrayPool<Byte>.Shared.Return(buffer);
                    // Increment total by count
                    total += count;
                }
                while (count is not default(Int32) || source.HasExited is false);
                Debug.WriteLine($"SO: Read {total}B");

                cache.Seek(0, SeekOrigin.Begin);

                output = await JsonSerializer.DeserializeAsync<T>(cache, default(JsonSerializerOptions), cancellationToken);
            });

            await Task.WhenAll(sourceOutput, sourceError);

            return output;
        }
    }
}
