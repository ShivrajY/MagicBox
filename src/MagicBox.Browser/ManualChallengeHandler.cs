using MagicBox.Core;

namespace MagicBox.Browser;

public sealed class ManualChallengeHandler : IChallengeHandler
{
    public async Task<bool> HandleAsync(string adapterId, Uri uri, CancellationToken cancellationToken)
    {
        if (!Environment.UserInteractive)
        {
            return false;
        }

        Console.Error.WriteLine($"Manual challenge required for adapter '{adapterId}' at {uri}.");
        Console.Error.WriteLine("Complete the challenge in the headed browser, then press Enter to continue.");
        var readLine = Task.Run(Console.ReadLine, CancellationToken.None);
        try
        {
            await readLine.WaitAsync(TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
            return !cancellationToken.IsCancellationRequested;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
