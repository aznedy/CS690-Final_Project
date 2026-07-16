namespace ParkingGarage;

using System.Text.Json;

public class DataManager {

    static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions { WriteIndented = true };

    const string ConfigFile = "garage-config.json";
    const string SessionsFile = "ParkingSessions.json";
    const string SubscribersFile = "subscribers.json";

    public GarageConfig Config { get; }
    public RateSchedule RateSchedule { get; }
    public List<ParkingSession> Sessions { get; }
    public List<Subscriber> Subscribers { get; }

    public DataManager() {
        Config = LoadConfig();
        RateSchedule = new RateSchedule(Config.BaseRatePerHour, Config.OvertimeRatePerHour, Config.AllowedHours);
        Sessions = LoadList<ParkingSession>(SessionsFile);
        Subscribers = LoadList<Subscriber>(SubscribersFile);
    }

    // ---------------- Persistence ----------------

    GarageConfig LoadConfig() {
        if(File.Exists(ConfigFile)) {
            var config = JsonSerializer.Deserialize<GarageConfig>(File.ReadAllText(ConfigFile));
            if(config != null) {
                return config;
            }
        }
        // write a starter config so the file can be created on first launch
        var defaultConfig = new GarageConfig {
            Capacity = 50,
            BaseRatePerHour = 5.00m,
            OvertimeRatePerHour = 6.00m,
            AllowedHours = 4,
            ReservedSpots = new List<string> {"01","02","03","04","05","06","07","08","09","10"}
        };
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(defaultConfig, jsonOptions));
        return defaultConfig;
    }

    List<T> LoadList<T>(string fileName) {
        if(File.Exists(fileName)) {
            var list = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(fileName));
            if(list != null) {
                return list;
            }
        }
        return new List<T>();
    }

    public void SaveSessions() {
        File.WriteAllText(SessionsFile, JsonSerializer.Serialize(Sessions, jsonOptions));
    }

    public void SaveSubscribers() {
        File.WriteAllText(SubscribersFile, JsonSerializer.Serialize(Subscribers, jsonOptions));
    }

    // ---------------- Spot Helper Methods ----------------

    static int NormalizeSpot(string spot) {
        return int.TryParse(spot, out var n) ? n : -1;
    }

    // every physical spot in the garage, e.g. "01".."50"
    public List<string> AllSpots() {
        var spots = new List<string>();
        for(int i = 1; i <= Config.Capacity; i++) {
            spots.Add(i.ToString("D2"));
        }
        return spots;
    }

    public bool IsReserved(string spot) {
        int n = NormalizeSpot(spot);
        foreach(var reserved in Config.ReservedSpots) {
            if(NormalizeSpot(reserved) == n) {
                return true;
            }
        }
        return false;
    }

    // general (non-reserved) spots are the ones ad hoc guests may be assigned
    public List<string> GeneralSpots() {
        return AllSpots().Where(spot => !IsReserved(spot)).ToList();
    }

    // FR-05: reserved spots with no active subscriber can be handed to ad hoc
    // guests as overflow when the general area is full. An expired subscriber
    // leaves the spot [U] (Reserved, Unassigned) and therefore assignable.
    public List<string> AssignableReservedSpots() {
        return AllSpots().Where(spot => IsReserved(spot) && ActiveSubscriberForSpot(spot) == null).ToList();
    }

    // Fill order that leaves an empty spot between vehicles (e.g. 11,13,15,...
    // then 12,14,...). Reduces door dings versus sequential filling.
    static List<string> EveryOtherOrder(IEnumerable<string> spots) {
        var ordered = spots.ToList();
        var odds = ordered.Where(spot => NormalizeSpot(spot) % 2 == 1);
        var evens = ordered.Where(spot => NormalizeSpot(spot) % 2 == 0);
        return odds.Concat(evens).ToList();
    }

    public List<ParkingSession> ActiveSessions() {
        return Sessions.Where(session => session.IsActive()).ToList();
    }

    public bool IsSpotOccupied(string spot) {
        int n = NormalizeSpot(spot);
        return ActiveSessions().Any(session => NormalizeSpot(session.AssignedSpace) == n);
    }

    // The next spot to assign an ad hoc guest. General spots are handed out
    // first, filled every-other for spacing (customer satisfaction).
    // when the general area is full, fall back to reserved spots that have
    // no active subscriber, likewise filled every-other.
    public string? FindAvailableSpot() {
        foreach(var spot in EveryOtherOrder(GeneralSpots())) {
            if(!IsSpotOccupied(spot)) {
                return spot;
            }
        }
        foreach(var spot in EveryOtherOrder(AssignableReservedSpots())) {
            if(!IsSpotOccupied(spot)) {
                return spot;
            }
        }
        return null;
    }

    // Free general spots, excludes reserved overflow, used for the display.
    public int AvailableSpots() {
        return GeneralSpots().Count(spot => !IsSpotOccupied(spot));
    }

    // Reserved overflow spots currently free for ad hoc use
    public int AvailableReservedSpots() {
        return AssignableReservedSpots().Count(spot => !IsSpotOccupied(spot));
    }

    // Entry is blocked only when no spot at all can be assigned
    // V3 - neither a general spot nor a reserved overflow spot.
    public bool IsFull() {
        return FindAvailableSpot() == null;
    }

    // ---------------- Subscribers ----------------

    public Subscriber? FindSubscriberByTag(string vehicleTag) {
        return Subscribers.FirstOrDefault(subscriber => subscriber.VehicleTag == vehicleTag);
    }

    public Subscriber? ActiveSubscriberForSpot(string spot) {
        int n = NormalizeSpot(spot);
        return Subscribers.FirstOrDefault(subscriber => NormalizeSpot(subscriber.AssignedSpot) == n && subscriber.IsActive());
    }

    public void AddSubscriber(Subscriber subscriber) {
        Subscribers.Add(subscriber);
        SaveSubscribers();
    }

    public void RemoveSubscriber(Subscriber subscriber) {
        Subscribers.Remove(subscriber);
        SaveSubscribers();
    }

    // ---------------- Entry / Exit ----------------

    // Record the entry timestamp for an ad hoc guest and assign a spot
    public ParkingSession RecordEntry(string vehicleTag, string assignedSpace) {
        var session = new ParkingSession {
            EntryTime = DateTime.Now.ToString("yyyyMMddHHmmss"),
            VehicleTag = vehicleTag,
            AssignedSpace = assignedSpace,
            ExitTime = null
        };
        Sessions.Add(session);
        SaveSessions();
        return session;
    }

    // Set the charge owed for a session without finalizing the exit,
    public void SetAmountCharged(ParkingSession session, decimal amountCharged) {
        session.AmountCharged = amountCharged;
        SaveSessions();
    }

    // Apply a payment and append it to the session's payment log.
    public Payment AddPayment(ParkingSession session, decimal amount, string method) {
        var payment = new Payment {
            Amount = amount,
            Method = method,
            Timestamp = DateTime.Now.ToString("yyyyMMddHHmmss")
        };
        session.Payments.Add(payment);
        session.AmountPaid = session.TotalPaid();
        SaveSessions();
        return payment;
    }

    // Finalize the exit once the balance is cleared.
    public void RecordExit(ParkingSession session, DateTime exitTime, decimal amountCharged) {
        session.ExitTime = exitTime.ToString("yyyyMMddHHmmss");
        session.AmountCharged = amountCharged;
        SaveSessions();
    }

    public List<Payment> PaymentsOn(DateTime day) {
        return Sessions
            .SelectMany(session => session.Payments)
            .Where(payment => payment.TimestampDateTime().Date == day.Date)
            .ToList();
    }
}

