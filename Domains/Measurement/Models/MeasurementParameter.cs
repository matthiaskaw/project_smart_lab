using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartLab.Domains.Measurement.Models
{
    public class MeasurementParameter
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public string DisplayName { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public ParameterType Type { get; set; }
        
        [Required]
        public object DefaultValue { get; set; } = new object();
        
        public bool IsRequired { get; set; }
        
        public string? Unit { get; set; }
        
        public Dictionary<string, object> ValidationRules { get; set; } = new();
    }

    [JsonConverter(typeof(ParameterTypeConverter))]
    public enum ParameterType
    {
        String,
        Integer,
        Double,
        Boolean,
        DateTime
    }

    public class ParameterTypeConverter : JsonConverter<ParameterType>
    {
        public override ParameterType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return value?.ToLowerInvariant() switch
            {
                "string" => ParameterType.String,
                "integer" => ParameterType.Integer,
                "double" => ParameterType.Double,
                "boolean" => ParameterType.Boolean,
                "datetime" => ParameterType.DateTime,
                _ => throw new JsonException($"Unable to convert \"{value}\" to ParameterType.")
            };
        }

        public override void Write(Utf8JsonWriter writer, ParameterType value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    public class StructuredMeasurementData
    {
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        public List<string> RawData { get; set; } = new();
        
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        public bool IsPartialData { get; set; } = false;
        
        public int BreakpointSequence { get; set; } = 0;
        
        public bool IsComplete { get; set; } = true;
    }
}