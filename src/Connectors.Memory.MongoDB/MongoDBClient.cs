// Copyright (c) Kevin BEAUGRAND. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace SemanticKernel.Connectors.Memory.MongoDB;

/// <summary>
/// Represents a client for interacting with a MongoDBClient Server database for storing semantic memories and embeddings.
/// </summary>
public sealed class MongoDBClient : IMongoDBClient
{
    private readonly MongoClientSettings _settings;

    private readonly string _databaseName;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDBClient"/> class with the specified connection string and schema.
    /// </summary>
    /// <param name="connectionString">The connection string to use for connecting to the MongoDB Server.</param>
    /// <param name="databaseName">The database name.</param>
    public MongoDBClient(string connectionString, string databaseName)
    {
        _settings = MongoClientSettings.FromConnectionString(connectionString);

        // Set the ServerApi field of the settings object to Stable API version 1
        _settings.ServerApi = new ServerApi(ServerApiVersion.V1);

        _databaseName = databaseName;
    }

    /// <inheritdoc />
    public async Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        if (await this.DoesCollectionExistsAsync(collectionName, cancellationToken).ConfigureAwait(false))
        {
            // Collection already exists
            return;
        }

        var client = new MongoClient(_settings);

        var database = client.GetDatabase(_databaseName);

        database.CreateCollection(collectionName, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DoesCollectionExistsAsync(string collectionName,
        CancellationToken cancellationToken = default)
    {
        var client = new MongoClient(_settings);

        var database = client.GetDatabase(_databaseName);

        var collectionCursor = await database.ListCollectionNamesAsync(new ListCollectionNamesOptions
        {
            Filter = new BsonDocument("name", collectionName)
        }, cancellationToken).ConfigureAwait(false);

        return collectionCursor.Any(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GetCollectionsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = new MongoClient(_settings);

        var database = client.GetDatabase(_databaseName);

        var collectionCursor = await database.ListCollectionNamesAsync(cancellationToken: cancellationToken)
                                                .ConfigureAwait(false);

        var collectionNames = await collectionCursor.ToListAsync(cancellationToken: cancellationToken)
                                                .ConfigureAwait(false);

        foreach (var collectionName in collectionNames)
        {
            yield return collectionName;
        }
    }

    /// <inheritdoc />
    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        if (!(await this.DoesCollectionExistsAsync(collectionName, cancellationToken).ConfigureAwait(false)))
        {
            // Collection does not exist
            return;
        }

        var client = new MongoClient(_settings);

        var database = client.GetDatabase(_databaseName);

        await database.DropCollectionAsync(collectionName, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MongoDBMemoryEntry?> ReadAsync(string collectionName, string key, bool withEmbeddings = false, CancellationToken cancellationToken = default)
    {
        var collection = new MongoClient(_settings)
                            .GetDatabase(_databaseName)
                            .GetCollection<MongoDBMemoryEntry>(collectionName);

        var filter = Builders<MongoDBMemoryEntry>.Filter.Eq(entry => entry.Key, key);

        var projection = Builders<MongoDBMemoryEntry>.Projection
                                .Include(c => c.Collection)
                                .Include(c => c.Key)
                                .Include(c => c.MetadataString)
                                .Include(c => c.Timestamp);

        if (withEmbeddings)
        {
            projection = projection.Include(entry => entry.Embedding);
        }

        var options = new FindOptions<MongoDBMemoryEntry, MongoDBMemoryEntry>
        {
            Projection = projection
        };

        var cursor = await collection.FindAsync(filter, options, cancellationToken)
                                        .ConfigureAwait(false);

        return await cursor.FirstOrDefaultAsync(cancellationToken)
                                        .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MongoDBMemoryEntry>> ReadBatchAsync(string collectionName, IEnumerable<string> keys,
                                                                bool withEmbeddings = false,
                                                                CancellationToken cancellationToken = default)
    {
        var collection = new MongoClient(_settings)
                            .GetDatabase(_databaseName)
                            .GetCollection<MongoDBMemoryEntry>(collectionName);

        var filter = Builders<MongoDBMemoryEntry>.Filter.In(entry => entry.Key, keys);

        var projection = Builders<MongoDBMemoryEntry>.Projection
                                .Include(c => c.Collection)
                                .Include(c => c.Key)
                                .Include(c => c.MetadataString)
                                .Include(c => c.Timestamp);
        if (withEmbeddings)
        {
            projection = projection.Include(entry => entry.Embedding);
        }

        var options = new FindOptions<MongoDBMemoryEntry, MongoDBMemoryEntry>
        {
            Projection = projection
        };

        var cursor = await collection.FindAsync(filter, options, cancellationToken)
                                      .ConfigureAwait(false);

        return await cursor.ToListAsync(cancellationToken)
                            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string collectionName, string key, CancellationToken cancellationToken = default)
    {
        var client = new MongoClient(_settings);

        var database = client.GetDatabase(_databaseName);

        var collection = database.GetCollection<MongoDBMemoryEntry>(collectionName);

        var filter = Builders<MongoDBMemoryEntry>.Filter.Eq(entry => entry.Key, key);

        await collection.DeleteOneAsync(filter, cancellationToken)
                .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteBatchAsync(string collectionName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var collection = new MongoClient(_settings)
                            .GetDatabase(_databaseName)
                            .GetCollection<MongoDBMemoryEntry>(collectionName);

        var filter = Builders<MongoDBMemoryEntry>.Filter.In(entry => entry.Key, keys);

        await collection.DeleteManyAsync(filter, cancellationToken)
                            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<(MongoDBMemoryEntry, double)> GetNearestMatchesAsync(
        string collectionName,
        string embedding,
        int limit,
        double minRelevanceScore = 0,
        bool withEmbeddings = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var collection = new MongoClient(_settings)
                            .GetDatabase(_databaseName)
                            .GetCollection<MongoDBMemoryEntry>(collectionName);

        var embeddingArray = new BsonArray(JsonSerializer.Deserialize<float[]>(embedding));

        var pipeline = new BsonDocument[]
        {
            new BsonDocument("$search", new BsonDocument
            {
                { "index", $"default" },
                { "knnBeta", new BsonDocument
                    {
                        { "vector", embeddingArray },
                        { "path", "Embedding" },
                        { "k", limit }
                    }
                }
            }),
            new BsonDocument("$project", new BsonDocument
            {
                { nameof(MongoDBMemoryEntry.Key), 1 },
                { nameof(MongoDBMemoryEntry.MetadataString), 1 },
                { nameof(MongoDBMemoryEntry.Timestamp), 1 },
                {
                    "score", new BsonDocument
                    {
                        { "$meta", "searchScore" }
                    }
                }
            })
        };

        if (withEmbeddings)
        {
            pipeline[1].AsBsonDocument["$project"].AsBsonDocument.Add(nameof(MongoDBMemoryEntry.Embedding), 1);
        }

        var options = new AggregateOptions
        {
            AllowDiskUse = true
        };

        using var cursor = await collection.AggregateAsync<BsonDocument>(pipeline, options, cancellationToken)
                                            .ConfigureAwait(false);

        var items = await cursor.ToListAsync(cancellationToken: cancellationToken)
                                .ConfigureAwait(false);

        foreach (var item in items)
        {
            var entry = BsonSerializer.Deserialize<MongoDBMemoryEntry>(item);

            entry.Collection = collectionName;
            var score = item.GetValue("score", 0.0).ToDouble();

            yield return (entry, score);
        }
    }

    /// <inheritdoc />
    public async Task UpsertAsync(string collectionName,
        string key,
        string? metadata,
        string embedding,
        DateTimeOffset? timestamp,
        CancellationToken cancellationToken = default)
    {
        var client = new MongoClient(_settings);

        var database = client.GetDatabase(_databaseName);
        var collection = database.GetCollection<MongoDBMemoryEntry>(collectionName);

        var filter = Builders<MongoDBMemoryEntry>.Filter.Eq(entry => entry.Key, key);

        var embeddings = new ReadOnlyMemory<float>(JsonSerializer.Deserialize<float[]>(embedding)).ToArray();

        var update = Builders<MongoDBMemoryEntry>.Update
            .Set(entry => entry.MetadataString, metadata)
            .Set(entry => entry.Embedding, embeddings)
            .Set(entry => entry.Timestamp, timestamp ?? DateTimeOffset.UtcNow);

        var options = new UpdateOptions { IsUpsert = true };

        await collection.UpdateOneAsync(filter, update, options, cancellationToken)
                .ConfigureAwait(false);
    }
}
