using Flowly.DeadLetters.DatabaseModel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Flowly.DeadLetters.Tests.Migrations;

public class DeadLetterMigrationsTests
{
    private const string MigrationName = "AddReplacementMessageBody";

    private const string InitialSqliteMigrationId = "20260515085218_Initial";

    private static DeadLetterDataContext SqlServerContext()
        => new(new DbContextOptionsBuilder<DeadLetterDataContext>()
            .UseSqlServer("Server=unused;Database=unused", options => options.MigrationsAssembly(typeof(SqlServerDeadLetterTrackingRegistrationExtension).Assembly.GetName().Name))
            .Options);

    private static DeadLetterDataContext PostgresContext()
        => new(new DbContextOptionsBuilder<DeadLetterDataContext>()
            .UseNpgsql("Host=unused;Database=unused", options => options.MigrationsAssembly(typeof(PostgresDeadLetterTrackingRegistrationExtension).Assembly.GetName().Name))
            .Options);

    private static DeadLetterDataContext SqliteContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<DeadLetterDataContext>()
            .UseSqlite(connection, options => options.MigrationsAssembly(typeof(SQLiteDeadLetterTrackingRegistrationExtension).Assembly.GetName().Name))
            .Options);

    private static AddColumnOperation ReplacementColumnOperation(DeadLetterDataContext context)
    {
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var migrationId = migrationsAssembly.Migrations.Keys.Single(id => id.EndsWith($"_{MigrationName}"));
        var migration = migrationsAssembly.CreateMigration(migrationsAssembly.Migrations[migrationId], context.Database.ProviderName!);

        return Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());
    }

    public class SqlServer
    {
        [Fact]
        public void ModelHasNoPendingChanges()
        {
            using var context = SqlServerContext();

            Assert.False(context.Database.HasPendingModelChanges());
        }

        [Fact]
        public void AddsANullableReplacementMessageBodyColumn()
        {
            using var context = SqlServerContext();

            var operation = ReplacementColumnOperation(context);

            Assert.Equal("DeadLetters", operation.Table);
            Assert.Equal("ReplacementMessageBody", operation.Name);
            Assert.True(operation.IsNullable);
            Assert.Equal("nvarchar(max)", operation.ColumnType);
        }
    }

    public class Postgres
    {
        [Fact]
        public void ModelHasNoPendingChanges()
        {
            using var context = PostgresContext();

            Assert.False(context.Database.HasPendingModelChanges());
        }

        [Fact]
        public void AddsANullableReplacementMessageBodyColumn()
        {
            using var context = PostgresContext();

            var operation = ReplacementColumnOperation(context);

            Assert.Equal("DeadLetters", operation.Table);
            Assert.Equal("ReplacementMessageBody", operation.Name);
            Assert.True(operation.IsNullable);
            Assert.Equal("text", operation.ColumnType);
        }
    }

    public class Sqlite
    {
        [Fact]
        public void ModelHasNoPendingChanges()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            using var context = SqliteContext(connection);

            Assert.False(context.Database.HasPendingModelChanges());
        }

        [Fact]
        public void AddsANullableReplacementMessageBodyColumn()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            using var context = SqliteContext(connection);

            var operation = ReplacementColumnOperation(context);

            Assert.Equal("DeadLetters", operation.Table);
            Assert.Equal("ReplacementMessageBody", operation.Name);
            Assert.True(operation.IsNullable);
            Assert.Equal("TEXT", operation.ColumnType);
        }

        [Fact]
        public async Task AppliedToAFreshDatabase_StoresAndReadsTheReplacementMessageBody()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var context = SqliteContext(connection);
            await context.Database.MigrateAsync();
            context.DeadLetters.Add(new DeadLetter
            {
                MessageId = "msg-1",
                QueueName = "orders",
                MessageBody = "original",
                MessageProperties = "{}",
                DeadLetteredAt = DateTimeOffset.UtcNow,
                ReplacementMessageBody = "corrected"
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var deadLetter = await context.DeadLetters.SingleAsync();

            Assert.Equal("original", deadLetter.MessageBody);
            Assert.Equal("corrected", deadLetter.ReplacementMessageBody);
        }

        [Fact]
        public async Task AppliedOnTopOfTheInitialMigration_KeepsExistingRowsWithoutAReplacement()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var context = SqliteContext(connection);
            await context.Database.GetService<IMigrator>().MigrateAsync(InitialSqliteMigrationId);
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO DeadLetters (MessageId, Status, QueueName, MessageBody, MessageProperties, DeadLetteredAt) VALUES ('msg-1', 'Pending', 'orders', 'original', '{}', 0)";
            await insert.ExecuteNonQueryAsync();

            await context.Database.MigrateAsync();

            var deadLetter = await context.DeadLetters.AsNoTracking().SingleAsync();
            Assert.Equal("original", deadLetter.MessageBody);
            Assert.Null(deadLetter.ReplacementMessageBody);
            Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        }
    }
}
