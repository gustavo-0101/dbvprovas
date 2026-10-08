using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;

namespace Dbvprovas.App.Smoke;

// Fumaça no Android (D-105, D-118): abre o app, faz o login de dev como a Aurora e confere o clube.
// Os scripts/android-smoke.ps1 (local) e ci-android-smoke.sh (CI) preparam o emulador, a API e o Appium.
public sealed class AppSmokeTests
{
    private const string Package = "io.github.gustavo0101.dbvprovas";

    [Fact]
    public void CA_TEN_005_App_shows_dev_user_club()
    {
        var appium = Environment.GetEnvironmentVariable("DBV_APPIUM_URL");
        Assert.SkipUnless(appium is not null, "Defina DBV_APPIUM_URL (o scripts/android-smoke.ps1 faz isso).");

        // O cliente acrescenta o prefixo "appium:" aos nomes das opções adicionais.
        var options = new AppiumOptions { PlatformName = "Android", AutomationName = AutomationName.AndroidUIAutomator2 };
        options.AddAdditionalAppiumOption("appPackage", Package);
        options.AddAdditionalAppiumOption("appActivity", ".MainActivity");
        options.AddAdditionalAppiumOption("noReset", false);
        options.AddAdditionalAppiumOption("newCommandTimeout", 180);
        using var driver = new AndroidDriver(new Uri(appium!), options, TimeSpan.FromMinutes(3));

        WaitFor(driver, "new UiSelector().textContains(\"Aurora\")").Click();

        Assert.NotNull(WaitFor(driver, "new UiSelector().text(\"Clube Águias\")"));
    }

    // O conteúdo do WebView aparece para o UiAutomator pela árvore de acessibilidade.
    private static IWebElement WaitFor(AndroidDriver driver, string uiSelector)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            var found = driver.FindElements(MobileBy.AndroidUIAutomator(uiSelector));
            if (found.Count > 0)
                return found[0];
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"Element did not appear: {uiSelector}");
    }
}
