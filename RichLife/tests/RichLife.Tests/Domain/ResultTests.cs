using RichLife.Domain.Common;

namespace RichLife.Tests.Domain;

public class ResultTests
{
    [Fact]
    public void Ok_IsSuccess_WithNoError()
    {
        var result = Result.Ok();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_CarriesTheErrorMessage()
    {
        var result = Result.Fail("boom");

        Assert.False(result.IsSuccess);
        Assert.Equal("boom", result.Error);
    }

    [Fact]
    public void OkOfT_CarriesTheValue()
    {
        var result = Result.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void FailOfT_ThrowsWhenTheValueIsRead()
    {
        var result = Result.Fail<int>("nope");

        Assert.False(result.IsSuccess);
        Assert.Equal("nope", result.Error);

        var ex = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("nope", ex.Message);
    }
}
