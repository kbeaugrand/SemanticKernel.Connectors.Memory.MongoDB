// Copyright (c) Kevin BEAUGRAND. All rights reserved.

using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SemanticKernel.Connectors.Memory.MongoDB;

/// <summary>
/// A MongoDB memory entry.
/// </summary>
[BsonIgnoreExtraElements]
public record struct MongoDBMemoryEntry
{
    /// <summary>
    /// The unique identitfier of the memory entry.
    /// </summary>
    public ObjectId Id { get; set; }

    /// <summary>
    /// The entry collection name.
    /// </summary>
    public string Collection { get; set; }

    /// <summary>
    /// Unique identifier of the memory entry in the collection.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Metadata as a string.
    /// </summary>
    public string MetadataString { get; set; }

    /// <summary>
    /// The embedding.
    /// </summary>
    public IEnumerable<float>? Embedding { get; set; }

    /// <summary>
    /// Optional timestamp. Its 'DateTimeKind' is <see cref="DateTimeKind.Utc"/>
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }
}
