using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using Xunit;

namespace FileConvert.UiTests
{
    public class UiTests : IClassFixture<ChromeDriverFixture>
    {
        ChromeDriverFixture fixture;

        public UiTests(ChromeDriverFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public void TestCanOpenDevSite()
        {
            //Arrange
            fixture.driver.Url = "https://fileconversiontools.azureedge.net/";

            //Act
            var PageTitle = fixture.driver.Title;
            var wait = new WebDriverWait(fixture.driver, new TimeSpan(0, 3, 0));

            //Assert
            Assert.Equal("File Conversion Tools", PageTitle);
        }

        [Fact]
        public void TestAppStartsUp()
        {
            //Arrange
            fixture.driver.Url = "https://fileconversiontools.azureedge.net/";

            //Act
            var wait = new WebDriverWait(fixture.driver, new TimeSpan(0, 3, 0));
            var FileLabel = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.Id("file-label")));
            
            //Assert
            Assert.NotNull(FileLabel);
        }

        [Fact]
        public void TestFileControlExists()
        {
            //Arrange
            fixture.driver.Url = "https://fileconversiontools.azureedge.net/";

            //Act
            var wait = new WebDriverWait(fixture.driver, new TimeSpan(0, 3, 0));
            var FileControl = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.Id("file-1")));

            //Assert
            Assert.NotNull(FileControl);
        }
    }
}
