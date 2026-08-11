namespace Flowly.Tests;

public class StartPositionTests
{
    public class First
    {
        [Fact]
        public void Match_InvokesFirstFunction()
        {
            var result = StartPosition.First().Match(
                () => "first",
                () => "last",
                _ => "offset",
                _ => "timestamp");

            Assert.Equal("first", result);
        }

        [Fact]
        public void EqualsAnotherFirst()
        {
            Assert.Equal(StartPosition.First(), StartPosition.First());
        }

        [Fact]
        public void DoesNotEqualLast()
        {
            Assert.NotEqual(StartPosition.First(), StartPosition.Last());
        }
    }

    public class Last
    {
        [Fact]
        public void Match_InvokesLastFunction()
        {
            var result = StartPosition.Last().Match(
                () => "first",
                () => "last",
                _ => "offset",
                _ => "timestamp");

            Assert.Equal("last", result);
        }

        [Fact]
        public void EqualsAnotherLast()
        {
            Assert.Equal(StartPosition.Last(), StartPosition.Last());
        }
    }

    public class Offset
    {
        [Fact]
        public void Match_InvokesOffsetFunctionWithValue()
        {
            var result = StartPosition.Offset(42).Match(
                () => 0L,
                () => 0L,
                offset => offset,
                _ => 0L);

            Assert.Equal(42, result);
        }

        [Fact]
        public void EqualsOffsetWithSameValue()
        {
            Assert.Equal(StartPosition.Offset(42), StartPosition.Offset(42));
        }

        [Fact]
        public void DoesNotEqualOffsetWithDifferentValue()
        {
            Assert.NotEqual(StartPosition.Offset(42), StartPosition.Offset(43));
        }

        [Fact]
        public void OffsetZero_DoesNotEqualFirst()
        {
            Assert.NotEqual(StartPosition.Offset(0), StartPosition.First());
        }
    }

    public class Timestamp
    {
        [Fact]
        public void Match_InvokesTimestampFunctionWithValue()
        {
            var timestamp = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

            var result = StartPosition.Timestamp(timestamp).Match(
                () => default,
                () => default,
                _ => default,
                value => value);

            Assert.Equal(timestamp, result);
        }

        [Fact]
        public void EqualsTimestampWithSameValue()
        {
            var timestamp = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

            Assert.Equal(StartPosition.Timestamp(timestamp), StartPosition.Timestamp(timestamp));
        }

        [Fact]
        public void DoesNotEqualTimestampWithDifferentValue()
        {
            Assert.NotEqual(
                StartPosition.Timestamp(new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc)),
                StartPosition.Timestamp(new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc)));
        }
    }

    public class GetHashCodeMethod
    {
        [Fact]
        public void EqualPositions_HaveEqualHashCodes()
        {
            Assert.Equal(StartPosition.Offset(7).GetHashCode(), StartPosition.Offset(7).GetHashCode());
        }
    }

    public class EqualsObject
    {
        [Fact]
        public void WithNonStartPosition_ReturnsFalse()
        {
            Assert.False(StartPosition.First().Equals("first"));
        }

        [Fact]
        public void WithBoxedEqualStartPosition_ReturnsTrue()
        {
            Assert.True(StartPosition.Last().Equals((object)StartPosition.Last()));
        }
    }
}
