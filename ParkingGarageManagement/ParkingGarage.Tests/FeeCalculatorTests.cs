namespace ParkingGarage.Tests;

using ParkingGarage;

public class FeeCalculatorTests
{
    RateSchedule rateSchedule;

    public FeeCalculatorTests() {
        // base $5.00/hr, overtime $6.00/hr, 4 hours allowed
        rateSchedule = new RateSchedule(5.00m, 6.00m, 4);
    }

    [Fact]
    public void Test_CalculateFee_WithinAllowedDuration()
    {
        DateTime entryTime = new DateTime(2026,7,1,9,0,0);
        DateTime exitTime = new DateTime(2026,7,1,11,0,0); // 2 hours

        var fee = FeeCalculator.CalculateFee(entryTime, exitTime, rateSchedule);

        Assert.Equal(10.00m, fee); // 2 hours * $5.00
    }

    [Fact]
    public void Test_CalculateFee_ExactlyAllowedDuration()
    {
        DateTime entryTime = new DateTime(2026,7,1,9,0,0);
        DateTime exitTime = new DateTime(2026,7,1,13,0,0); // 4 hours

        var fee = FeeCalculator.CalculateFee(entryTime, exitTime, rateSchedule);

        Assert.Equal(20.00m, fee); // 4 hours * $5.00, no overtime
    }

    [Fact]
    public void Test_CalculateFee_ExceedsAllowedDuration()
    {
        DateTime entryTime = new DateTime(2026,7,1,9,0,0);
        DateTime exitTime = new DateTime(2026,7,1,15,0,0); // 6 hours

        var fee = FeeCalculator.CalculateFee(entryTime, exitTime, rateSchedule);

        Assert.Equal(32.00m, fee); // 4 hours * $5.00 + 2 hours * $6.00
    }

    [Fact]
    public void Test_CalculateFee_RoundsPartialHourUp()
    {
        DateTime entryTime = new DateTime(2026,7,1,9,0,0);
        DateTime exitTime = new DateTime(2026,7,1,10,15,0); // 1 hour 15 minutes

        var fee = FeeCalculator.CalculateFee(entryTime, exitTime, rateSchedule);

        Assert.Equal(10.00m, fee); // rounds up to 2 hours * $5.00
    }
}
