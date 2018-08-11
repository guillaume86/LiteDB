﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB.Engine;

namespace LiteDB
{
    /// <summary>
    /// The LiteDB database. Used for create a LiteDB instance and use all storage resources. It's the database connection
    /// </summary>
    public partial class LiteDatabase : IDisposable
    {
        #region Properties

        private readonly Lazy<LiteEngine> _engine = null;
        private BsonMapper _mapper = BsonMapper.Global;

        /// <summary>
        /// Get current instance of BsonMapper used in this database instance (can be BsonMapper.Global)
        /// </summary>
        public BsonMapper Mapper { get { return _mapper; } }

        /// <summary>
        /// Get current database engine instance. Engine is lower data layer that works with BsonDocuments only (no mapper, no LINQ)
        /// </summary>
        public LiteEngine Engine { get { return _engine.Value; } }

        #endregion

        #region Ctor

        /// <summary>
        /// Starts LiteDB database using a connection string for file system database
        /// </summary>
        public LiteDatabase(string connectionString, BsonMapper mapper = null)
            : this(new ConnectionString(connectionString), mapper)
        {
        }

        /// <summary>
        /// Starts LiteDB database using a connection string for file system database
        /// </summary>
        public LiteDatabase(ConnectionString connectionString, BsonMapper mapper = null)
        {
            if(connectionString == null) throw new ArgumentNullException(nameof(connectionString));

            _mapper = mapper ?? BsonMapper.Global;

            _engine = new Lazy<LiteEngine>(() =>
            {
                var settings = new EngineSettings
                {
                    Filename = connectionString.Filename,
                    InitialSize = connectionString.InitialSize,
                    LimitSize = connectionString.LimitSize,
                    UtcDate = connectionString.UtcDate,
                    Timeout = connectionString.Timeout,
                    LogLevel = connectionString.Log
                };

                return new LiteEngine(settings);
            });
        }

        /// <summary>
        /// Starts LiteDB database using a Stream disk
        /// </summary>
        public LiteDatabase(Stream stream, BsonMapper mapper = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            _mapper = mapper ?? BsonMapper.Global;

            _engine = new Lazy<LiteEngine>(() =>
            {
                var settings = new EngineSettings
                {
                    DataStream = stream
                };

                return new LiteEngine(settings);
            });
        }

        /// <summary>
        /// Starts LiteDB database using custom LiteEngine settings
        /// </summary>
        public LiteDatabase(IEngineSettings settings, BsonMapper mapper = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _engine = new Lazy<LiteEngine>(() => new LiteEngine(settings));
            _mapper = mapper ?? BsonMapper.Global;
        }

        /// <summary>
        /// Starts LiteDB database using custom ILiteEngine implementation
        /// </summary>
        public LiteDatabase(LiteEngine engine, BsonMapper mapper = null)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            _engine = new Lazy<LiteEngine>(() => engine);
            _mapper = mapper ?? BsonMapper.Global;
        }

        #endregion

        #region Collections

        /// <summary>
        /// Get a collection using a entity class as strong typed document. If collection does not exits, create a new one.
        /// </summary>
        /// <param name="name">Collection name (case insensitive)</param>
        public LiteCollection<T> GetCollection<T>(string name)
        {
            return new LiteCollection<T>(name, BsonAutoId.ObjectId, _engine, _mapper);
        }

        /// <summary>
        /// Get a collection using a name based on typeof(T).Name (BsonMapper.ResolveCollectionName function)
        /// </summary>
        public LiteCollection<T> GetCollection<T>()
        {
            return this.GetCollection<T>(null);
        }

        /// <summary>
        /// Get a collection using a name based on typeof(T).Name (BsonMapper.ResolveCollectionName function)
        /// </summary>
        public LiteCollection<T> GetCollection<T>(BsonAutoId autoId)
        {
            return this.GetCollection<T>(null);
        }

