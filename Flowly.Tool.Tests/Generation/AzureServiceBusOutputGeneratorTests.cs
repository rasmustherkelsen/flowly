using System.Text.Json;
using Flowly.Tool.Generation;
using Flowly.Tool.QueueDiscovery;

namespace Flowly.Tool.Tests.Generation;

public class AzureServiceBusOutputGeneratorTests
{
    public class CreateBicepTemplate
    {
        [Fact]
        public void IncludesSubscriptionRuleResource_ForEachSubscription()
        {
            var events = new[]
            {
                new QueueDiscoveryEvent("order-placed", "notify-handler", "azure-service-bus", TimeSpan.FromDays(14), true)
            };

            var bicep = AzureServiceBusOutputGenerator.CreateBicepTemplate("sbNamespace", "my-servicebus", [], events);

            Assert.Contains("resource rule_order_placed_notify_handler", bicep);
            Assert.Contains("filterType: 'SqlFilter'", bicep);
            Assert.Contains("NOT EXISTS([flowly-target-subscription])", bicep);
            Assert.Contains("[flowly-target-subscription] = ''notify-handler''", bicep);
        }

        [Fact]
        public void SubscriptionRuleOverridesDefaultRule()
        {
            var events = new[]
            {
                new QueueDiscoveryEvent("order-placed", "notify-handler", "azure-service-bus", TimeSpan.FromDays(14), true)
            };

            var bicep = AzureServiceBusOutputGenerator.CreateBicepTemplate("sbNamespace", "my-servicebus", [], events);

            Assert.Contains("/$Default'", bicep);
        }

        [Fact]
        public void EachSubscriptionGetsItsOwnFilterExpression()
        {
            var events = new[]
            {
                new QueueDiscoveryEvent("order-placed", "notify-handler", "azure-service-bus", TimeSpan.FromDays(14), true),
                new QueueDiscoveryEvent("order-placed", "audit-handler", "azure-service-bus", TimeSpan.FromDays(14), true)
            };

            var bicep = AzureServiceBusOutputGenerator.CreateBicepTemplate("sbNamespace", "my-servicebus", [], events);

            Assert.Contains("[flowly-target-subscription] = ''notify-handler''", bicep);
            Assert.Contains("[flowly-target-subscription] = ''audit-handler''", bicep);
        }
    }

    public class CreateEmulatorConfigJson
    {
        [Fact]
        public void IncludesDefaultRuleWithSqlFilter_ForEachSubscription()
        {
            var events = new[]
            {
                new QueueDiscoveryEvent("order-placed", "notify-handler", "azure-service-bus", TimeSpan.FromHours(1), true)
            };

            var json = AzureServiceBusOutputGenerator.CreateEmulatorConfigJson("testns", [], events);
            var doc = JsonDocument.Parse(json);

            var subscription = doc.RootElement
                .GetProperty("UserConfig")
                .GetProperty("Namespaces")[0]
                .GetProperty("Topics")[0]
                .GetProperty("Subscriptions")[0];

            var rules = subscription.GetProperty("Rules");
            Assert.Equal(1, rules.GetArrayLength());

            var rule = rules[0];
            Assert.Equal("$Default", rule.GetProperty("Name").GetString());
            Assert.Equal("Sql", rule.GetProperty("Properties").GetProperty("FilterType").GetString());

            var sqlExpression = rule.GetProperty("Properties").GetProperty("SqlFilter").GetProperty("SqlExpression").GetString();
            Assert.Contains("NOT EXISTS([flowly-target-subscription])", sqlExpression);
            Assert.Contains("[flowly-target-subscription] = 'notify-handler'", sqlExpression);
        }

        [Fact]
        public void EachSubscriptionGetsItsOwnFilterExpression()
        {
            var events = new[]
            {
                new QueueDiscoveryEvent("order-placed", "notify-handler", "azure-service-bus", TimeSpan.FromHours(1), true),
                new QueueDiscoveryEvent("order-placed", "audit-handler", "azure-service-bus", TimeSpan.FromHours(1), true)
            };

            var json = AzureServiceBusOutputGenerator.CreateEmulatorConfigJson("testns", [], events);
            var doc = JsonDocument.Parse(json);

            var subscriptions = doc.RootElement
                .GetProperty("UserConfig")
                .GetProperty("Namespaces")[0]
                .GetProperty("Topics")[0]
                .GetProperty("Subscriptions");

            var expressions = Enumerable.Range(0, subscriptions.GetArrayLength())
                .Select(i => subscriptions[i].GetProperty("Rules")[0]
                    .GetProperty("Properties")
                    .GetProperty("SqlFilter")
                    .GetProperty("SqlExpression")
                    .GetString()!)
                .ToList();

            Assert.Contains(expressions, e => e.Contains("'notify-handler'"));
            Assert.Contains(expressions, e => e.Contains("'audit-handler'"));
        }
    }
}
