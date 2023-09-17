// Copyright (c) Kevin BEAUGRAND. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;

namespace SemanticKernel.Connectors.Memory.MongoDB;

/// <summary>
/// An implementation of <see cref="IMemoryStore"/> backed by a MongoDB server.
/// </summary>
/// <remarks>The data is saved to a MongoDB server, specified in the connection string of the factory method.
/// The data persists between subsequent instances.
/// </remarks>
public sealed class MongoDBMemoryStore : IMemoryStore
{
    private readonly IMongoDBClient _dbClient;

    /// <summary>
    /// Connects to a MongoDB database using the provided connection string and database, and returns a new instance of <see cref="MongoDBMemoryStore"/>.
    /// </summary>
    /// <param name="connectionString">The connection string to use for connecting to the MongoDB Server.</param>
    /// <param name="database">The database to use for the MongoDB Server.</param>
    /// <returns>A new instance of <see cref="MongoDBMemoryStore"/> connected to the specified MongoDB Server.</returns>
    public static MongoDBMemoryStore Connect(string connectionString, string database)
    {
        var client = new MongoDBClient(connectionString, database);

        return new MongoDBMemoryStore(client);
    }

    /// <summary>
    /// Represents a memory store implementation that uses a MongoDB Server as its backing store.
    /// </summary>
    public MongoDBMemoryStore(IMongoDBClient dbClient)
    {
        this._dbClient = dbClient;
    }

    /// <inheritdoc/>
    public async Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        await this._dbClient.CreateCollectionAsync(collectionName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        return await this._dbClient.DoesCollectionExistsAsync(collectionName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var collection in this._dbClient.GetCollectionsAsync(cancellationToken))
        {
            yield return collection;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        await this._dbClient.DeleteCollectionAsync(collectionName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<MemoryRecord?> GetAsync(string collectionName, string key, bool withEmbedding = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentNullException(nameof(collectionName));
        }

        var entry = await this._dbClient.ReadAsync(collectionName, key, withEmbedding, cancellationToken).ConfigureAwait(false);

        if (!entry.HasValue || entry == default(MongoDBMemoryEntry)) { return null; }

        return this.GetMemoryRecordFromEntry(entry.Value);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<MemoryRecord> GetBatchAsync(string collectionName, IEnumerable<string> keys, bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentNullException(nameof(collectionName));
        }

        foreach (MongoDBMemoryEntry entry in await this._dbClient.ReadBatchAsync(collectionName, keys, withEmbeddings, cancellationToken)
                        .ConfigureAwait(false))
        {
            yield return this.GetMemoryRecordFromEntry(entry);
        }
    }

    /// <inheritdoc/>
    public async Task<(MemoryRecord, double)?> GetNearestMatchAsync(string collectionName, ReadOnlyMemory<float> embedding, double minRelevanceScore = 0, bool withEmbedding = false, CancellationToken cancellationToken = default)
    {
        var nearest = this.GetNearestMatchesAsync(
                    collectionName: collectionName,
                    embedding: embedding,
                    limit: 1,
                    minRelevanceScore: minRelevanceScore,
                    withEmbeddings: withEmbedding,
                    cancellationToken: cancellationToken)
            .WithCancellation(cancellationToken);

        await foreach (var item in nearest)
        {
            return item;
        }

        return null;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<(MemoryRecord, double)> GetNearestMatchesAsync(string collectionName, ReadOnlyMemory<float> embedding, int limit, double minRelevanceScore = 0, bool withEmbeddings = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentNullException(nameof(collectionName));
        }

        if (limit <= 0)
        {
            yield break;
        }

        IAsyncEnumerable<(MongoDBMemoryEntry, double)> results = this._dbClient.GetNearestMatchesAsync(
            collectionName: collectionName,
            embedding: JsonSerializer.Serialize(embedding.ToArray()),
            limit: limit,
            minRelevanceScore: minRelevanceScore,
            withEmbeddings: withEmbeddings,
            cancellationToken: cancellationToken);

        await foreach ((MongoDBMemoryEntry entry, double cosineSimilarity) in results.ConfigureAwait(false))
        {
            yield return (this.GetMemoryRecordFromEntry(entry), cosineSimilarity);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string collectionName, string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentNullException(nameof(collectionName));
        }

        await this._dbClient.DeleteAsync(collectionName, key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentNullException(nameof(collectionName));
        }

        await this._dbClient.DeleteBatchAsync(collectionName, keys, cancellationToken).ConfigureAwait(false);
    }


    /// <inheritdoc/>
    public async Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentNullException(nameof(collectionName));
        }

        return await this.InternalUpsertAsync(collectionName, record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> UpsertBatchAsync(
        string collectionName,
        IEnumerable<MemoryRecord> records,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentNullException(nameof(collectionName));
        }

        foreach (MemoryRecord record in records)
        {
            yield return await this.InternalUpsertAsync(collectionName, record, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    private MemoryRecord GetMemoryRecordFromEntry(MongoDBMemoryEntry entry)
    {
        return MemoryRecord.FromJsonMetadata(
            json: entry.MetadataString,
            embedding: entry.Embedding == null ? Array.Empty<float>() : entry.Embedding.ToArray(),
            key: entry.Key,
            timestamp: entry.Timestamp
            );
    }

    /// <inheritdoc />
    private async Task<string> InternalUpsertAsync(string collectionName, MemoryRecord record, CancellationToken cancellationToken)
    {
        record.Key = record.Metadata.Id;

        await this._dbClient.UpsertAsync(
            collectionName: collectionName,
            key: record.Key,
            metadata: record.GetSerializedMetadata(),
            embedding: JsonSerializer.Serialize(record.Embedding.ToArray()),
            timestamp: record.Timestamp,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return record.Key;
    }
}
