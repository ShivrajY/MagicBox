using MagicBox.Core;
using Microsoft.Playwright;

namespace MagicBox.Browser;

internal sealed class ConfiguredLoginRunner
{
    private readonly IChallengeHandler challengeHandler;

    public ConfiguredLoginRunner(IChallengeHandler challengeHandler)
    {
        this.challengeHandler = challengeHandler;
    }

    public async Task RunAsync(IPage page, AuthDefinition auth, ISecretResolver? secrets, Uri target, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(auth.LoginUrl))
        {
            await page.GotoAsync(auth.LoginUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
        }

        foreach (var step in auth.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timeout = (float)Math.Clamp(step.TimeoutMs, 100, 600_000);
            switch (step.Kind.ToLowerInvariant())
            {
                case "navigate":
                    await page.GotoAsync(step.Value ?? target.AbsoluteUri, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeout }).ConfigureAwait(false);
                    break;
                case "fill":
                    var fillValue = step.SecretName is null
                        ? step.Value ?? string.Empty
                        : secrets?.Resolve(step.SecretName) ?? throw new InvalidDataException($"Configured login secret '{step.SecretName}' was not resolved.");
                    await page.Locator(step.Selector ?? throw new InvalidDataException("Login fill step requires selector.")).FillAsync(fillValue, new LocatorFillOptions { Timeout = timeout }).ConfigureAwait(false);
                    break;
                case "click":
                    await page.Locator(step.Selector ?? throw new InvalidDataException("Login click step requires selector.")).ClickAsync(new LocatorClickOptions { Timeout = timeout }).ConfigureAwait(false);
                    break;
                case "press":
                    await page.Locator(step.Selector ?? throw new InvalidDataException("Login press step requires selector.")).PressAsync(step.Value ?? "Enter", new LocatorPressOptions { Timeout = timeout }).ConfigureAwait(false);
                    break;
                case "waitforselector":
                    await page.WaitForSelectorAsync(step.Selector ?? throw new InvalidDataException("Login wait step requires selector."), new PageWaitForSelectorOptions { Timeout = timeout }).ConfigureAwait(false);
                    break;
                case "waitforurl":
                    await page.WaitForURLAsync(step.Value ?? "**", new PageWaitForURLOptions { Timeout = timeout }).ConfigureAwait(false);
                    break;
                case "manualchallenge":
                    if (!await challengeHandler.HandleAsync("configured-login", target, cancellationToken).ConfigureAwait(false))
                    {
                        throw new ChallengeRequiredException("Manual challenge was not completed before the challenge timeout.");
                    }
                    break;
                case "assertselector":
                    var assertionLocator = page.Locator(step.Selector ?? throw new InvalidDataException("Login assertion requires selector."));
                    await assertionLocator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = timeout }).ConfigureAwait(false);
                    if (!await assertionLocator.IsVisibleAsync().ConfigureAwait(false))
                    {
                        throw new InvalidOperationException("Configured login assertion failed.");
                    }
                    break;
                default:
                    throw new InvalidDataException($"Unsupported login step '{step.Kind}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(auth.SuccessSelector))
        {
            await page.WaitForSelectorAsync(auth.SuccessSelector, new PageWaitForSelectorOptions { Timeout = 30_000 }).ConfigureAwait(false);
        }
    }
}
