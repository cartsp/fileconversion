using FileConvert.Core.ValueObjects;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.IO;
using System.Linq;
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
            fixture.driver.Url = "https://devfileconversion.z33.web.core.windows.net/";

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
            fixture.driver.Url = "https://devfileconversion.z33.web.core.windows.net/";

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
            fixture.driver.Url = "https://devfileconversion.z33.web.core.windows.net/";

            //Act
            var wait = new WebDriverWait(fixture.driver, new TimeSpan(0, 3, 0));
            var FileControl = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.Id("file-1")));

            //Assert
            Assert.NotNull(FileControl);
        }

        [Fact]
        public void TestAvailableFileConversionAppears()
        {
            //Arrange
            fixture.driver.Url = "https://devfileconversion.z33.web.core.windows.net/";

            //Act
            var wait = new WebDriverWait(fixture.driver, new TimeSpan(0, 3, 0));
            var uploadElement = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.Id("file-1")));
            
            var filepath = Directory.GetCurrentDirectory() + $"{Path.DirectorySeparatorChar}Documents{Path.DirectorySeparatorChar}cities.csv";
            uploadElement.SendKeys(filepath);

            wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.ClassName("conversion-choices")));
            var conversionSelection = fixture.driver.FindElementsByClassName("conversion-choices");
            var htmlOption = conversionSelection.First().Text;
            
            //Assert
            Assert.NotNull(conversionSelection);
            Assert.NotEmpty(conversionSelection);
            Assert.Equal(FileExtension.xlsx, htmlOption);
        }

        [Fact]
        public void TestNoAvailableFileConversionAppears()
        {
            //Arrange
            fixture.driver.Url = "https://devfileconversion.z33.web.core.windows.net/";

            //Act
            var wait = new WebDriverWait(fixture.driver, new TimeSpan(0, 3, 0));
            var uploadElement = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.Id("file-1")));

            var filepath = Directory.GetCurrentDirectory() + $"{Path.DirectorySeparatorChar}Documents{Path.DirectorySeparatorChar}test.dgn";
            uploadElement.SendKeys(filepath);

            var noConversionsFound = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.ClassName("no-convertors-found")));

            //Assert
            Assert.NotNull(noConversionsFound);
            Assert.Equal("No file conversions available for this file type", noConversionsFound.Text);
        }
    }
}
