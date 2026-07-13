namespace ParkingGarage.Tests;

using ParkingGarage;

public class VehicleTagValidatorTests
{
    [Theory]
    [InlineData("ABC")]          // minimum length (3)
    [InlineData("ABC123")]
    [InlineData("HH1277I")]
    [InlineData("LUVTOAD")]
    [InlineData("123456789")]    // maximum length (9)
    public void Test_ValidTags(string tag)
    {
        Assert.True(VehicleTagValidator.IsValid(tag));
    }

    [Theory]
    [InlineData("")]             // empty
    [InlineData("AB")]           // too short (2)
    [InlineData("ABCD123456")]   // too long (10)
    [InlineData("ABC-12")]       // hyphen
    [InlineData("ABC 12")]       // space
    [InlineData("ABC_12")]       // underscore
    [InlineData("LUV.TOAD")]     // period
    public void Test_InvalidTags(string tag)
    {
        Assert.False(VehicleTagValidator.IsValid(tag));
    }

    [Fact]
    public void Test_NullTag_IsInvalid()
    {
        Assert.False(VehicleTagValidator.IsValid(null!));
    }
}
