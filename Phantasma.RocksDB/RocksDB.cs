﻿using System;
using System.Collections.Generic;
using System.IO;
using Phantasma.Storage;
using Phantasma.Core;
using Logger = Phantasma.Core.Log.Logger;
using Phantasma.Core.Log;
using RocksDbSharp;

namespace Phantasma.RocksDB
{
    public class DBPartition : IKeyValueStoreAdapter
    {
	    private RocksDb _db;
        private ColumnFamilyHandle partition;
        private string partitionName;
        private string path;
        private readonly Logger logger;

        public uint Count => GetCount();

        public DBPartition(Logger logger, string fileName)
        {
            this.partitionName = Path.GetFileName(fileName);
            this.path = Path.GetDirectoryName(fileName);
            this.logger = logger;

            logger.Message("FileName: " + fileName);

            if (!path.EndsWith("/"))
            {
                path += '/';
            }

	        this._db = RocksDbStore.Instance(logger, path);

            // Create partition if it doesn't exist already
            try
            {
                logger.Message("Getting partition: " + this.partitionName);
                this.partition = this._db.GetColumnFamily(partitionName);
            }
            catch
            {
                logger.Message("Partition not found, create it now: " + this.partitionName);

                // TODO different partitions might need different options...
                this.partition = this._db.CreateColumnFamily(new ColumnFamilyOptions(), partitionName);
            }
        }

        #region internal_funcs
        private ColumnFamilyHandle GetPartition()
        {
            return _db.GetColumnFamily(partitionName);
        }

        private byte[] Get_Internal(byte[] key)
        {
            return _db.Get(key, cf: GetPartition());
        }

        private void Put_Internal(byte[] key, byte[] value)
        {
            _db.Put(key, value, cf: GetPartition());
        }

        private void Remove_Internal(byte[] key)
        {
            _db.Remove(key, cf: GetPartition());
        }
        #endregion

        public uint GetCount()
        {
            uint count = 0;
            var readOptions = new ReadOptions();
            using (var iter = _db.NewIterator(readOptions: readOptions, cf: GetPartition()))
            {
                iter.SeekToFirst();
                while (iter.Valid())
                {
                    count++;
                    iter.Next();
                }
            }

            return count;
        }

        public void Visit(Action<byte[], byte[]> visitor)
        {
            var readOptions = new ReadOptions();
            using (var iter = _db.NewIterator(readOptions: readOptions, cf: GetPartition()))
            {
                iter.SeekToFirst();
                while (iter.Valid())
                {
                    visitor(iter.Key(), iter.Value());
                    iter.Next();
                }
            }
        }

        public void SetValue(byte[] key, byte[] value)
        {
            Throw.IfNull(key, nameof(key));

            if (value == null || value.Length == 0)
            {
                Remove_Internal(key);
            }
            else
            {
                Put_Internal(key, value);
            }
        }

        public byte[] GetValue(byte[] key)
        {
            if (ContainsKey(key))
            {
                return Get_Internal(key);
            }

            return null;
        }

        public bool ContainsKey(byte[] key)
        {
            var result = Get_Internal(key);
            return (result != null  && result.Length > 0) ? true : false;
        }

        public bool Remove(byte[] key)
        {
            if (ContainsKey(key))
            {
                Remove_Internal(key);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public class RocksDbStore
    {
	    private static Dictionary<string, RocksDb> _db = new Dictionary<string, RocksDb>();
	    private static Dictionary<string, RocksDbStore> _rdb = new Dictionary<string, RocksDbStore>();
        private readonly Logger logger;

        private string fileName;

        private RocksDbStore(string fileName, Logger logger)
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown();
            this.logger = logger;
            logger.Message("RocksDBStore: " + fileName);
            this.fileName = fileName.Replace("\\", "/");

            var path = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            //TODO check options
            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            var columnFamilies = new ColumnFamilies
            {
                { "default", new ColumnFamilyOptions().OptimizeForPointLookup(256) },
                //{ "test", new ColumnFamilyOptions()
                //    //.SetWriteBufferSize(writeBufferSize)
                //    //.SetMaxWriteBufferNumber(maxWriteBufferNumber)
                //    //.SetMinWriteBufferNumberToMerge(minWriteBufferNumberToMerge)
                //    .SetMemtableHugePageSize(2 * 1024 * 1024)
                //    .SetPrefixExtractor(SliceTransform.CreateFixedPrefix((ulong)8))
                //    .SetBlockBasedTableFactory(bbto)
                //},
            };

            try
            {
                var partitionList = RocksDb.ListColumnFamilies(options, path);

                foreach (var partition in partitionList)
                {
                    columnFamilies.Add(partition, new ColumnFamilyOptions());
                }
            }
            catch
            {
                logger.Warning("Inital start, no partitions created yet!");
            }

            logger.Message("Opening database at: " + path);
	        _db.Add(fileName, RocksDb.Open(options, path, columnFamilies));
        }

        public static RocksDb Instance(Logger logger, string name)
        {
            if (!_db.ContainsKey(name))
            {
                if (string.IsNullOrEmpty(name)) throw new System.ArgumentException("Parameter cannot be null", "name");

                _rdb.Add(name, new RocksDbStore(name, logger));
            }

            return _db[name];
        }

        private void Shutdown()
        {

            logger.Message("Shutting down database...");
            foreach (var db in _db)
            {
                db.Value.Dispose();
            }
            logger.Message("Database has been shut down!");
        }

    }
}