        /// <summary>
        /// Get a collection using a generic BsonDocument. If collection does not exits, create a new one.
        /// </summary>
        /// <param name="name">Collection name (case insensitive)</param>
        /// <param name="autoId">Define autoId data type (when document contains no _id field)</param>
        public LiteCollection<BsonDocument> GetCollection(string name, BsonAutoId autoId = BsonAutoId.ObjectId)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(name));

            return new LiteCollection<BsonDocument>(name, autoId, _engine, _mapper);
        }

        #endregion

        #region Transaction

        /// <summary>
        /// Initialize a new transaction. Transaction are created "per-thread". There is only one single transaction per thread.
        /// Return true if transaction was created or false if current thread already in a transaction.
        /// </summary>
        public bool BeginTrans() => _engine.Value.BeginTrans();

        /// <summary>
        /// Commit current transaction
        /// </summary>
        public bool Commit() => _engine.Value.Commit();

        /// <summary>
        /// Rollback current transaction
        /// </summary>
        public bool Rollback() => _engine.Value.Rollback();

        #endregion

        #region FileStorage

        private LiteStorage<string> _fs = null;
        
        /// <summary>
        /// Returns a special collection for storage files/stream inside datafile. Use _files and _chunks collection names. FileId is implemented as string. Use "GetStorage" for custom options
        /// </summary>
        public LiteStorage<string> FileStorage
        {
            get { return _fs ?? (_fs = this.GetStorage<string>()); }
        }

        /// <summary>
        /// Get new instance of Storage using custom FileId type, custom "_files" collection name and custom "_chunks" collection. LiteDB support multiples file storages (using different files/chunks collection names)
        /// </summary>
        public LiteStorage<TFileId> GetStorage<TFileId>(string filesCollection = "_files", string chunksCollection = "_chunks")
        {
            return new LiteStorage<TFileId>(this, filesCollection, chunksCollection);
        }

        #endregion

        #region Shortcut

        /// <summary>
        /// Get all collections name inside this database.
        /// </summary>
        public IEnumerable<string> GetCollectionNames()
        {
            return _engine.Value.GetCollectionNames();
        }

        /// <summary>
        /// Checks if a collection exists on database. Collection name is case insensitive
        /// </summary>
        public bool CollectionExists(string name)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(name));

            return _engine.Value.GetCollectionNames().Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Drop a collection and all data + indexes
        /// </summary>
        public bool DropCollection(string name)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(name));

            return _engine.Value.DropCollection(name);
        }

        /// <summary>
        /// Rename a collection. Returns false if oldName does not exists or newName already exists
        /// </summary>
        public bool RenameCollection(string oldName, string newName)
        {
            if (oldName.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(oldName));
            if (newName.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(newName));

            return _engine.Value.RenameCollection(oldName, newName);
        }

        #endregion

        #region Execute SQL

        /// <summary>
        /// Execute SQL commands and return as data reader
        /// </summary>
        public BsonDataReader Execute(string command, BsonDocument parameters = null)
        {
            return _engine.Value.Execute(command, parameters);
        }

        /// <summary>
        /// Execute SQL commands and return as data reader
        /// </summary>
        public BsonDataReader Execute(string command, params BsonValue[] args)
        {
            var p = new BsonDocument();
            var index = 0;

            foreach(var arg in args)
            {
                p[index.ToString()] = arg;
                index++;
            }

            return _engine.Value.Execute(command, p);
        }

        #endregion

        #region Shrink/Analyze/Vaccum/Checkpoint/UserVersion

        /// <summary>
        /// Reduce disk size re-arranging unused spaces.
        /// </summary>
        public long Shrink()
        {
            return _engine.Value.Shrink();
        }

        /// <summary>
        /// Do datafile WAL checkpoint. Copy all commited transaction in log file into datafile.
        /// </summary>
        public int Checkpoint()
        {
            return _engine.Value.Checkpoint(false);
        }

        /// <summary>
        /// Analyze indexes in collections to better index choose decision
        /// </summary>
        public int Analyze(params string[] collections)
        {
            return _engine.Value.Analyze(collections);
        }

        /// <summary>
        /// Analyze all database to find-and-fix non linked empty pages
        /// </summary>
        public int Vaccum()
        {
            return _engine.Value.Vaccum();
        }

        /// <summary>
        /// Get/Set database user version - use this version number to control database change model
        /// </summary>
        public int UserVersion
        {
            get => _engine.Value.GetUserVersion();
            set => _engine.Value.SetUserVersion(value);
        }

        #endregion

        public void Dispose()
        {
            if (_engine.IsValueCreated) _engine.Value.Dispose();
        }
    }
}
