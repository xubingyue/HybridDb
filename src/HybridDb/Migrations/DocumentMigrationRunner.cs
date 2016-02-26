﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HybridDb.Commands;
using HybridDb.Config;
using Serilog;

namespace HybridDb.Migrations
{
    public class DocumentMigrationRunner
    {
        readonly ILogger logger;
        readonly IDocumentStore store;
        readonly Configuration configuration;

        public DocumentMigrationRunner(IDocumentStore store)
        {
            this.store = store;

            logger = store.Configuration.Logger;
            configuration = store.Configuration;
        }

        public Task RunInBackground()
        {
            return Task.Factory.StartNew(RunSynchronously, TaskCreationOptions.LongRunning);
        }

        public void RunSynchronously()
        {
            if (!store.Configuration.RunDocumentMigrationsOnStartup)
                return;
                
            var migrator = new DocumentMigrator(store.Configuration);

            foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
            {
                var baseDesign = configuration.DocumentDesigns.First(x => x.Table.Name == table.Name);

                while (true)
                {
                    QueryStats stats;

                    var rows = store
                        .Query(table, out stats,
                            @where: "AwaitsReprojection = @AwaitsReprojection or Version < @version",
                            @select: "Id, AwaitsReprojection, Version, Discriminator, Etag",
                            take: 100,
                            @orderby: "newid()",
                            parameters: new {AwaitsReprojection = true, version = configuration.ConfiguredVersion})
                        .ToList();

                    if (stats.TotalResults == 0) break;

                    logger.Information("Found {0} document that must be migrated.", stats.TotalResults);

                    foreach (var row in rows)
                    {
                        var key = (string)row[table.IdColumn];
                        var currentDocumentVersion = (int)row[table.VersionColumn];
                        var discriminator = ((string)row[table.DiscriminatorColumn]).Trim();
                        var concreteDesign = store.Configuration.GetOrCreateConcreteDesign(baseDesign, discriminator, key);

                        var shouldUpdate = false;

                        if ((bool)row[table.AwaitsReprojectionColumn])
                        {
                            shouldUpdate = true;
                            logger.Information("Reprojection document {0}/{1}.", 
                                concreteDesign.DocumentType.FullName, key, currentDocumentVersion, configuration.ConfiguredVersion);
                        }

                        if (migrator.ApplicableCommands(concreteDesign, currentDocumentVersion).Any())
                        {
                            shouldUpdate = true;
                        }

                        if (shouldUpdate)
                        {
                            try
                            {
                                using (var session = store.OpenSession())
                                {
                                    session.Load(concreteDesign, key);
                                    session.SaveChanges(lastWriteWins: false, forceWriteUnchangedDocument: true);
                                }
                            }
                            catch (ConcurrencyException) {}
                            catch (Exception exception)
                            {
                                logger.Error(exception, "Error while migrating document of type {0} with id {1}.", concreteDesign.DocumentType.FullName, key);
                                Thread.Sleep(100);
                            }
                        }
                        else
                        {
                            logger.Information("Document did not change.");

                            var projection = new Dictionary<string, object>
                            {
                                {table.VersionColumn, configuration.ConfiguredVersion}
                            };

                            store.Update(table, key, (Guid)row[table.EtagColumn], projection);
                        }
                    }
                }
            }
        }
    }
}