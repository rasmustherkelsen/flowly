using Flowly.DeadLetters.Services;

namespace Flowly.DeadLetters.Tests.Services;

public class ReplacementMessageBodyValidatorTests
{
    public class Validate
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\r\n\t")]
        public void WithEmptyOrWhitespaceReplacement_Throws(string replacement)
        {
            Assert.Throws<InvalidReplacementMessageBodyException>(() => ReplacementMessageBodyValidator.Validate("plain", replacement));
        }

        [Fact]
        public void WithReplacementOverTheMaximumSize_Throws()
        {
            var replacement = new string('a', ReplacementMessageBodyValidator.MaxBytes + 1);

            Assert.Throws<InvalidReplacementMessageBodyException>(() => ReplacementMessageBodyValidator.Validate("plain", replacement));
        }

        [Fact]
        public void WithReplacementExactlyTheMaximumSize_Succeeds()
        {
            ReplacementMessageBodyValidator.Validate("plain", new string('a', ReplacementMessageBodyValidator.MaxBytes));
        }

        [Fact]
        public void CountsTheSizeInUtf8Bytes()
        {
            var replacement = new string('æ', ReplacementMessageBodyValidator.MaxBytes / 2 + 1);

            Assert.Throws<InvalidReplacementMessageBodyException>(() => ReplacementMessageBodyValidator.Validate("plain", replacement));
        }

        [Fact]
        public void WithJsonStoredBodyAndInvalidJsonReplacement_Throws()
        {
            Assert.Throws<InvalidReplacementMessageBodyException>(() => ReplacementMessageBodyValidator.Validate("""{"a":1}""", """{"a":"""));
        }

        [Fact]
        public void WithJsonStoredBodyAndValidJsonReplacement_Succeeds()
        {
            ReplacementMessageBodyValidator.Validate("""{"a":1}""", """{"a":2}""");
        }

        [Fact]
        public void WithNonJsonStoredBody_AcceptsAnyNonEmptyReplacement()
        {
            ReplacementMessageBodyValidator.Validate("not json", "also not json");
        }

        [Fact]
        public void WithJsonScalarStoredBody_RequiresJsonReplacement()
        {
            Assert.Throws<InvalidReplacementMessageBodyException>(() => ReplacementMessageBodyValidator.Validate("42", "forty-two"));
        }
    }
}
