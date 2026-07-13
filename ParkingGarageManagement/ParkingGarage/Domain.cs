namespace ParkingGarage;

using System.Text.Json.Serialization;

// Garage configuration, loaded from garage-config.json.
public class GarageConfig {
    [JsonPropertyName("Capacity")] public int Capacity { get; set; }
    [JsonPropertyName("BaseRatePerHour")] public decimal BaseRatePerHour { get; set; }
    [JsonPropertyName("OvertimeRatePerHour")] public decimal OvertimeRatePerHour { get; set; }
    [JsonPropertyName("AllowedHours")] public int AllowedHours { get; set; }
    [JsonPropertyName("ReservedSpots")] public List<string> ReservedSpots { get; set; } = new List<string>();
}

// Fee rates derived from the configuration.
public class RateSchedule {
    public decimal BaseRatePerHour { get; }
    public decimal OvertimeRatePerHour { get; }
    public int AllowedHours { get; }

    public RateSchedule(decimal baseRatePerHour, decimal overtimeRatePerHour, int allowedHours) {
        this.BaseRatePerHour = baseRatePerHour;
        this.OvertimeRatePerHour = overtimeRatePerHour;
        this.AllowedHours = allowedHours;
    }
}

// A subscriber record, persisted in subscribers.json.
public class Subscriber {
    [JsonPropertyName("Subscriber_Name")] public string Name { get; set; } = "";
    [JsonPropertyName("Assigned_Spot")] public string AssignedSpot { get; set; } = "";
    [JsonPropertyName("Vehicle_Tag")] public string VehicleTag { get; set; } = "";
    [JsonPropertyName("Subscription_Expiry")] public string SubscriptionExpiry { get; set; } = "";

    public bool IsActive() {
        if(DateTime.TryParseExact(SubscriptionExpiry, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var expiry)) {
            return expiry.Date >= DateTime.Now.Date;
        }
        return false;
    }
}

// A parking session, persisted in ParkingSessions.json.
// Exit_Time (and the payment fields) stay null until the vehicle exits.
public class ParkingSession {
    [JsonPropertyName("Entry_Time")] public string EntryTime { get; set; } = "";
    [JsonPropertyName("Vehicle_Tag")] public string VehicleTag { get; set; } = "";
    [JsonPropertyName("Assigned_Space")] public string AssignedSpace { get; set; } = "";
    [JsonPropertyName("Exit_Time")] public string? ExitTime { get; set; }
    [JsonPropertyName("Amount_Charged")] public decimal? AmountCharged { get; set; }
    [JsonPropertyName("Amount_Paid")] public decimal? AmountPaid { get; set; }

    public bool IsActive() {
        return ExitTime == null;
    }

    public DateTime EntryDateTime() {
        return DateTime.ParseExact(EntryTime, "yyyyMMddHHmmss", null);
    }
}


public class FeeBreakdown {
    public TimeSpan Duration { get; init; }
    public int BillableHours { get; init; }
    public int BaseHours { get; init; }
    public decimal BaseRatePerHour { get; init; } 
    public decimal BaseFee { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal OvertimeRatePerHour { get; init; }
    public decimal OvertimeFee { get; init; }
    public decimal Total { get; init; }
}

