# Semantic Kernel - MongoDB Connector

[![Build & Test](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.MongoDB/actions/workflows/build_test.yml/badge.svg)](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.MongoDB/actions/workflows/build_test.yml)
[![Create Release](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.MongoDB/actions/workflows/publish.yml/badge.svg)](https://github.com/kbeaugrand/SemanticKernel.Connectors.Memory.MongoDB/actions/workflows/publish.yml)
[![Version](https://img.shields.io/github/v/release/kbeaugrand/SemanticKernel.Connectors.Memory.MongoDB)](https://img.shields.io/github/v/release/kbeaugrand/SemanticKernel.Connectors.Memory.MongoDB)
[![License](https://img.shields.io/github/license/kbeaugrand/SemanticKernel.Connectors.Memory.MongoDB)](https://img.shields.io/github/v/release/kbeaugrand/SemanticKernel.Connectors.Memory.MongoDB)

This is a connector for the [Semantic Kernel](https://aka.ms/semantic-kernel).

It provides a connection to a MongoDB Atlas database for the Semantic Kernel for the memories.

> Note: It leverage on ``MongoDB Atlas Vector Search`` to provide vector search, this cannot work while running private instance of OpenSource MongoDB clusters.

## About Semantic Kernel

**Semantic Kernel (SK)** is a lightweight SDK enabling integration of AI Large
Language Models (LLMs) with conventional programming languages. The SK
extensible programming model combines natural language **semantic functions**,
traditional code **native functions**, and **embeddings-based memory** unlocking
new potential and adding value to applications with AI.

Please take a look at [Semantic Kernel](https://aka.ms/semantic-kernel) for more information.

## Installation

To install this memory store, you need to add the required nuget package to your project:

```dotnetcli
dotnet add package SemanticKernel.Connectors.Memory.MongoDB --version 1.0.0-beta1
```

## Create the collection and the Search index

Please refer to [the documentation](https://www.mongodb.com/docs/atlas/atlas-search/field-types/knn-vector/) to get more details on how to define an ``MongoDB Atlas Vector Search`` index. You can name the index default and create the index. Finally, write the following definition in the JSON editor on MongoDB Atlas:

```json
{
  "mappings": {
    "dynamic": true,
    "fields": {
      "Embedding": {
        "dimensions": 1536,
        "similarity": "cosine",
        "type": "knnVector"
      }
    }
  }
}
```

## Usage

To add your MongoDB Server memory connector, add the following statements to your kernel initialization code:

```csharp
using SemanticKernel.Connectors.Memory.MongoDB;
...
var kernel = Kernel.Builder
            ...
                .WithMemoryStorage(MongoDBMemoryStore.Connect(connectionString: <your_connection_string>, database: <your_database>))
            ...
                .Build();
```

The memory store will populate all the needed tables during startup and let you focus on the development of your plugin.

## License

This project is licensed under the [MIT License](LICENSE).
