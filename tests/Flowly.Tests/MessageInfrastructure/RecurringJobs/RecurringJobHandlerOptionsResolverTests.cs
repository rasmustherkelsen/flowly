using System.ComponentModel;
using Flowly.MessageInfrastructure.RecurringJobs;

namespace Flowly.Tests.MessageInfrastructure.RecurringJobs;

public class RecurringJobHandlerOptionsResolverTests
{
    public class Resolve
    {
        [Fact]
        public void WithRecurringJobAttribute_ReturnsAttributeDescription()
        {
            var result = RecurringJobHandlerOptionsResolver.Resolve<HandlerWithRecurringJobAttribute>();

            Assert.Equal("My Job", result.JobDescription);
        }

        [Fact]
        public void WithDescriptionAttribute_ReturnsDescriptionAttributeValue()
        {
            var result = RecurringJobHandlerOptionsResolver.Resolve<HandlerWithDescriptionAttribute>();

            Assert.Equal("My Description", result.JobDescription);
        }

        [Fact]
        public void WithRecurringJobAttributeAndDescriptionAttribute_RecurringJobAttributeTakesPrecedence()
        {
            var result = RecurringJobHandlerOptionsResolver.Resolve<HandlerWithBothAttributes>();

            Assert.Equal("RecurringJob Description", result.JobDescription);
        }

        [Fact]
        public void WithConfigureOverride_ConfigureOverridesDescriptionAttribute()
        {
            var result = RecurringJobHandlerOptionsResolver.Resolve<HandlerWithDescriptionAttributeAndConfigureOverride>();

            Assert.Equal("Configure Description", result.JobDescription);
        }

        [Fact]
        public void WithConfigureOverride_ConfigureOverridesRecurringJobAttribute()
        {
            var result = RecurringJobHandlerOptionsResolver.Resolve<HandlerWithRecurringJobAttributeAndConfigureOverride>();

            Assert.Equal("Configure Description", result.JobDescription);
        }

        [Fact]
        public void WithNoDescriptionSources_DerivesDescriptionFromTypeName()
        {
            var result = RecurringJobHandlerOptionsResolver.Resolve<ImportDataRecurringJob>();

            Assert.Equal("ImportData", result.JobDescription);
        }

        [Fact]
        public void WithDescriptionAttribute_ReturnsCronFromConfigure()
        {
            var result = RecurringJobHandlerOptionsResolver.Resolve<HandlerWithDescriptionAttribute>();

            Assert.Equal("0 * * * *", result.CronExpression);
        }

        [RecurringJob("My Job", "0 * * * *")]
        private class HandlerWithRecurringJobAttribute : RecurringJobHandlerBase
        {
            public override Task Handle(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        [Description("My Description")]
        private class HandlerWithDescriptionAttribute : RecurringJobHandlerBase
        {
            public override void Configure(RecurringJobHandlerOptions options) =>
                options.CronExpression = "0 * * * *";

            public override Task Handle(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        [RecurringJob("RecurringJob Description", "0 * * * *")]
        [Description("Description Attribute Value")]
        private class HandlerWithBothAttributes : RecurringJobHandlerBase
        {
            public override Task Handle(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        [Description("Description Attribute Value")]
        private class HandlerWithDescriptionAttributeAndConfigureOverride : RecurringJobHandlerBase
        {
            public override void Configure(RecurringJobHandlerOptions options)
            {
                options.CronExpression = "0 * * * *";
                options.JobDescription = "Configure Description";
            }

            public override Task Handle(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        [RecurringJob("RecurringJob Description", "0 * * * *")]
        private class HandlerWithRecurringJobAttributeAndConfigureOverride : RecurringJobHandlerBase
        {
            public override void Configure(RecurringJobHandlerOptions options) =>
                options.JobDescription = "Configure Description";

            public override Task Handle(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        [RecurringJob("", "0 * * * *")]
        private class ImportDataRecurringJob : RecurringJobHandlerBase
        {
            public override Task Handle(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
