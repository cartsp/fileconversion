using OpenQA.Selenium.Chrome;
using System;
using System.Diagnostics;

namespace FileConvert.UiTests
{
    public class ChromeDriverFixture : IDisposable
    {
        public ChromeOptions chromeOptions { get; private set; }
        public ChromeDriver driver { get; private set; }

        public ChromeDriverFixture()
        {
            chromeOptions = new ChromeOptions { Proxy = null };
            if (!Debugger.IsAttached)
            {
                chromeOptions.AddArguments("headless");
                chromeOptions.AddArguments("no-sandbox");
                chromeOptions.AddArguments("disable-dev-shm-usage");
            }
            driver = new ChromeDriver(chromeOptions);
        }

        public void Dispose()
        {
            driver.Close();
            driver.Dispose();
        }
    }
}
