using FileConvert.Core.ValueObjects;
using Xunit;

namespace FileConvert.UnitTests
{
    public class ValueTypeTests
    {
        [Fact]
        public void TestFileExtensionEquality()
        {
            //arrange
            var gifExt = FileExtension.gif;
            var gifExtText = ".gif";

            //assert
            Assert.NotNull(gifExt);
            Assert.Equal(gifExt, gifExtText);
            Assert.Equal(gifExt, FileExtension.gif);
            Assert.Equal(gifExtText, gifExt.ToString());
            Assert.True(gifExt.Equals(".gif"));
            Assert.True(gifExt.Equals(FileExtension.gif));
            Assert.True(gifExt.Equals(gifExt));
            Assert.True(gifExt == FileExtension.gif);
            Assert.Equal(gifExt.GetHashCode(), FileExtension.gif.GetHashCode());
            Assert.NotEqual(gifExt.GetHashCode(), FileExtension.jpg.GetHashCode());
        }



    }
}
