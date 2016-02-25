﻿using System;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class NotGeneratingNullableColumnsForNullableTypes : HybridDbTests
    {
        [Fact]
        public void NullableGuidGetsNullableColumnType()
        {
            var store = DocumentStore.ForTesting(
                TableMode.UseTempTables,
                connectionString,
                new LambdaHybridDbConfigurator(config => config.Document<Entity>().With(x => x.SomeNullableGuid)));

            var column = store.Configuration.GetDesignFor<Entity>().Table["SomeNullableGuid"];
            column.Nullable.ShouldBe(true);
        }

        public class Entity
        {
            public Guid? SomeNullableGuid { get; set; }
        }
    }
}