using Flowly;

namespace Messages;

/// <summary>
///     A temperature reading recorded onto the <c>sensor-reading</c> event hub, which has four partitions.
/// </summary>
/// <param name="SensorId">The sensor that took the reading. It is used as the partition key, so all readings of one sensor stay in order in one partition.</param>
/// <param name="Celsius">The measured temperature.</param>
/// <param name="Sequence">The running number of the reading, to make the order visible in the log.</param>
[StreamPartitions(4)]
public record SensorReadingMessage(string SensorId, double Celsius, int Sequence);
