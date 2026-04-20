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

    public class CreateBicepTemplateEdgeCases
    {
        [Fact]
        public void WithNoQueuesAndNoEvents_StillIncludesNamespaceResource()
        {
            var bicep = AzureServiceBusOutputGenerator.CreateBicepTemplate("ns", "my-servicebus", [], []);

            Assert.Contains("resource serviceBusNamespace", bicep);
            Assert.Contains("param serviceBusNamespaceName string = 'my-servicebus'", bicep);
        }

        [Fact]
        public void WithSingleQueue_IncludesQueueResource()
        {
            var queues = new[]
            {
                new QueueDiscoveryQueue("order-placed", "azure-service-bus", false, TimeSpan.FromDays(1), true, TimeSpan.FromMinutes(5))
            };

            var bicep = AzureServiceBusOutputGenerator.CreateBicepTemplate("ns", "my-servicebus", queues, []);

            Assert.Contains("resource queue_order_placed", bicep);
            Assert.Contains("'${serviceBusNamespace.name}/order-placed'", bicep);
        }

        [Fact]
        public void WithQueueContainingSpecialCharacters_SanitizesToValidIdentifier()
        {
            var queues = new[]
            {
                new QueueDiscoveryQueue("order.placed-v2", "azure-service-bus", false, TimeSpan.FromDays(1), true, TimeSpan.FromMinutes(5))
            };

            var bicep = AzureServiceBusOutputGenerator.CreateBicepTemplate("ns", "my-servicebus", queues, []);

            Assert.Contains("resource queue_order_placed_v2", bicep);
        }

        [Fact]
        public void WithRequiresSessionTrue_EmitsRequiresSessionTrue()
        {
            var queues = new[]
            {
                new QueueDiscoveryQueue("order-placed", "azure-service-bus", true, TimeSpan.FromDays(1), true, TimeSpan.FromMinutes(5))
            };

            var bicep = AzureServiceBusOutputGenerator.CreateBicepTemplate("ns", "my-servicebus", queues, []);

            Assert.Contains("requiresSession: true", bicep);
        }

        [Fact]
        public void WithDeadLetterOnMessageExpirationFalse_EmitsLowercaseFalse()
        {
            var queues = new[]
            {
                new QueueDiscoveryQueue("order-placed", "azure-service-bus", false, TimeSpan.FromDays(1), false, TimeSpan.FromMinutes(5))
            };

            var bicep = AzureServiceBusOutputGenerator.CreateBicepTemplate("ns", "my-servicebus", queues, []);

            Assert.Contains("deadLetteringOnMessageExpiration: false", bicep);
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

        [Fact]
        public void WithNoQueuesAndNoEvents_ReturnsValidJsonWithEmptyArrays()
        {
            var json = AzureServiceBusOutputGenerator.CreateEmulatorConfigJson("testns", [], []);
            var doc = JsonDocument.Parse(json);

            var namespaceElement = doc.RootElement
                .GetProperty("UserConfig")
                .GetProperty("Namespaces")[0];

            Assert.Equal("testns", namespaceElement.GetProperty("Name").GetString());
            Assert.Equal(0, namespaceElement.GetProperty("Queues").GetArrayLength());
            Assert.Equal(0, namespaceElement.GetProperty("Topics").GetArrayLength());
        }

        [Fact]
        public void WithTtlLongerThanEmulatorMax_CapsAtOneHour()
        {
            var queues = new[]
            {
                new QueueDiscoveryQueue("order-placed", "azure-service-bus", false, TimeSpan.FromDays(14), true, TimeSpan.FromMinutes(5))
            };

            var json = AzureServiceBusOutputGenerator.CreateEmulatorConfigJson("testns", queues, []);
            var doc = JsonDocument.Parse(json);

            var ttl = doc.RootElement
                .GetProperty("UserConfig")
                .GetProperty("Namespaces")[0]
                .GetProperty("Queues")[0]
                .GetProperty("Properties")
                .GetProperty("DefaultMessageTimeToLive")
                .GetString();

            Assert.Equal("PT1H", ttl);
        }

        [Fact]
        public void WithTtlShorterThanEmulatorMax_UsesActualValue()
        {
            var queues = new[]
            {
                new QueueDiscoveryQueue("order-placed", "azure-service-bus", false, TimeSpan.FromMinutes(30), true, TimeSpan.FromMinutes(5))
            };

            var json = AzureServiceBusOutputGenerator.CreateEmulatorConfigJson("testns", queues, []);
            var doc = JsonDocument.Parse(json);

            var ttl = doc.RootElement
                .GetProperty("UserConfig")
                .GetProperty("Namespaces")[0]
                .GetProperty("Queues")[0]
                .GetProperty("Properties")
                .GetProperty("DefaultMessageTimeToLive")
                .GetString();

            Assert.Equal("PT30M", ttl);
        }

        [Fact]
        public void EventsOnSameTopic_GroupIntoSingleTopic()
        {
            var events = new[]
            {
                new QueueDiscoveryEvent("order-placed", "notify-handler", "azure-service-bus", TimeSpan.FromHours(1), true),
                new QueueDiscoveryEvent("order-placed", "audit-handler", "azure-service-bus", TimeSpan.FromHours(1), true)
            };

            var json = AzureServiceBusOutputGenerator.CreateEmulatorConfigJson("testns", [], events);
            var doc = JsonDocument.Parse(json);

            var topics = doc.RootElement
                .GetProperty("UserConfig")
                .GetProperty("Namespaces")[0]
                .GetProperty("Topics");

            Assert.Equal(1, topics.GetArrayLength());
            Assert.Equal(2, topics[0].GetProperty("Subscriptions").GetArrayLength());
        }
    }

    public class CreateAspireBootstrapCode
    {
        [Fact]
        public void WithNoQueuesAndNoEvents_OnlyEmitsNamespaceDeclaration()
        {
            var code = AzureServiceBusOutputGenerator.CreateAspireBootstrapCode(
                builderVariableName: "builder",
                connectionName: "EmulatorNamespace",
                namespaceVariableName: "sb",
                queueNames: [],
                eventDefinitions: []);

            Assert.Contains("var sb = builder.AddAzureServiceBus(\"EmulatorNamespace\");", code);
            Assert.DoesNotContain("AddServiceBusQueue", code);
            Assert.DoesNotContain("AddServiceBusTopic", code);
        }

        [Fact]
        public void WithQueues_EmitsAddServiceBusQueueForEach()
        {
            var code = AzureServiceBusOutputGenerator.CreateAspireBootstrapCode(
                builderVariableName: "builder",
                connectionName: "EmulatorNamespace",
                namespaceVariableName: "sb",
                queueNames: ["order-placed", "invoice-created"],
                eventDefinitions: []);

            Assert.Contains("sb.AddServiceBusQueue(\"order-placed\")", code);
            Assert.Contains("sb.AddServiceBusQueue(\"invoice-created\")", code);
        }

        [Fact]
        public void WithEvent_EmitsTopicAndSubscriptionCalls()
        {
            var events = new[]
            {
                new QueueDiscoveryEvent("order-placed", "notify-handler", "azure-service-bus", TimeSpan.FromHours(1), true)
            };

            var code = AzureServiceBusOutputGenerator.CreateAspireBootstrapCode(
                builderVariableName: "builder",
                connectionName: "EmulatorNamespace",
                namespaceVariableName: "sb",
                queueNames: [],
                eventDefinitions: events);

            Assert.Contains("sb.AddServiceBusTopic(\"order-placed\")", code);
            Assert.Contains(".AddServiceBusSubscription(\"notify-handler\")", code);
        }

        [Fact]
        public void WithMultipleSubscriptionsOnSameTopic_DeclaresTopicOnceWithMultipleSubscriptions()
        {
            var events = new[]
            {
                new QueueDiscoveryEvent("order-placed", "notify-handler", "azure-service-bus", TimeSpan.FromHours(1), true),
                new QueueDiscoveryEvent("order-placed", "audit-handler", "azure-service-bus", TimeSpan.FromHours(1), true)
            };

            var code = AzureServiceBusOutputGenerator.CreateAspireBootstrapCode(
                builderVariableName: "builder",
                connectionName: "EmulatorNamespace",
                namespaceVariableName: "sb",
                queueNames: [],
                eventDefinitions: events);

            var topicDeclarationCount = CountOccurrences(code, "AddServiceBusTopic(\"order-placed\")");
            var subscriptionCount = CountOccurrences(code, ".AddServiceBusSubscription(");

            Assert.Equal(1, topicDeclarationCount);
            Assert.Equal(2, subscriptionCount);
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
