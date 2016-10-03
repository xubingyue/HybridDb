﻿using System;
using HybridDb.Config;

namespace HybridDb.Commands
{
    public class DeleteCommand : DatabaseCommand
    {
        public DocumentTable Table { get; }
        public string Key { get; }
        public Guid CurrentEtag { get; }
        public bool LastWriteWins { get; }

        public DeleteCommand(DocumentTable table, string key, Guid etag, bool lastWriteWins)
        {
            Table = table;
            Key = key;
            CurrentEtag = etag;
            LastWriteWins = lastWriteWins;
        }
    }
}