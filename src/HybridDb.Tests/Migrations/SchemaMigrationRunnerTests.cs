using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Migrations
{
    public class SchemaMigrationRunnerTests : HybridDbStoreTests
    {
        [Fact]
        public void AutomaticallyCreatesMetadataTable()
        {
            var runner = new SchemaMigrationRunner(logger, new SchemaDiffer(), new List<Migration>());

            runner.Run(store);

            configuration.Tables.ShouldContainKey("HybridDb");
            database.RawQuery<int>("select top 1 SchemaVersion from #HybridDb").Single().ShouldBe(0);
        }

        [Fact]
        public void DoesNothingGivenNoMigrations()
        {
            CreateMetadataTable();

            var runner = new SchemaMigrationRunner(logger, new FakeSchemaDiffer(), new List<Migration>());

            runner.Run(store);

            database.QuerySchema().Single().Key.ShouldBe("HybridDb"); // the metadata table and nothing else
        }

        [Fact]
        public void RunsProvidedSchemaMigrations()
        {
            CreateMetadataTable();

            var runner = new SchemaMigrationRunner(logger,
                new FakeSchemaDiffer(),
                new InlineMigration(1,
                    new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), isPrimaryKey: true))),
                    new AddColumn("Testing", new Column("Noget", typeof (int)))));

            runner.Run(store);

            var tables = database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"]["Id"].ShouldNotBe(null);
            tables["Testing"]["Noget"].ShouldNotBe(null);
        }

        [Fact]
        public void RunsDiffedSchemaMigrations()
        {
            CreateMetadataTable();

            var runner = new SchemaMigrationRunner(logger,
                new FakeSchemaDiffer(
                    new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), isPrimaryKey: true))),
                    new AddColumn("Testing", new Column("Noget", typeof (int)))));

            runner.Run(store);

            var tables = database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"]["Id"].ShouldNotBe(null);
            tables["Testing"]["Noget"].ShouldNotBe(null);
        }

        [Fact]
        public void RunsProvidedSchemaMigrationsInOrderThenDiffed()
        {
            CreateMetadataTable();

            var table = new Table("Testing", new Column("Id", typeof(Guid), isPrimaryKey: true));

            var runner = new SchemaMigrationRunner(logger,
                new FakeSchemaDiffer(new RenameColumn(table, "Noget", "NogetNyt")),
                new InlineMigration(2, new AddColumn("Testing", new Column("Noget", typeof(int)))),
                new InlineMigration(1, new CreateTable(table)));

            runner.Run(store);

            var tables = database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"]["Id"].ShouldNotBe(null);
            tables["Testing"]["NogetNyt"].ShouldNotBe(null);
        }
        

        [Fact]
        public void DoesNotRunUnsafeSchemaMigrations()
        {
            CreateMetadataTable();

            var runner = new SchemaMigrationRunner(logger,
                new FakeSchemaDiffer(new UnsafeThrowingCommand()),
                new InlineMigration(1, new UnsafeThrowingCommand()));

            Should.NotThrow(() => runner.Run(store));
        }

        [Fact]
        public void DoesNotRunSchemaMigrationTwice()
        {
            CreateMetadataTable();

            var command = new CountingCommand();

            var runner = new SchemaMigrationRunner(logger,
                new FakeSchemaDiffer(), 
                new InlineMigration(1, command));

            runner.Run(store);
            runner.Run(store);

            command.NumberOfTimesCalled.ShouldBe(1);
        }

        [Fact]
        public void NextRunContinuesAtNextVersion()
        {
            CreateMetadataTable();

            var command = new CountingCommand();

            new SchemaMigrationRunner(logger, new FakeSchemaDiffer(), new InlineMigration(1, command))
                .Run(store);

            new SchemaMigrationRunner(logger,
                new FakeSchemaDiffer(),
                new InlineMigration(1, new ThrowingCommand()),
                new InlineMigration(2, command))
                .Run(store);

            command.NumberOfTimesCalled.ShouldBe(2);
        }

        [Fact]
        public void ThrowsIfSchemaVersionIsAhead()
        {
            CreateMetadataTable();

            new SchemaMigrationRunner(logger, new FakeSchemaDiffer(), new InlineMigration(1, new CountingCommand())).Run(store);

            Should.Throw<InvalidOperationException>(() => new SchemaMigrationRunner(logger, new FakeSchemaDiffer()).Run(store))
                .Message.ShouldBe("Database schema is ahead of configuration. Schema is version 1, but configuration is version 0.");
        }

        [Fact]
        public void RollsBackOnExceptions()
        {
            CreateMetadataTable();

            try
            {
                var runner = new SchemaMigrationRunner(logger,
                    new FakeSchemaDiffer(
                        new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), isPrimaryKey: true))),
                        new ThrowingCommand()));

                runner.Run(store);
            }
            catch (Exception)
            {
            }

            database.QuerySchema().ShouldNotContainKey("Testing");
        }

        [Fact]
        public void SetsRequiresReprojectionOnTablesWithNewColumns()
        {
            Document<Entity>();
            Document<AbstractEntity>();
            Document<DerivedEntity>();
            Document<OtherEntity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity());
                session.Store(new Entity());
                session.Store(new DerivedEntity());
                session.Store(new DerivedEntity());
                session.Store(new OtherEntity());
                session.Store(new OtherEntity());
                session.SaveChanges();
            }

            var runner = new SchemaMigrationRunner(logger, 
                new FakeSchemaDiffer(
                    new AddColumn("Entities", new Column("NewCol", typeof(int))),
                    new AddColumn("AbstractEntities", new Column("NewCol", typeof(int)))));
            
            runner.Run(store);

            database.RawQuery<bool>("select AwaitsReprojection from #Entities").ShouldAllBe(x => x);
            database.RawQuery<bool>("select AwaitsReprojection from #AbstractEntities").ShouldAllBe(x => x);
            database.RawQuery<bool>("select AwaitsReprojection from #OtherEntities").ShouldAllBe(x => !x);
        }

        [Fact]
        public void HandlesConcurrentRuns()
        {
            UseRealTables();
            CreateMetadataTable();

            var countingCommand = new CountingCommand();

            Func<SchemaMigrationRunner> runnerFactory = () =>
                new SchemaMigrationRunner(logger,
                    new SchemaDiffer(),
                    new InlineMigration(1,
                        new AddColumn("Other", new Column("Asger", typeof (int))),
                        new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), isPrimaryKey: true))),
                        new SlowCommand(),
                        countingCommand));

            InitializeStore();

            new CreateTable(new DocumentTable("Other")).Execute(database);

            Parallel.For(1, 10, x =>
            {
                Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                runnerFactory().Run(store);
            });

            countingCommand.NumberOfTimesCalled.ShouldBe(1);
        }

        void CreateMetadataTable()
        {
            new CreateTable(new Table("HybridDb", new Column("SchemaVersion", typeof(int)))).Execute(database);
        }

        public class FakeSchemaDiffer : ISchemaDiffer
        {
            readonly SchemaMigrationCommand[] commands;

            public FakeSchemaDiffer(params SchemaMigrationCommand[] commands)
            {
                this.commands = commands;
            }

            public IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(IReadOnlyList<Table> schema, Configuration configuration)
            {
                return commands.ToList();
            }
        }

        public class ThrowingCommand : SchemaMigrationCommand
        {
            public override void Execute(Database database)
            {
                throw new InvalidOperationException();
            }
        }

        public class UnsafeThrowingCommand : ThrowingCommand
        {
            public UnsafeThrowingCommand()
            {
                Unsafe = true;
            }
        }

        public class CountingCommand : SchemaMigrationCommand
        {
            public int NumberOfTimesCalled { get; private set; }

            public override void Execute(Database database)
            {
                NumberOfTimesCalled++;
            }
        }
        
        public class SlowCommand : SchemaMigrationCommand
        {
            public override void Execute(Database database)
            {
                Thread.Sleep(5000);
            }
        }
    }
}