using System.Text;
using System.Text.Json;
using System.Xml;
using Flowly.Tool.QueueDiscovery;
using Flowly.Transport;

namespace Flowly.Tool.Generation;

internal static class AzureServiceBusOutputGenerator
{
    private static readonly TimeSpan EmulatorMaxMessageTimeToLive = TimeSpan.FromHours(1);
    private static readonly TimeSpan EmulatorMaxDuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(5);

    public static string CreateEmulatorConfigJson(string @namespace, IReadOnlyList<QueueDiscoveryQueue> queueDefinitions, IReadOnlyList<QueueDiscoveryEvent> eventDefinitions)
    {
        var topicGroups = eventDefinitions
            .GroupBy(e => e.TopicName, StringComparer.OrdinalIgnoreCase)
            .Select(g => CreateEmulatorTopic(g.Key, g.ToList()))
            .ToArray();

        var payload = new
        {
            UserConfig = new
            {
                Namespaces = new[]
                {
                    new
                    {
                        Name = @namespace,
                        Queues = queueDefinitions.Select(CreateEmulatorQueue).ToArray(),
                        Topics = topicGroups
                    }
                },
                Logging = new
                {
                    Type = "File"
                }
            }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public static string CreateBicepTemplate(string namespaceResourceName, string serviceBusNamespaceName, IReadOnlyList<QueueDiscoveryQueue> queueDefinitions, IReadOnlyList<QueueDiscoveryEvent> eventDefinitions)
    {
        var sb = new StringBuilder();

        sb.AppendLine("@description('Name of the Azure Service Bus namespace.')");
        sb.AppendLine($"param serviceBusNamespaceName string = '{serviceBusNamespaceName}'");
        sb.AppendLine();
        sb.AppendLine("resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {");
        sb.AppendLine("  name: serviceBusNamespaceName");
        sb.AppendLine("  location: resourceGroup().location");
        sb.AppendLine("  sku: {");
        sb.AppendLine("    name: 'Standard'");
        sb.AppendLine("    tier: 'Standard'");
        sb.AppendLine("  }");
        sb.AppendLine("  properties: {}");
        sb.AppendLine("}");
        sb.AppendLine();

        foreach (var queueDefinition in queueDefinitions)
        {
            var queueName = queueDefinition.Name;
            sb.AppendLine($"resource queue_{ToIdentifier(queueName)} 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {{");
            sb.AppendLine($"  name: '${{serviceBusNamespace.name}}/{queueName}'");
            sb.AppendLine("  properties: {");
            sb.AppendLine("    maxDeliveryCount: 10");
            sb.AppendLine($"    deadLetteringOnMessageExpiration: {queueDefinition.DeadLetterOnMessageExpiration.ToString().ToLowerInvariant()}");
            sb.AppendLine($"    defaultMessageTimeToLive: '{ToIso8601Duration(queueDefinition.DefaultMessageTimeToLive)}'");
            sb.AppendLine($"    lockDuration: '{ToIso8601Duration(queueDefinition.LockDuration)}'");
            sb.AppendLine($"    requiresSession: {queueDefinition.RequiresSession.ToString().ToLowerInvariant()}");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        foreach (var topicGroup in eventDefinitions.GroupBy(e => e.TopicName, StringComparer.OrdinalIgnoreCase))
        {
            var topicName = topicGroup.Key;
            var firstEvent = topicGroup.First();
            sb.AppendLine($"resource topic_{ToIdentifier(topicName)} 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {{");
            sb.AppendLine($"  name: '${{serviceBusNamespace.name}}/{topicName}'");
            sb.AppendLine("  properties: {");
            sb.AppendLine($"    defaultMessageTimeToLive: '{ToIso8601Duration(firstEvent.DefaultMessageTimeToLive)}'");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();

            foreach (var sub in topicGroup)
            {
                var subId = $"{ToIdentifier(topicName)}_{ToIdentifier(sub.SubscriptionName)}";
                sb.AppendLine($"resource subscription_{subId} 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {{");
                sb.AppendLine($"  name: '${{topic_{ToIdentifier(topicName)}.name}}/{sub.SubscriptionName}'");
                sb.AppendLine("  properties: {");
                sb.AppendLine("    maxDeliveryCount: 10");
                sb.AppendLine($"    deadLetteringOnMessageExpiration: {sub.DeadLetterOnMessageExpiration.ToString().ToLowerInvariant()}");
                sb.AppendLine($"    defaultMessageTimeToLive: '{ToIso8601Duration(sub.DefaultMessageTimeToLive)}'");
                sb.AppendLine("  }");
                sb.AppendLine("}");
                sb.AppendLine();

                sb.AppendLine($"resource rule_{subId} 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = {{");
                sb.AppendLine($"  name: '${{subscription_{subId}.name}}/$Default'");
                sb.AppendLine("  properties: {");
                sb.AppendLine("    filterType: 'SqlFilter'");
                sb.AppendLine("    sqlFilter: {");
                sb.AppendLine($"      sqlExpression: '(NOT EXISTS([{FlowlyMessageProperties.TargetSubscription}])) OR [{FlowlyMessageProperties.TargetSubscription}] = ''{sub.SubscriptionName}'''");
                sb.AppendLine("    }");
                sb.AppendLine("  }");
                sb.AppendLine($"  dependsOn: [subscription_{subId}]");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    public static string CreateAspireBootstrapCode(string builderVariableName, string connectionName, string namespaceVariableName, IReadOnlyList<string> queueNames, IReadOnlyList<QueueDiscoveryEvent> eventDefinitions)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"var {namespaceVariableName} = {builderVariableName}.AddAzureServiceBus(\"{connectionName}\");");
        sb.AppendLine();

        foreach (var queueName in queueNames) sb.AppendLine($"var {ToQueueVariableName(queueName)} = {namespaceVariableName}.AddServiceBusQueue(\"{queueName}\");");

        foreach (var topicGroup in eventDefinitions.GroupBy(e => e.TopicName, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            var topicName = topicGroup.Key;
            var topicVariable = ToQueueVariableName(topicName).Replace("Queue", "Topic");
            sb.AppendLine($"var {topicVariable} = {namespaceVariableName}.AddServiceBusTopic(\"{topicName}\");");
            foreach (var sub in topicGroup) sb.AppendLine($"{topicVariable}.AddServiceBusSubscription(\"{sub.SubscriptionName}\");");
        }

        return sb.ToString();
    }

    private static object CreateEmulatorQueue(QueueDiscoveryQueue queueDefinition)
    {
        return new
        {
            queueDefinition.Name,
            Properties = new
            {
                DeadLetteringOnMessageExpiration = queueDefinition.DeadLetterOnMessageExpiration,
                DefaultMessageTimeToLive = ToIso8601Duration(
                    queueDefinition.DefaultMessageTimeToLive > EmulatorMaxMessageTimeToLive
                        ? EmulatorMaxMessageTimeToLive
                        : queueDefinition.DefaultMessageTimeToLive),
                DuplicateDetectionHistoryTimeWindow = ToIso8601Duration(EmulatorMaxDuplicateDetectionHistoryTimeWindow),
                ForwardDeadLetteredMessagesTo = string.Empty,
                ForwardTo = string.Empty,
                LockDuration = ToIso8601Duration(queueDefinition.LockDuration),
                MaxDeliveryCount = 10,
                RequiresDuplicateDetection = false,
                queueDefinition.RequiresSession
            }
        };
    }

    private static object CreateEmulatorTopic(string topicName, IReadOnlyList<QueueDiscoveryEvent> subscriptions)
    {
        return new
        {
            Name = topicName,
            Properties = new
            {
                DefaultMessageTimeToLive = ToIso8601Duration(
                    subscriptions[0].DefaultMessageTimeToLive > EmulatorMaxMessageTimeToLive
                        ? EmulatorMaxMessageTimeToLive
                        : subscriptions[0].DefaultMessageTimeToLive),
                DuplicateDetectionHistoryTimeWindow = ToIso8601Duration(EmulatorMaxDuplicateDetectionHistoryTimeWindow),
                RequiresDuplicateDetection = false
            },
            Subscriptions = subscriptions.Select(s => new
            {
                Name = s.SubscriptionName,
                Properties = new
                {
                    DeadLetteringOnMessageExpiration = s.DeadLetterOnMessageExpiration,
                    DefaultMessageTimeToLive = ToIso8601Duration(
                        s.DefaultMessageTimeToLive > EmulatorMaxMessageTimeToLive
                            ? EmulatorMaxMessageTimeToLive
                            : s.DefaultMessageTimeToLive),
                    MaxDeliveryCount = 10
                },
                Rules = new[]
                {
                    new
                    {
                        Name = "$Default",
                        Properties = new
                        {
                            FilterType = "Sql",
                            SqlFilter = new
                            {
                                SqlExpression = $"(NOT EXISTS([{FlowlyMessageProperties.TargetSubscription}])) OR [{FlowlyMessageProperties.TargetSubscription}] = '{s.SubscriptionName}'"
                            }
                        }
                    }
                }
            }).ToArray()
        };
    }

    private static string ToIdentifier(string value)
    {
        var sanitized = new string(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        if (string.IsNullOrWhiteSpace(sanitized)) return "queue";

        if (char.IsDigit(sanitized[0])) return $"q_{sanitized}";

        return sanitized;
    }

    private static string ToQueueVariableName(string queueName)
    {
        var parts = queueName
            .Split(queueName.Where(ch => !char.IsLetterOrDigit(ch)).Distinct().ToArray(), StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.ToLowerInvariant())
            .ToArray();

        var baseName = parts.Length == 0
            ? "queue"
            : parts[0] + string.Concat(parts.Skip(1).Select(Capitalize));

        if (char.IsDigit(baseName[0])) baseName = $"q{Capitalize(baseName)}";

        return baseName.EndsWith("Queue", StringComparison.Ordinal)
            ? baseName
            : $"{baseName}Queue";
    }

    private static string Capitalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string ToIso8601Duration(TimeSpan value)
    {
        return XmlConvert.ToString(value);
    }
}