# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DanbooruTaggingUI is a .NET MAUI cross-platform application designed as a prompt-building companion tool for tag-based image generation models (e.g., NovelAI, Pony, Illustrious, Stable Diffusion derivatives). Its main goal is to make it easier to discover, explore, and assemble tags when creating prompts, especially when the exact tag names are unknown or hard to remember.

The app ingests a large dataset of Danbooru-style tags (currently ~12,000, but potentially up to 100,000+ including characters and other groups). Tags are stored in a hierarchical graph database that reflects their semantic structure and relationships:

- `weapons → swords → katana`
- `animals → birds → eagle`
- `armor → helmet → samurai helmet`

Every row in the dataset represents a path where the final element is always a tag, and any intermediate nodes are organizational groups or parent tags. A tag can appear multiple times, once as a standalone entry and again as a parent with its own children.

## Architecture

### Project Structure
- **MAUI Blazor Hybrid App**: Cross-platform UI using Blazor components within MAUI
- **SQLite Database**: Local tag storage with hierarchical relationships
- **Tag Graph Service**: Core service for navigating tag DAG (Directed Acyclic Graph)
- **CSV Import System**: Bulk import of tag hierarchies from CSV files

### Key Components
- **TagGraphService**: Main service for tag navigation, search, and CRUD operations
- **TagImportService**: Handles CSV import and database population
- **TagDbInitializer**: Database schema creation and FTS (Full-Text Search) setup
- **TreeVm**: View models for tree/graph visualization

### Database Schema
The app uses SQLite and is structured to handle:

- **Nodes**: Each tag or group is a node (`node` table). Nodes know if they are a true tag or just a category.
- **Edges**: Parent–child relationships (`edge` table). This allows the tree or graph structure to be navigated.
- **Aliases**: Alternative names (`alias` table), useful for matching natural language search terms.
- **Paths**: Precomputed path strings (`path` table), used for quick breadcrumb trails and discovery.
- **FTS Index**: A full-text search index (`node_search` table) built with FTS5. This supports searching across node text, aliases, and path tokens with ranked results.

This structure supports:

- **Tree navigation**: drill down from broad groups into specific tags
- **Breadcrumbs**: walk upwards from any tag to its root categories
- **Relatedness**: find nearby tags (siblings, parents, children, or graph proximity)
- **Typeahead**: quick suggestions while typing, backed by alias and FTS search
- **Natural language search**: future plans include matching descriptive queries to likely tags using synonyms or embeddings

## Development Commands

### Build & Run
```bash
# Build the project
dotnet build DanbooruTaggingUI.csproj

# Run on specific platforms
dotnet run --framework net8.0-windows10.0.19041.0
dotnet run --framework net8.0-android
dotnet run --framework net8.0-ios
dotnet run --framework net8.0-maccatalyst
```

### Clean & Restore
```bash
dotnet clean
dotnet restore
```

## Data Management

### CSV Import Format
The application expects CSV files with hierarchical tag data:
```
category,subcategory,tag_name
character,female,example_character
```

### Database Operations
- **Graph Navigation**: Parents, children, siblings, breadcrumbs, subtrees
- **Search**: Exact match → prefix search → full-text search (BM25)
- **Relationships**: Related tags via graph proximity
- **CRUD**: Safe, idempotent node and edge operations

### Key Service Methods
```csharp
// Navigation
var children = tagGraphService.GetChildren(nodeId);
var breadcrumb = tagGraphService.GetBreadcrumb(nodeId);
var related = tagGraphService.GetRelatedSimple(nodeId);

// Search
var typeahead = tagGraphService.Typeahead(prefix);
var searchResults = tagGraphService.Search(query);

// CRUD
var node = tagGraphService.UpsertNode(text, isTag: true);
tagGraphService.AddEdge(parentId, childId);
```

## Service Registration

Services are registered in `MauiProgram.cs`:
```csharp
builder.Services.AddSingleton(new TagGraphService(dbPath));
```

Database initialization happens at startup:
```csharp
TagDbInitializer.Initialize(dbPath);
var importer = new TagImportService(dbPath, csvPath);
importer.ImportIfNeeded();
```

## File Conventions

- **Data/**: Core services for database operations
- **Models/**: View models and data transfer objects
- **Components/**: Blazor UI components
- **tags.csv**: Source data file bundled with the app
- **Database**: Created in `FileSystem.AppDataDirectory` as `tags.db`

## App Goals

The application serves as a semantic explorer for Danbooru-style tags with these key objectives:

- **Tree View**: Provide a tree view to drill down tag hierarchies for inspiration
- **Search & Typeahead**: Allow search and typeahead to find tags quickly when the name is partially known
- **Tag Discovery**: Enable tag discovery by showing related tags, siblings, parents, and multiple possible paths
- **Prompt Cart**: Support a prompt cart where users can collect tags into a working prompt string
- **Natural Language Queries**: Eventually support natural language queries (e.g., "a red medieval knight's helmet") and map them to relevant tags across categories, even if the user doesn't know the exact tag name
- **Scalability**: Provide a scalable foundation so the dataset can grow to tens of thousands of tags without performance issues

✨ **In short**: the app is a semantic explorer for Danbooru-style tags. It combines hierarchical navigation, alias handling, and full-text search to make tag-based prompting more intuitive and creative.

## Dependencies

- .NET 8.0 with MAUI framework
- Microsoft.Data.Sqlite for database operations
- SQLitePCLRaw.bundle_green for SQLite runtime
- Microsoft.AspNetCore.Components.WebView.Maui for Blazor UI