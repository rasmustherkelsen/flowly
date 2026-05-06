using Flowly.Jobs.Model;

namespace Flowly.Jobs.Tests.Model;

public class JobIdTests
{
    public class Constructor
    {
        [Fact]
        public void Parameterless_GeneratesNonEmptyGuid()
        {
            var jobId = new JobId();

            Assert.NotEqual(Guid.Empty, jobId.InnerId);
        }

        [Fact]
        public void Parameterless_EachInstanceHasDistinctInnerId()
        {
            var first = new JobId();
            var second = new JobId();

            Assert.NotEqual(first.InnerId, second.InnerId);
        }

        [Fact]
        public void WithGuid_StoresProvidedInnerId()
        {
            var guid = Guid.NewGuid();

            var jobId = new JobId(guid);

            Assert.Equal(guid, jobId.InnerId);
        }
    }

    public class ToStringMethod
    {
        [Fact]
        public void ReturnsInnerIdAsString()
        {
            var guid = Guid.NewGuid();
            var jobId = new JobId(guid);

            Assert.Equal(guid.ToString(), jobId.ToString());
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoJobIdsWithSameInnerId_AreEqual()
        {
            var guid = Guid.NewGuid();
            var first = new JobId(guid);
            var second = new JobId(guid);

            Assert.Equal(first, second);
        }

        [Fact]
        public void TwoJobIdsWithDifferentInnerIds_AreNotEqual()
        {
            var first = new JobId(Guid.NewGuid());
            var second = new JobId(Guid.NewGuid());

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void TwoJobIdsWithSameInnerId_HaveSameHashCode()
        {
            var guid = Guid.NewGuid();
            var first = new JobId(guid);
            var second = new JobId(guid);

            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }
    }
}
