using System.Text;
using System.Text.Json;
using System.Xml;
using Flowly.Tool.QueueDiscovery;

namespace Flowly.Tool.Generation;

internal static class AzureServiceBusOutputGenerator
{
    public static string CreateEmulatorConfigJson(string @namespace, IReadOnlyList<QueueDiscoveryQueue> queueDefinitions)
    {
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
                        Topics = Array.Empty<object>()
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

    public static string CreateBicepTemplate(string namespaceResourceName, string serviceBusNamespaceName, IReadOnlyList<QueueDiscoveryQueue> queueDefinitions)
    {
        var sb = new StringBuilder();

        sb.AppendLine("@description('Name of the Azure Service Bus namespace.')");
        sb.AppendLine($"param serviceBusNamespaceName string = '{serviceBusNamespaceName}'");
        sb.AppendLine();
        sb.AppendLine("resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {");
        sb.AppendLine($"  name: serviceBusNamespaceName");
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

        return sb.ToString();
    }

    public static string CreateAspireBootstrapCode(string builderVariableName, string connectionName, string namespaceVariableName, IReadOnlyList<string> queueNames)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"var {namespaceVariableName} = {builderVariableName}.AddAzureServiceBus(\"{connectionName}\");");
        sb.AppendLine();

        foreach (var queueName in queueNames)
        {
            sb.AppendLine($"var {ToQueueVariableName(queueName)} = {namespaceVariableName}.AddServiceBusQueue(\"{queueName}\");");
        }

        return sb.ToString();
    }

    private static object CreateEmulatorQueue(QueueDiscoveryQueue queueDefinition)
    {
        return new
        {
            Name = queueDefinition.Name,
            Properties = new
            {
                DeadLetteringOnMessageExpiration = queueDefinition.DeadLetterOnMessageExpiration,
                DefaultMessageTimeToLive = ToIso8601Duration(queueDefinition.DefaultMessageTimeToLive),
                DuplicateDetectionHistoryTimeWindow = "PT10M",
                ForwardDeadLetteredMessagesTo = string.Empty,
                ForwardTo = string.Empty,
                LockDuration = ToIso8601Duration(queueDefinition.LockDuration),
                MaxDeliveryCount = 10,
                RequiresDuplicateDetection = false,
                RequiresSession = queueDefinition.RequiresSession
            }
        };
    }

    private static string ToIdentifier(string value)
    {
        var sanitized = new string(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "queue";
        }

        if (char.IsDigit(sanitized[0]))
        {
            return $"q_{sanitized}";
        }

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

        if (char.IsDigit(baseName[0]))
        {
            baseName = $"q{Capitalize(baseName)}";
        }

        return baseName.EndsWith("Queue", StringComparison.Ordinal)
            ? baseName
            : $"{baseName}Queue";
    }

    private static string Capitalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string ToIso8601Duration(TimeSpan value)
    {
        return XmlConvert.ToString(value);
    }
}
