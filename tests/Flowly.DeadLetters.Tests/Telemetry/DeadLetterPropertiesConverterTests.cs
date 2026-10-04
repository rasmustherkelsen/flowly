using System.Diagnostics;
using System.Text.Json;
using Flowly.DeadLetters.Telemetry;

namespace Flowly.DeadLetters.Tests.Telemetry;

public class DeadLetterPropertiesConverterTests
{
    public class ParseActivityContextFromJson
    {
        [Fact]
        public void WithNoTraceparent_ReturnsDefault()
        {
            var result = DeadLetterPropertiesConverter.ParseActivityContext("""{"source":"orders-api"}""");

            Assert.Equal(default, result);
        }

        [Fact]
        public void WithValidTraceparent_ReturnsParsedContext()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var traceparent = $"00-{traceId}-{spanId}-01";

            var result = DeadLetterPropertiesConverter.ParseActivityContext($$"""{"traceparent":"{{traceparent}}"}""");

            Assert.Equal(traceId, result.TraceId);
            Assert.Equal(spanId, result.SpanId);
        }

        [Fact]
        public void WithValidTraceparentAndTracestate_IncludesTracestate()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var traceparent = $"00-{traceId}-{spanId}-01";

            var result = DeadLetterPropertiesConverter.ParseActivityContext($$"""{"traceparent":"{{traceparent}}","tracestate":"vendor=value"}""");

            Assert.Equal("vendor=value", result.TraceState);
        }

        [Fact]
        public void WithInvalidTraceparent_ReturnsDefault()
        {
            var result = DeadLetterPropertiesConverter.ParseActivityContext("""{"traceparent":"not-a-valid-traceparent"}""");

            Assert.Equal(default, result);
        }
    }

    public class ParseActivityContextFromDictionary
    {
        [Fact]
        public void WithMissingTraceparentKey_ReturnsDefault()
        {
            var result = DeadLetterPropertiesConverter.ParseActivityContext(new Dictionary<string, object> { ["source"] = "orders-api" });

            Assert.Equal(default, result);
        }

        [Fact]
        public void WithNonStringTraceparentValue_ReturnsDefault()
        {
            var result = DeadLetterPropertiesConverter.ParseActivityContext(new Dictionary<string, object> { ["traceparent"] = 123 });

            Assert.Equal(default, result);
        }
    }

    public class ConvertProperties
    {
        [Fact]
        public void ConvertsStringValue()
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, object>>("""{"source":"orders-api"}""")!;

            var result = DeadLetterPropertiesConverter.ConvertProperties(raw);

            Assert.Equal("orders-api", result["source"]);
        }

        [Fact]
        public void ConvertsIntegerValue()
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, object>>("""{"priority":42}""")!;

            var result = DeadLetterPropertiesConverter.ConvertProperties(raw);

            Assert.Equal(42, result["priority"]);
        }

        [Fact]
        public void ConvertsLongValue()
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, object>>("""{"bigNumber":9999999999}""")!;

            var result = DeadLetterPropertiesConverter.ConvertProperties(raw);

            Assert.Equal(9999999999L, result["bigNumber"]);
        }

        [Fact]
        public void ConvertsDoubleValue()
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, object>>("""{"ratio":1.5}""")!;

            var result = DeadLetterPropertiesConverter.ConvertProperties(raw);

            Assert.Equal(1.5, result["ratio"]);
        }

        [Fact]
        public void ConvertsTrueValue()
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, object>>("""{"is-urgent":true}""")!;

            var result = DeadLetterPropertiesConverter.ConvertProperties(raw);

            Assert.Equal(true, result["is-urgent"]);
        }

        [Fact]
        public void ConvertsFalseValue()
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, object>>("""{"is-urgent":false}""")!;

            var result = DeadLetterPropertiesConverter.ConvertProperties(raw);

            Assert.Equal(false, result["is-urgent"]);
        }

        [Fact]
        public void PassesThroughNonJsonElementValueUnchanged()
        {
            var raw = new Dictionary<string, object> { ["missing"] = null! };

            var result = DeadLetterPropertiesConverter.ConvertProperties(raw);

            Assert.Null(result["missing"]);
        }

        [Fact]
        public void ConvertsArrayValueToItsStringRepresentation()
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, object>>("""{"tags":["a","b"]}""")!;

            var result = DeadLetterPropertiesConverter.ConvertProperties(raw);

            Assert.Contains("a", result["tags"].ToString());
        }

        [Fact]
        public void WithEmptyInput_ReturnsEmptyDictionary()
        {
            var result = DeadLetterPropertiesConverter.ConvertProperties([]);

            Assert.Empty(result);
        }
    }
}
