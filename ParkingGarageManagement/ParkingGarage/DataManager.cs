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

    // ---------------- persistence ----------------

    GarageConfig LoadConfig() {
        if(File.Exists(ConfigFile)) {
            var config = JsonSerializer.Deserialize<GarageConfig>(File.ReadAllText(ConfigFile));
            if(config != null) {
                return config;
            }
        }
        // write a starter config so the file always exists
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

    // ---------------- spot helpers ----------------

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

    public List<ParkingSession> ActiveSessions() {
        return Sessions.Where(session => session.IsActive()).ToList();
    }

    public bool IsSpotOccupied(string spot) {
        int n = NormalizeSpot(spot);
        return ActiveSessions().Any(session => NormalizeSpot(session.AssignedSpace) == n);
    }

    // FR-01: the lowest-numbered general spot that no active session occupies
    public string? FindAvailableSpot() {
        foreach(var spot in GeneralSpots()) {
            if(!IsSpotOccupied(spot)) {
                return spot;
            }
        }
        return null;
    }

    public int AvailableSpots() {
        return GeneralSpots().Count(spot => !IsSpotOccupied(spot));
    }

    // FR-02: no general spot is free
    public bool IsFull() {
        return AvailableSpots() <= 0;
    }

    // ---------------- subscribers ----------------

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

    // ---------------- entry / exit ----------------

    // FR-03: record the entry timestamp for an ad hoc guest and assign a spot
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

    public void RecordExit(ParkingSession session, DateTime exitTime, decimal amountCharged) {
        session.ExitTime = exitTime.ToString("yyyyMMddHHmmss");
        session.AmountCharged = amountCharged;
        SaveSessions();
    }
}
