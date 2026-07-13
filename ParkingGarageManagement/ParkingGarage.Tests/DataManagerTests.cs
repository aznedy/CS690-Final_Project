namespace ParkingGarage.Tests;

using ParkingGarage;

public class DataManagerTests
{
    DataManager dataManager;

    public DataManagerTests() {
        // capacity 15, spots 01-10 reserved, general spots 11-15 (5 of them)
        File.WriteAllText("garage-config.json",
            "{\"Capacity\":15,\"BaseRatePerHour\":5.00,\"OvertimeRatePerHour\":6.00,\"AllowedHours\":4," +
            "\"ReservedSpots\":[\"01\",\"02\",\"03\",\"04\",\"05\",\"06\",\"07\",\"08\",\"09\",\"10\"]}");
        File.WriteAllText("ParkingSessions.json", "[]");
        File.WriteAllText("subscribers.json",
            "[{\"Subscriber_Name\":\"Test Sub\",\"Assigned_Spot\":\"07\",\"Vehicle_Tag\":\"SUBTAG\",\"Subscription_Expiry\":\"20991231\"}]");
        dataManager = new DataManager();
    }

    [Fact]
    public void Test_AvailableSpots_ExcludesReservedSpots()
    {
        Assert.Equal(5, dataManager.AvailableSpots()); // only 11-15 are general
    }

    [Fact]
    public void Test_FindAvailableSpot_ReturnsLowestGeneralSpot()
    {
        Assert.Equal("11", dataManager.FindAvailableSpot());
    }

    [Fact]
    public void Test_IsReserved()
    {
        Assert.True(dataManager.IsReserved("07"));
        Assert.False(dataManager.IsReserved("11"));
    }

    [Fact]
    public void Test_RecordEntry_OccupiesSpotAndReducesAvailability()
    {
        dataManager.RecordEntry("ABC123", "11");

        Assert.True(dataManager.IsSpotOccupied("11"));
        Assert.Equal(4, dataManager.AvailableSpots());
        Assert.Equal("12", dataManager.FindAvailableSpot());
    }

    [Fact]
    public void Test_ActiveSubscriberForSpot()
    {
        Assert.NotNull(dataManager.ActiveSubscriberForSpot("07"));
        Assert.Null(dataManager.ActiveSubscriberForSpot("08"));
    }

    [Fact]
    public void Test_IsFull_WhenAllGeneralSpotsOccupied()
    {
        Assert.False(dataManager.IsFull());
        dataManager.RecordEntry("A", "11");
        dataManager.RecordEntry("B", "12");
        dataManager.RecordEntry("C", "13");
        dataManager.RecordEntry("D", "14");
        dataManager.RecordEntry("E", "15");
        Assert.True(dataManager.IsFull());
    }
}
