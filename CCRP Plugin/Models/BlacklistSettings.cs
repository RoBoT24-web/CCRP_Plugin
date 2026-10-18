using System.Collections.Generic;
using Newtonsoft.Json;

namespace CCRP_Plugin.Models
{
    public class BlacklistSettings
    {
        [JsonConverter(typeof(CompactGiveSoftBlacklistListConverter))]
        public List<GiveSoftBlacklist> GiveItemBlacklist { get; set; }

        [JsonConverter(typeof(CompactGiveSoftBlacklistListConverter))]
        public List<GiveSoftBlacklist> PickupItemBlacklist { get; set; }

        [JsonConverter(typeof(CompactGiveSoftBlacklistListConverter))]
        public List<GiveSoftBlacklist> GiveAndPickupItemBlacklist { get; set; }

        [JsonConverter(typeof(CompactVehicleSpawnBlacklistListConverter))]
        public List<VehicleSpawnBlacklist> VehicleSpawnBlacklist { get; set; }

        public BlacklistSettings()
        {
            GiveItemBlacklist = new List<GiveSoftBlacklist>();
            PickupItemBlacklist = new List<GiveSoftBlacklist>();
            GiveAndPickupItemBlacklist = new List<GiveSoftBlacklist>();
            VehicleSpawnBlacklist = new List<VehicleSpawnBlacklist>();
        }
    }

    public abstract class CompactJsonArrayConverter<T> : JsonConverter
    {
        public override bool CanConvert(System.Type objectType)
        {
            return objectType == typeof(List<T>);
        }

        public override bool CanRead => false;

        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new System.NotSupportedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            foreach (T entry in (List<T>)value)
            {
                writer.WriteRawValue(JsonConvert.SerializeObject(entry, Formatting.None));
            }
            writer.WriteEndArray();
        }
    }

    public class CompactGiveSoftBlacklistListConverter : CompactJsonArrayConverter<GiveSoftBlacklist>
    {
    }

    public class CompactVehicleSpawnBlacklistListConverter : CompactJsonArrayConverter<VehicleSpawnBlacklist>
    {
    }
}