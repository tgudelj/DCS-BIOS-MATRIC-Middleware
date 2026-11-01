using DcsBios.Communicator.DataParsers;
using Xunit;

namespace DBMM.Tests;

public class StringParserTests {
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(16)]
    [InlineData(63)]
    [InlineData(64)]
    public void CompleteStringAndResetTrackEveryByte(int length) {
        var parser = new StringParser(0, length, "TEST");
        for (var offset = 0; offset < length; offset += 2) {
            parser.AddData(offset, 0x4141);
            Assert.Equal(offset + 2 >= length, parser.DataReady);
        }
        Assert.Equal(new string('A', length), parser.CurrentValue);
        parser.Reset();
        Assert.False(parser.DataReady);
        Assert.Empty(parser.CurrentValue);
        if (length > 2) {
            parser.AddData(length % 2 == 0 ? length - 2 : length - 1, 0x4242);
            Assert.False(parser.DataReady);
        }
    }
}
