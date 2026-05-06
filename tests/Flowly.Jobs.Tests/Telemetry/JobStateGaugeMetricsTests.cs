using System.Diagnostics.Metrics;
using Flowly.Jobs.Telemetry;
using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.Jobs.Tests.Telemetry;

[CollectionDefinition(nameof(JobStateGaugeMetricsTests), DisableParallelization = true)]
public class JobStateGaugeMetricsTestsCollection;

public class JobStateGaugeMetricsTests
{
    [Collection(nameof(JobStateGaugeMetricsTests))]
    public class Constructor
    {
        [Fact]
        public void WithTelemetryEnabled_PublishesFailedAndRunningGauges()
        {
            var publishedInstrumentNames = new List<string>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName)
                {
                    publishedInstrumentNames.Add(instrument.Name);
                }
            };
            meterListener.Start();

            using var jobStateGaugeMetrics = new JobStateGaugeMetrics(new FlowlyOptions { EnableTelemetry = true });

            Assert.Contains(FlowlyInstrumentationConstants.JobsFailed, publishedInstrumentNames);
            Assert.Contains(FlowlyInstrumentationConstants.JobsRunning, publishedInstrumentNames);
        }

        [Fact]
        public void WithTelemetryDisabled_DoesNotPublishAnyGauges()
        {
            var publishedInstrumentNames = new List<string>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName)
                {
                    publishedInstrumentNames.Add(instrument.Name);
                }
            };
            meterListener.Start();

            using var jobStateGaugeMetrics = new JobStateGaugeMetrics(new FlowlyOptions { EnableTelemetry = false });

            Assert.Empty(publishedInstrumentNames);
        }

        [Fact]
        public void InitialFailedAndRunningCountsAreZero()
        {
            var observed = new Dictionary<string, long>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) => observed[instrument.Name] = value);
            meterListener.Start();

            using var jobStateGaugeMetrics = new JobStateGaugeMetrics(new FlowlyOptions { EnableTelemetry = true });
            meterListener.RecordObservableInstruments();

            Assert.Equal(0, observed[FlowlyInstrumentationConstants.JobsFailed]);
            Assert.Equal(0, observed[FlowlyInstrumentationConstants.JobsRunning]);
        }
    }

    [Collection(nameof(JobStateGaugeMetricsTests))]
    public class UpdateCounts
    {
        [Fact]
        public void UpdatesFailedAndRunningGaugeValues()
        {
            var observed = new Dictionary<string, long>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) => observed[instrument.Name] = value);
            meterListener.Start();

            using var jobStateGaugeMetrics = new JobStateGaugeMetrics(new FlowlyOptions { EnableTelemetry = true });

            jobStateGaugeMetrics.UpdateCounts(failed: 7, running: 3);
            meterListener.RecordObservableInstruments();

            Assert.Equal(7, observed[FlowlyInstrumentationConstants.JobsFailed]);
            Assert.Equal(3, observed[FlowlyInstrumentationConstants.JobsRunning]);
        }

        [Fact]
        public void OverwritesPreviousValuesOnSubsequentCalls()
        {
            var observed = new Dictionary<string, long>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) => observed[instrument.Name] = value);
            meterListener.Start();

            using var jobStateGaugeMetrics = new JobStateGaugeMetrics(new FlowlyOptions { EnableTelemetry = true });

            jobStateGaugeMetrics.UpdateCounts(failed: 10, running: 20);
            jobStateGaugeMetrics.UpdateCounts(failed: 1, running: 2);
            meterListener.RecordObservableInstruments();

            Assert.Equal(1, observed[FlowlyInstrumentationConstants.JobsFailed]);
            Assert.Equal(2, observed[FlowlyInstrumentationConstants.JobsRunning]);
        }

        [Fact]
        public void WithTelemetryDisabled_DoesNotThrow()
        {
            using var jobStateGaugeMetrics = new JobStateGaugeMetrics(new FlowlyOptions { EnableTelemetry = false });

            jobStateGaugeMetrics.UpdateCounts(failed: 5, running: 9);
        }

        [Fact]
        public void WithNegativeValues_StoresThemAsObserved()
        {
            var observed = new Dictionary<string, long>();
            using var meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) => observed[instrument.Name] = value);
            meterListener.Start();

            using var jobStateGaugeMetrics = new JobStateGaugeMetrics(new FlowlyOptions { EnableTelemetry = true });

            jobStateGaugeMetrics.UpdateCounts(failed: -1, running: -2);
            meterListener.RecordObservableInstruments();

            Assert.Equal(-1, observed[FlowlyInstrumentationConstants.JobsFailed]);
            Assert.Equal(-2, observed[FlowlyInstrumentationConstants.JobsRunning]);
        }
    }

    [Collection(nameof(JobStateGaugeMetricsTests))]
    public class Dispose
    {
        [Fact]
        public void WithTelemetryEnabled_DoesNotThrow()
        {
            var jobStateGaugeMetrics = new JobStateGaugeMetrics(new FlowlyOptions { EnableTelemetry = true });

            jobStateGaugeMetrics.Dispose();
        }

        [Fact]
        public void WithTelemetryDisabled_DoesNotThrow()
        {
            var jobStateGaugeMetrics = new JobStateGaugeMetrics(new FlowlyOptions { EnableTelemetry = false });

            jobStateGaugeMetrics.Dispose();
        }

        [Fact]
        public void CalledMultipleTimes_DoesNotThrow()
        {
            var jobStateGaugeMetrics = new JobStateGaugeMetrics(new FlowlyOptions { EnableTelemetry = true });

            jobStateGaugeMetrics.Dispose();
            jobStateGaugeMetrics.Dispose();
        }
    }
}
