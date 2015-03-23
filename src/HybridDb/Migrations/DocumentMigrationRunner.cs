﻿using System;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Logging;

namespace HybridDb.Migrations
{
    public class DocumentMigrationRunner
    {
        readonly static object locker = new object();

        readonly ILogger logger;
        readonly IDocumentStore store;
        readonly Configuration configuration;

        public DocumentMigrationRunner(ILogger logger, IDocumentStore store)
        {
            this.logger = logger;
            this.store = store;
            
            configuration = store.Configuration;
        }

        public Task RunInBackground()
        {
            return Task.Factory.StartNew(RunSynchronously, TaskCreationOptions.LongRunning);
        }

        public void RunSynchronously()
        {
            lock (locker)
            {
                var migrator = new DocumentMigrator(store);

                foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
                {
                    var baseDesign = configuration.DocumentDesigns.First(x => x.Table.Name == table.Name);

                    while (true)
                    {
                        QueryStats stats;
                        var rows = store
                            .Query(table, out stats,
                                @where: "AwaitsReprojection = @AwaitsReprojection or Version < @version",
                                take: 100,
                                orderby: "newid()",
                                parameters: new {AwaitsReprojection = true, version = configuration.ConfiguredVersion});

                        if (stats.TotalResults == 0) break;

                        logger.Info("Found {0} document that must be migrated.", stats.TotalResults);

                        foreach (var row in rows)
                        {
                            var discriminator = ((string) row[table.DiscriminatorColumn]).Trim();

                            DocumentDesign concreteDesign;
                            if (!baseDesign.DecendentsAndSelf.TryGetValue(discriminator, out concreteDesign))
                            {
                                throw new InvalidOperationException(
                                    string.Format("Discriminator '{0}' was not found in configuration.", discriminator));
                            }

                            logger.Info("Trying to migrate document {0}/{1} from version {2} to {3}.", table, row[table.IdColumn], row[table.VersionColumn], configuration.ConfiguredVersion);

                            var entity = migrator.DeserializeAndMigrate(concreteDesign, (byte[]) row[table.DocumentColumn], (int)row[table.VersionColumn]);
                            var projections = concreteDesign.Projections.ToDictionary(x => x.Key, x => x.Value.Projector(entity));
                            projections.Add(table.VersionColumn, configuration.ConfiguredVersion);

                            try
                            {
                                store.Update(table, (Guid) row[table.IdColumn], (Guid) row[table.EtagColumn], projections);
                            }
                            catch (ConcurrencyException) { }
                        }
                    }
                }
            }
        }
    }
}