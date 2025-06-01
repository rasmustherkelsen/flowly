using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SimpleTransit.MessageInfrastructure.BackgroundServices;
using SimpleTransit.MessageInfrastructure.Model;
using SimpleTransit.MessageInfrastructure.Senders;
using SimpleTransit.Repositories;
using SimpleTransit.Test.Utils;

namespace SimpleTransit.Test.MessageInfrastructure.BackgroundServices;

public class RecurringJobSchedulerBackgroundServiceTest
{
    public class ExecuteAsync
    {
        [Theory, AutoDataWithCustomization(typeof(SetupRecurringJobSchedulerBackgroundServiceForTest))]
        internal async Task MustSubmitEachJobThatIsDueForProcessing(
            RecurringJobSchedulerBackgroundService recurringJobSchedulerBackgroundService,
            IRecurringJobInvoker recurringJobInvoker,
            CancellationToken cancellationToken)
        {
            try
            {
                await recurringJobSchedulerBackgroundService.StartAsync(cancellationToken);
            }
            catch (TaskCanceledException)
            {
            }

            await recurringJobInvoker.Received(1).Submit(Arg.Is<RecurringJob>(r => r != null));
        }
    }

    private class SetupRecurringJobSchedulerBackgroundServiceForTest : ICustomization
    {
        public void Customize(IFixture fixture)
        {
            fixture.Customize(new AutoNSubstituteCustomization());

            fixture.Inject(new RecurringJobSchedulerBackgroundServiceOptions(TimeSpan.Zero));

            var recurringJobSubmitter = fixture.Create<IRecurringJobInvoker>();
            fixture.Inject(recurringJobSubmitter);

            var jobStateRepository = fixture.Create<IJobStateRepository>();
            fixture.Inject(jobStateRepository);

            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            fixture.Inject(cancellationToken);

            var dueJob = new RecurringJob(Guid.NewGuid(), "The Job", TimeSpan.FromSeconds(10), DateTime.UtcNow.AddHours(-1), null, null);
            jobStateRepository.GetRecurringJobs().Returns(_ =>
            {
                cancellationTokenSource.Cancel(); // Cancel the token
                return [dueJob];
            });

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(jobStateRepository);
            serviceCollection.AddSingleton(recurringJobSubmitter);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            fixture.Inject(serviceProvider.GetRequiredService<IServiceScopeFactory>());
        }
    }
}