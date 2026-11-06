using Azure.Messaging.EventHubs.Consumer;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsStartPositionsTests
{
    public class ToEventPosition
    {
        [Fact]
        public void First_IsEarliest()
        {
            Assert.Equal(EventPosition.Earliest, EventHubsStartPositions.ToEventPosition(StartPosition.First()));
        }

        [Fact]
        public void Last_IsLatest()
        {
            Assert.Equal(EventPosition.Latest, EventHubsStartPositions.ToEventPosition(StartPosition.Last()));
        }

        [Fact]
        public void Offset_IsExclusiveAfterThePreviousSequenceNumber()
        {
            var eventPosition = EventHubsStartPositions.ToEventPosition(StartPosition.Offset(5));

            Assert.Equal(EventPosition.FromSequenceNumber(4, isInclusive: false), eventPosition);
            Assert.NotEqual(EventPosition.FromSequenceNumber(5, isInclusive: true), eventPosition);
            Assert.NotEqual(EventPosition.FromSequenceNumber(4, isInclusive: true), eventPosition);
        }

        [Fact]
        public void OffsetZero_IsExclusiveAfterMinusOne()
        {
            Assert.Equal(EventPosition.FromSequenceNumber(-1, isInclusive: false), EventHubsStartPositions.ToEventPosition(StartPosition.Offset(0)));
        }

        [Fact]
        public void NegativeOffset_IsTheSamePositionAsOffsetZero()
        {
            Assert.Equal(EventPosition.FromSequenceNumber(-1, isInclusive: false), EventHubsStartPositions.ToEventPosition(StartPosition.Offset(-7)));
        }

        [Fact]
        public void OffsetBeyondTheLargestSequenceNumber_DoesNotOverflow()
        {
            Assert.Equal(EventPosition.FromSequenceNumber(long.MaxValue - 1, isInclusive: false), EventHubsStartPositions.ToEventPosition(StartPosition.Offset(long.MaxValue)));
        }

        [Fact]
        public void UtcTimestamp_IsTheEnqueuedTimeAtThatInstant()
        {
            var eventPosition = EventHubsStartPositions.ToEventPosition(StartPosition.Timestamp(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)));

            Assert.Equal(EventPosition.FromEnqueuedTime(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)), eventPosition);
        }

        [Fact]
        public void UnspecifiedTimestamp_IsTreatedAsUtc()
        {
            var eventPosition = EventHubsStartPositions.ToEventPosition(StartPosition.Timestamp(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified)));

            Assert.Equal(EventPosition.FromEnqueuedTime(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)), eventPosition);
        }

        [Fact]
        public void LocalTimestamp_IsTheEnqueuedTimeAtThatInstant()
        {
            var localTimestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local);

            var eventPosition = EventHubsStartPositions.ToEventPosition(StartPosition.Timestamp(localTimestamp));

            Assert.Equal(EventPosition.FromEnqueuedTime(new DateTimeOffset(localTimestamp.ToUniversalTime(), TimeSpan.Zero)), eventPosition);
        }
    }

    public class AfterSequenceNumber
    {
        [Fact]
        public void IsExclusiveSoTheEventWithThatSequenceNumberIsNotRead()
        {
            Assert.Equal(EventPosition.FromSequenceNumber(30, isInclusive: false), EventHubsStartPositions.AfterSequenceNumber(30));
            Assert.NotEqual(EventPosition.FromSequenceNumber(30, isInclusive: true), EventHubsStartPositions.AfterSequenceNumber(30));
        }

        [Fact]
        public void MinusOneDescribesTheBeginningOfTheStream()
        {
            Assert.Contains("[-1]", EventHubsStartPositions.AfterSequenceNumber(-1).ToString());
            Assert.Contains("Inclusive: [False]", EventHubsStartPositions.AfterSequenceNumber(-1).ToString());
        }
    }
}
